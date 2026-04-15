using AuthService.Data;
using AuthService.Entities;
using AuthService.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Services
{
    public class EmailManagementService(
        AppDbContext db,
        IEmailVerificationService emailVerificationService) : IEmailManagementService
    {
        public async Task AddEmailAsync(Guid userId, string email)
        {
            var normalizedEmail = email.Trim().ToLower();

            var exists = await db.Set<UserEmail>()
                .AnyAsync(e => e.Email == normalizedEmail);

            if (exists)
                throw new ConflictException("该邮箱已被使用。");

            var userEmail = new UserEmail
            {
                Id = Guid.NewGuid(),
                Email = normalizedEmail,
                IsPrimary = false,
                VerifiedAt = null,
                UserId = userId
            };

            db.Set<UserEmail>().Add(userEmail);
            await db.SaveChangesAsync();

            await emailVerificationService.SendVerificationCodeAsync(userId, EmailTarget.ById(userEmail.Id));
        }

        public async Task RemoveEmailAsync(Guid userId, string email)
        {
            var normalizedEmail = email.Trim().ToLower();

            var userEmail = await db.Set<UserEmail>()
                .FirstOrDefaultAsync(e => e.Email == normalizedEmail && e.UserId == userId)
                ?? throw new BusinessException("邮箱不存在。");

            if (userEmail.IsPrimary)
                throw new BusinessException("不能删除主邮箱，请先设置其他邮箱为主邮箱。");

            db.Set<UserEmail>().Remove(userEmail);
            await db.SaveChangesAsync();
        }

        public async Task SetPrimaryEmailAsync(Guid userId, string email)
        {
            var normalizedEmail = email.Trim().ToLower();

            var userEmail = await db.Set<UserEmail>()
                .FirstOrDefaultAsync(e => e.Email == normalizedEmail && e.UserId == userId)
                ?? throw new BusinessException("邮箱不存在。");

            if (userEmail.VerifiedAt == null)
                throw new BusinessException("只能将已验证的邮箱设为主邮箱。");

            var currentPrimary = await db.Set<UserEmail>()
                .FirstOrDefaultAsync(e => e.UserId == userId && e.IsPrimary);

            if (currentPrimary is not null)
                currentPrimary.IsPrimary = false;

            userEmail.IsPrimary = true;
            await db.SaveChangesAsync();
        }
    }
}
