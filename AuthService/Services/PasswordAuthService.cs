using AuthService.Data;
using AuthService.DTOs.Auth;
using AuthService.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Services
{
    public interface IPasswordAuthService
    {
        Task<AuthResponse> RegisterAsync(RegisterRequest request, string ipAddress, string device);
        Task<AuthResponse> LoginAsync(LoginRequest request, string ipAddress, string device);
        Task AddPasswordAsync(Guid userId, string password);
    }

    public class PasswordAuthService(
        AppDbContext db,
        ISessionService sessionService,
        IPasswordHasher<User> passwordHasher) : IPasswordAuthService
    {
        public async Task<AuthResponse> RegisterAsync(RegisterRequest request, string ipAddress, string device)
        {
            // Check if email already exists
            var emailExists = await db.UserEmails
                .AnyAsync(e => e.Email == request.Email.ToLowerInvariant());
            if (emailExists)
                throw new InvalidOperationException("Email already registered.");

            var user = new User
            {
                DisplayName = request.DisplayName,
            };

            var userEmail = new UserEmail
            {
                Email = request.Email.ToLowerInvariant(),
                IsPrimary = true,
                UserId = user.Id,
            };

            var passwordCredential = new PasswordCredential
            {
                UserId = user.Id,
                PasswordHash = passwordHasher.HashPassword(user, request.Password),
            };

            var authProvider = new AuthProvider
            {
                Provider = AuthProviderType.Password,
                ProviderUserId = user.Id.ToString(),
                UserId = user.Id,
            };

            db.Users.Add(user);
            db.UserEmails.Add(userEmail);
            db.PasswordCredentials.Add(passwordCredential);
            db.AuthProviders.Add(authProvider);

            return await sessionService.CreateSessionAsync(user.Id, ipAddress, device);
        }

        public async Task<AuthResponse> LoginAsync(LoginRequest request, string ipAddress, string device)
        {
            var userEmail = await db.UserEmails
                .Include(e => e.User)
                    .ThenInclude(u => u.PasswordCredential)
                .FirstOrDefaultAsync(e => e.Email == request.Email.ToLowerInvariant());

            if (userEmail is null || userEmail.User.IsDeleted)
                throw new UnauthorizedAccessException("Invalid email or password.");

            var credential = userEmail.User.PasswordCredential;
            if (credential is null)
                throw new UnauthorizedAccessException("Invalid email or password.");

            var user = userEmail.User;
            var verifyResult = passwordHasher.VerifyHashedPassword(user, credential.PasswordHash, request.Password);

            if (verifyResult == PasswordVerificationResult.Failed)
                throw new UnauthorizedAccessException("Invalid email or password.");

            // Auto-rehash if the hasher indicates the hash needs upgrading
            if (verifyResult == PasswordVerificationResult.SuccessRehashNeeded)
            {
                credential.PasswordHash = passwordHasher.HashPassword(user, request.Password);
            }

            return await sessionService.CreateSessionAsync(user.Id, ipAddress, device);
        }

        public async Task AddPasswordAsync(Guid userId, string password)
        {
            var user = await db.Users
                .Include(u => u.PasswordCredential)
                .FirstOrDefaultAsync(u => u.Id == userId);
                
            if (user is null)
                throw new InvalidOperationException("User not found.");
                
            if (user.PasswordCredential is not null)
                throw new InvalidOperationException("User already has a password.");
                
            var credential = new PasswordCredential
            {
                UserId = userId,
                PasswordHash = passwordHasher.HashPassword(user, password)
            };
            
            db.PasswordCredentials.Add(credential);
            
            var authProvider = new AuthProvider
            {
                Provider = AuthProviderType.Password,
                ProviderUserId = userId.ToString(),
                UserId = userId
            };
            db.AuthProviders.Add(authProvider);
            
            await db.SaveChangesAsync();
        }
    }
}
