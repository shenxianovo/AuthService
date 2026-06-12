using System.Security.Cryptography;
using System.Text;
using AuthService.Common;
using AuthService.Configuration;
using AuthService.Data;
using AuthService.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AuthService.Services
{
    public interface IPasswordResetService
    {
        /// <summary>
        /// Start the forgot-password flow. Always completes without revealing whether
        /// the email exists (anti-enumeration): unknown, unverified or rate-limited
        /// addresses are silently ignored.
        /// </summary>
        Task RequestResetAsync(string email);

        /// <summary>
        /// Consume a reset token: set the new password and revoke every session.
        /// All failure modes collapse into <see cref="AuthError.InvalidResetToken"/>
        /// so a probing client learns nothing about the token's state.
        /// </summary>
        Task<Result> ResetAsync(string token, string newPassword);
    }

    /// <summary>
    /// Unauthenticated password reset (mailbox-ownership proof). Token mechanics
    /// follow ADR-007/009: 32 random bytes in the link, SHA-256 hex in the database,
    /// single use, short TTL. The password write itself goes through
    /// <see cref="IAccountService"/> — for an OAuth-only account the reset sets a
    /// first password (the mailbox proof is equally strong there).
    /// </summary>
    public class PasswordResetService(
        AppDbContext db,
        IAccountService account,
        ISessionService sessionService,
        IEmailService emailService,
        IPasswordHasher<User> passwordHasher,
        IOptions<ResendOptions> options) : IPasswordResetService
    {
        private readonly ResendOptions _options = options.Value;

        public async Task RequestResetAsync(string email)
        {
            var normalized = email.ToLowerInvariant();
            var userEmail = await db.UserEmails
                .Include(e => e.User)
                .FirstOrDefaultAsync(e => e.Email == normalized);

            // Only verified addresses may drive an unauthenticated reset (ADR-012:
            // VerifiedAt is load-bearing). Silently ignore everything else.
            if (userEmail is null || userEmail.VerifiedAt is null)
                return;

            var oneMinuteAgo = DateTimeOffset.UtcNow.AddMinutes(-1);
            var recentReset = await db.PasswordResets
                .AnyAsync(r => r.UserId == userEmail.UserId
                            && !r.Used
                            && r.ExpiresAt > DateTimeOffset.UtcNow
                            && r.CreatedAt > oneMinuteAgo);

            // Rate-limited requests still return silently — a 429 would leak that
            // the address exists.
            if (recentReset)
                return;

            var rawToken = GenerateToken();
            db.PasswordResets.Add(new PasswordReset
            {
                TokenHash = HashToken(rawToken),
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(_options.PasswordResetExpirationMinutes),
                UserId = userEmail.UserId,
            });
            await db.SaveChangesAsync();

            var resetUrl = $"{_options.PasswordResetUrlBase}?token={rawToken}";
            await emailService.SendPasswordResetLinkAsync(userEmail.Email, userEmail.User.DisplayName, resetUrl);
        }

        public async Task<Result> ResetAsync(string token, string newPassword)
        {
            var tokenHash = HashToken(token);
            var reset = await db.PasswordResets
                .FirstOrDefaultAsync(r => r.TokenHash == tokenHash
                                       && !r.Used
                                       && r.ExpiresAt > DateTimeOffset.UtcNow);

            if (reset is null)
                return Result.Fail(AuthError.InvalidResetToken);

            // Single use, and any other outstanding links for this user die too.
            var outstanding = await db.PasswordResets
                .Where(r => r.UserId == reset.UserId && !r.Used)
                .ToListAsync();
            foreach (var r in outstanding)
                r.Used = true;

            var passwordHash = passwordHasher.HashPassword(null!, newPassword);
            var setResult = await account.SetPasswordAsync(reset.UserId, passwordHash);
            if (!setResult.IsSuccess)
                return setResult;

            // Whoever held a session before the reset (possibly the thief that made
            // the reset necessary) is signed out everywhere.
            await sessionService.RevokeAllSessionsAsync(reset.UserId);

            await db.SaveChangesAsync();
            return Result.Ok();
        }

        private static string GenerateToken()
        {
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .Replace("+", "-").Replace("/", "_").TrimEnd('=');
        }

        private static string HashToken(string token)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return Convert.ToHexString(bytes);
        }
    }
}
