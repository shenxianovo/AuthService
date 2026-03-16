using System.Security.Cryptography;
using AuthService.Data;
using AuthService.DTOs.Auth;
using AuthService.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AuthService.Services
{
    public interface IPasswordAuthService
    {
        Task<AuthResponse> RegisterAsync(RegisterRequest request, string ipAddress, string device);
        Task<AuthResponse> LoginAsync(LoginRequest request, string ipAddress, string device);
    }

    public class PasswordAuthService(
        AppDbContext db,
        SessionService sessionService) : IPasswordAuthService
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
                PasswordHash = HashPassword(request.Password),
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

            if (!VerifyPassword(request.Password, credential.PasswordHash))
                throw new UnauthorizedAccessException("Invalid email or password.");

            var user = userEmail.User;

            return await sessionService.CreateSessionAsync(user.Id, ipAddress, device);
        }

        // --- Password hashing (BCrypt-like using PBKDF2) ---

        private static string HashPassword(string password)
        {
            var salt = RandomNumberGenerator.GetBytes(16);
            var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
            return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
        }

        private static bool VerifyPassword(string password, string storedHash)
        {
            var parts = storedHash.Split('.');
            if (parts.Length != 2) return false;

            var salt = Convert.FromBase64String(parts[0]);
            var expectedHash = Convert.FromBase64String(parts[1]);
            var actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);

            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
    }
}
