using AuthService.Common;
using AuthService.Data;
using AuthService.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Services
{
    public class EmailManagementService(
        AppDbContext db,
        IEmailVerificationService emailVerificationService) : IEmailManagementService
    {
        public async Task<Result> AddEmailAsync(Guid userId, string email)
        {
            var normalizedEmail = email.Trim().ToLower();

            var exists = await db.Set<UserEmail>()
                .AnyAsync(e => e.Email == normalizedEmail);

            if (exists)
                return Result.Fail(AuthError.EmailAlreadyExists);

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

            var sendResult = await emailVerificationService.SendVerificationCodeAsync(userId, EmailTarget.ById(userEmail.Id));
            if (!sendResult.IsSuccess)
                return sendResult;

            return Result.Ok();
        }

        public async Task<Result> RemoveEmailAsync(Guid userId, string email)
        {
            var normalizedEmail = email.Trim().ToLower();

            var userEmail = await db.Set<UserEmail>()
                .FirstOrDefaultAsync(e => e.Email == normalizedEmail && e.UserId == userId);

            if (userEmail is null)
                return Result.Fail(AuthError.EmailNotFound);

            if (userEmail.IsPrimary)
                return Result.Fail(AuthError.CannotRemovePrimaryEmail);

            db.Set<UserEmail>().Remove(userEmail);
            await db.SaveChangesAsync();
            return Result.Ok();
        }

        public async Task<Result> SetPrimaryEmailAsync(Guid userId, string email)
        {
            var normalizedEmail = email.Trim().ToLower();

            var userEmail = await db.Set<UserEmail>()
                .FirstOrDefaultAsync(e => e.Email == normalizedEmail && e.UserId == userId);

            if (userEmail is null)
                return Result.Fail(AuthError.EmailNotFound);

            if (userEmail.VerifiedAt == null)
                return Result.Fail(AuthError.EmailNotVerified);

            var currentPrimary = await db.Set<UserEmail>()
                .FirstOrDefaultAsync(e => e.UserId == userId && e.IsPrimary);

            if (currentPrimary is not null)
                currentPrimary.IsPrimary = false;

            userEmail.IsPrimary = true;
            await db.SaveChangesAsync();
            return Result.Ok();
        }
    }
}
