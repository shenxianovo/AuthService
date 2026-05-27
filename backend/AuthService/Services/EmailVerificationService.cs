using AuthService.Common;
using AuthService.Data;
using AuthService.Entities;
using AuthService.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace AuthService.Services
{
    public class EmailVerificationService(
        AppDbContext db,
        IEmailService emailService,
        IOptions<ResendOptions> options) : IEmailVerificationService
    {
        private readonly ResendOptions _options = options.Value;

        public async Task<Result> SendVerificationCodeAsync(Guid userId, EmailTarget? target = null)
        {
            var userEmail = await ResolveEmailAsync(userId, target, includeUser: true);
            if (userEmail is null)
                return Result.Fail(AuthError.EmailNotFound);

            if (userEmail.VerifiedAt is not null)
                return Result.Fail(AuthError.EmailAlreadyVerified);

            var oneMinuteAgo = DateTimeOffset.UtcNow.AddMinutes(-1);
            var recentVerification = await db.Set<EmailVerification>()
                .AnyAsync(v => v.UserEmailId == userEmail.Id
                            && !v.Used
                            && v.ExpiresAt > DateTimeOffset.UtcNow
                            && v.CreatedAt > oneMinuteAgo);

            if (recentVerification)
                return Result.Fail(AuthError.VerificationRateLimited);

            var code = Random.Shared.Next(100000, 999999).ToString();

            db.Set<EmailVerification>().Add(new EmailVerification
            {
                Id = Guid.NewGuid(),
                TokenHash = HashCode(code),
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(_options.VerificationCodeExpirationMinutes),
                Used = false,
                UserEmailId = userEmail.Id
            });

            await db.SaveChangesAsync();
            await emailService.SendVerificationCodeAsync(userEmail.Email, userEmail.User!.DisplayName, code);
            return Result.Ok();
        }

        public async Task<Result> VerifyCodeAsync(Guid userId, string code, EmailTarget? target = null)
        {
            var userEmail = await ResolveEmailAsync(userId, target, includeUser: false);
            if (userEmail is null)
                return Result.Fail(AuthError.EmailNotFound);

            if (userEmail.VerifiedAt is not null)
                return Result.Fail(AuthError.EmailAlreadyVerified);

            var verification = await db.Set<EmailVerification>()
                .Where(v => v.UserEmailId == userEmail.Id && !v.Used && v.ExpiresAt > DateTimeOffset.UtcNow)
                .OrderByDescending(v => v.CreatedAt)
                .FirstOrDefaultAsync();

            if (verification is null || verification.TokenHash != HashCode(code))
                return Result.Fail(AuthError.InvalidVerificationCode);

            verification.Used = true;
            userEmail.VerifiedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            return Result.Ok();
        }

        private async Task<UserEmail?> ResolveEmailAsync(Guid userId, EmailTarget? target, bool includeUser)
        {
            var query = includeUser
                ? db.Set<UserEmail>().Include(e => e.User)
                : db.Set<UserEmail>().AsQueryable();

            return target switch
            {
                EmailTarget.ByIdTarget t => await query
                    .FirstOrDefaultAsync(e => e.Id == t.EmailId && e.UserId == userId),
                EmailTarget.ByAddressTarget t => await query
                    .FirstOrDefaultAsync(e => e.Email == t.Email.ToLowerInvariant() && e.UserId == userId),
                _ => await query
                    .FirstOrDefaultAsync(e => e.UserId == userId && e.IsPrimary)
            };
        }

        private static string HashCode(string code)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code));
            return Convert.ToHexString(bytes);
        }
    }
}
