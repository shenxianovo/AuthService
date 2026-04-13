using AuthService.Common;
using AuthService.Data;
using AuthService.DTOs.Auth;
using AuthService.Entities;
using AuthService.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace AuthService.Services
{
    public interface ISessionService
    {
        Task<AuthResponse> CreateSessionAsync(Guid userId, string ipAddress, string device);
        Task<Result<AuthResponse>> RefreshSessionAsync(string refreshToken);
        Task RevokeSessionAsync(Guid sessionId);
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

        public async Task<Result<AuthResponse>> RefreshSessionAsync(string rawRefreshToken)
        {
            var tokenHash = HashToken(rawRefreshToken);

            var existing = await db.RefreshTokens
                .Include(rt => rt.Session)
                    .ThenInclude(s => s.User)
                .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash);

            if (existing == null
                || existing.Revoked
                || existing.ExpiresAt <= DateTimeOffset.UtcNow
                || existing.Session.Revoked
                || existing.Session.ExpiresAt <= DateTimeOffset.UtcNow
                || existing.Session.User.IsDeleted)
            {
                return Result<AuthResponse>.Fail(AuthError.InvalidRefreshToken);
            }

            // Rotate: revoke the old token and issue a new one
            existing.Revoked = true;

            var newRawToken = GenerateRefreshToken();
            var newRefreshToken = new RefreshToken
            {
                SessionId = existing.SessionId,
                TokenHash = HashToken(newRawToken),
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(_jwtOptions.RefreshTokenExpirationDays),
            };
            db.RefreshTokens.Add(newRefreshToken);

            await db.SaveChangesAsync();

            var session = existing.Session;
            var accessToken = jwtService.GenerateAccessToken(session.UserId, session.Id);

            return Result<AuthResponse>.Ok(new AuthResponse
            {
                UserId = session.UserId,
                AccessToken = accessToken,
                RefreshToken = newRawToken,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(_jwtOptions.AccessTokenExpirationMinutes),
            });
        }

        public async Task RevokeSessionAsync(Guid sessionId)
        {
            var session = await db.Sessions
                .Include(s => s.RefreshTokens)
                .FirstOrDefaultAsync(s => s.Id == sessionId);

            if (session == null || session.Revoked)
                return;

            session.Revoked = true;
            foreach (var rt in session.RefreshTokens.Where(rt => !rt.Revoked))
                rt.Revoked = true;

            await db.SaveChangesAsync();
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
