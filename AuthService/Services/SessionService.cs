using AuthService.Data;
using AuthService.DTOs.Auth;
using AuthService.Entities;
using AuthService.Configuration;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace AuthService.Services
{
    public interface ISessionService
    {
        Task<AuthResponse> CreateSessionAsync(Guid userId, string ipAddress, string device);
    }

    public class SessionService(AppDbContext db, IJwtService jwtService, IOptions<JwtOptions> jwtOptions) : ISessionService
    {
        private readonly JwtOptions _jwtOptions = jwtOptions.Value;

        public async Task<AuthResponse> CreateSessionAsync(
            Guid userId,
            string ipAddress,
            string device)
        {
            var session = new Session
            {
                UserId = userId,
                Device = device,
                IpAddress = ipAddress,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(_jwtOptions.SessionExpirationDays),
            };

            var rawToken = GenerateRefreshToken();

            var refreshToken = new RefreshToken
            {
                SessionId = session.Id,
                TokenHash = HashToken(rawToken),
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(_jwtOptions.RefreshTokenExpirationDays),
            };

            db.Sessions.Add(session);
            db.RefreshTokens.Add(refreshToken);

            await db.SaveChangesAsync();

            var accessToken = jwtService.GenerateAccessToken(userId, session.Id);

            return new AuthResponse
            {
                UserId = userId,
                AccessToken = accessToken,
                RefreshToken = rawToken,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(_jwtOptions.AccessTokenExpirationMinutes),
            };
        }
        private static string GenerateRefreshToken()
        {
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        }

        private static string HashToken(string token)
        {
            var bytes = Convert.FromBase64String(token);
            var hash = SHA256.HashData(bytes);
            return Convert.ToBase64String(hash);
        }
    }
}