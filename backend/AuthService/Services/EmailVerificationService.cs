using AuthService.Data;
using AuthService.Entities;
using AuthService.Exceptions;
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

        public async Task SendVerificationCodeAsync(Guid userId)
        {
            var userEmail = await db.Set<UserEmail>()
                .Include(e => e.User)
                .FirstOrDefaultAsync(e => e.UserId == userId && e.IsPrimary)
                ?? throw new BusinessException("没有找到主邮箱。");

            if (userEmail.VerifiedAt is not null)
                throw new BusinessException("邮箱已验证。");

            // Rate limit: only one send per minute
            var oneMinuteAgo = DateTimeOffset.UtcNow.AddMinutes(-1);
            var recentVerification = await db.Set<EmailVerification>()
                .AnyAsync(v => v.UserEmailId == userEmail.Id
                            && !v.Used
                            && v.ExpiresAt > DateTimeOffset.UtcNow
                            && v.CreatedAt > oneMinuteAgo);

            if (recentVerification)
                throw new BusinessException("请稍后再试。");

            var code = Random.Shared.Next(100000, 999999).ToString();
            var tokenHash = HashCode(code);

            var verification = new EmailVerification
            {
                Id = Guid.NewGuid(),
                TokenHash = tokenHash,
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(_options.VerificationCodeExpirationMinutes),
                Used = false,
                UserEmailId = userEmail.Id
            };

            db.Set<EmailVerification>().Add(verification);
            await db.SaveChangesAsync();

            await emailService.SendVerificationCodeAsync(
                userEmail.Email,
                userEmail.User!.DisplayName,
                code);
        }

        public async Task VerifyCodeAsync(Guid userId, string code)
        {
            var userEmail = await db.Set<UserEmail>()
                .FirstOrDefaultAsync(e => e.UserId == userId && e.IsPrimary)
                ?? throw new BusinessException("没有找到主邮箱。");

            if (userEmail.VerifiedAt is not null)
                throw new BusinessException("邮箱已验证。");

            var tokenHash = HashCode(code);

            var verification = await db.Set<EmailVerification>()
                .Where(v => v.UserEmailId == userEmail.Id
                         && !v.Used
                         && v.ExpiresAt > DateTimeOffset.UtcNow)
                .OrderByDescending(v => v.CreatedAt)
                .FirstOrDefaultAsync();

            if (verification is null || verification.TokenHash != tokenHash)
                throw new BusinessException("验证码错误。");

            verification.Used = true;
            userEmail.VerifiedAt = DateTimeOffset.UtcNow;

            await db.SaveChangesAsync();
        }

        private static string HashCode(string code)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code));
            return Convert.ToHexString(bytes);
        }
    }
}
