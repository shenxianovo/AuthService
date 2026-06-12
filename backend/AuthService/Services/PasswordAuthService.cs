using AuthService.Common;
using AuthService.Data;
using AuthService.DTOs.Auth;
using AuthService.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Services
{
    public interface IPasswordAuthService
    {
        Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, string ipAddress, string device);
        Task<Result<AuthResponse>> LoginAsync(LoginRequest request, string ipAddress, string device);
        Task<Result> AddPasswordAsync(Guid userId, string password);

        /// <summary>
        /// Authenticated password change. Proof is the CURRENT password, not just the
        /// session — a stolen session must not be enough to take over the credential.
        /// Revokes every other session; the caller's own (<paramref name="sessionId"/>)
        /// survives.
        /// </summary>
        Task<Result> ChangePasswordAsync(Guid userId, Guid sessionId, string currentPassword, string newPassword);
    }

    public class PasswordAuthService(
        AppDbContext db,
        IAccountService account,
        ISessionService sessionService,
        IPasswordHasher<User> passwordHasher) : IPasswordAuthService
    {
        public async Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, string ipAddress, string device)
        {
            var username = request.Username.ToLowerInvariant();

            if (!UsernameValidator.IsValid(username))
                return Result<AuthResponse>.Fail(AuthError.InvalidUsername);

            var emailExists = await db.UserEmails
                .AnyAsync(e => e.Email == request.Email.ToLowerInvariant());
            if (emailExists)
                return Result<AuthResponse>.Fail(AuthError.EmailAlreadyExists);

            // Username uniqueness is global — soft-deleted users still hold their
            // username under the unique index, so bypass the soft-delete filter.
            var usernameExists = await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Username == username);
            if (usernameExists)
                return Result<AuthResponse>.Fail(AuthError.UsernameAlreadyExists);

            var passwordHash = passwordHasher.HashPassword(null!, request.Password);
            var user = await account.CreateFromPasswordAsync(username, request.Email, request.DisplayName, passwordHash);

            await db.SaveChangesAsync();

            return await sessionService.CreateSessionAsync(user, ipAddress, device);
        }

        public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request, string ipAddress, string device)
        {
            var userEmail = await db.UserEmails
                .Include(e => e.User)
                    .ThenInclude(u => u.PasswordCredential)
                .FirstOrDefaultAsync(e => e.Email == request.Email.ToLowerInvariant());

            if (userEmail is null)
                return Result<AuthResponse>.Fail(AuthError.InvalidCredentials);

            var credential = userEmail.User.PasswordCredential;
            if (credential is null)
                return Result<AuthResponse>.Fail(AuthError.InvalidCredentials);

            var user = userEmail.User;

            PasswordVerificationResult verifyResult;
            try
            {
                verifyResult = passwordHasher.VerifyHashedPassword(user, credential.PasswordHash, request.Password);
            }
            catch (FormatException)
            {
                // Stored hash is malformed (not valid Base64 / Identity v2/v3 format).
                // Treat as invalid credentials rather than crashing with 500.
                return Result<AuthResponse>.Fail(AuthError.InvalidCredentials);
            }

            if (verifyResult == PasswordVerificationResult.Failed)
                return Result<AuthResponse>.Fail(AuthError.InvalidCredentials);

            if (verifyResult == PasswordVerificationResult.SuccessRehashNeeded)
                credential.PasswordHash = passwordHasher.HashPassword(user, request.Password);

            return await sessionService.CreateSessionAsync(user, ipAddress, device);
        }

        public async Task<Result> AddPasswordAsync(Guid userId, string password)
        {
            var passwordHash = passwordHasher.HashPassword(null!, password);
            var result = await account.AddPasswordAsync(userId, passwordHash);
            if (!result.IsSuccess)
                return result;

            await db.SaveChangesAsync();
            return Result.Ok();
        }

        public async Task<Result> ChangePasswordAsync(Guid userId, Guid sessionId, string currentPassword, string newPassword)
        {
            var user = await db.Users
                .Include(u => u.PasswordCredential)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user is null)
                return Result.Fail(AuthError.UserNotFound);

            if (user.PasswordCredential is null)
                return Result.Fail(AuthError.PasswordNotSet);

            PasswordVerificationResult verifyResult;
            try
            {
                verifyResult = passwordHasher.VerifyHashedPassword(user, user.PasswordCredential.PasswordHash, currentPassword);
            }
            catch (FormatException)
            {
                return Result.Fail(AuthError.InvalidCredentials);
            }

            if (verifyResult == PasswordVerificationResult.Failed)
                return Result.Fail(AuthError.InvalidCredentials);

            var newHash = passwordHasher.HashPassword(user, newPassword);
            var setResult = await account.SetPasswordAsync(userId, newHash);
            if (!setResult.IsSuccess)
                return setResult;

            // Google-style: the change signs out every other session, keeping this one.
            await sessionService.RevokeAllSessionsAsync(userId, exceptSessionId: sessionId);

            await db.SaveChangesAsync();
            return Result.Ok();
        }
    }
}