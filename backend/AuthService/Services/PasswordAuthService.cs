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
    }

    public class PasswordAuthService(
        AppDbContext db,
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

            var usernameExists = await db.Users.AnyAsync(u => u.Username == username);
            if (usernameExists)
                return Result<AuthResponse>.Fail(AuthError.UsernameAlreadyExists);

            var user = new User
            {
                Username = username,
                DisplayName = request.DisplayName,
            };

            db.Users.Add(user);
            db.UserEmails.Add(new UserEmail
            {
                Email = request.Email.ToLowerInvariant(),
                IsPrimary = true,
                UserId = user.Id,
            });
            db.PasswordCredentials.Add(new PasswordCredential
            {
                UserId = user.Id,
                PasswordHash = passwordHasher.HashPassword(user, request.Password),
            });
            db.AuthProviders.Add(new AuthProvider
            {
                Provider = AuthProviderType.Password,
                ProviderUserId = user.Id.ToString(),
                UserId = user.Id,
            });

            await db.SaveChangesAsync();

            return await sessionService.CreateSessionAsync(user, ipAddress, device);
        }

        public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request, string ipAddress, string device)
        {
            var userEmail = await db.UserEmails
                .Include(e => e.User)
                    .ThenInclude(u => u.PasswordCredential)
                .FirstOrDefaultAsync(e => e.Email == request.Email.ToLowerInvariant());

            if (userEmail is null || userEmail.User.IsDeleted)
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
            var user = await db.Users
                .Include(u => u.PasswordCredential)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user is null)
                return Result.Fail(AuthError.UserNotFound);

            if (user.PasswordCredential is not null)
                return Result.Fail(AuthError.PasswordAlreadySet);

            db.PasswordCredentials.Add(new PasswordCredential
            {
                UserId = userId,
                PasswordHash = passwordHasher.HashPassword(user, password)
            });
            db.AuthProviders.Add(new AuthProvider
            {
                Provider = AuthProviderType.Password,
                ProviderUserId = userId.ToString(),
                UserId = userId
            });

            await db.SaveChangesAsync();
            return Result.Ok();
        }
    }
}