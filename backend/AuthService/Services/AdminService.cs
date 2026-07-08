using AuthService.Common;
using AuthService.Data;
using AuthService.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Services
{
    public interface IAdminService
    {
        Task<Result> SetRoleAsync(Guid targetUserId, UserRole role);
    }

    public class AdminService(AppDbContext db) : IAdminService
    {
        public async Task<Result> SetRoleAsync(Guid targetUserId, UserRole role)
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == targetUserId);
            if (user is null)
                return Result.Fail(AuthError.UserNotFound);

            if (user.Role == role)
                return Result.Ok();

            // Demoting the only admin would lock the admin surface entirely
            // (recoverable only via the bootstrap config + restart) — refuse.
            if (user.Role == UserRole.Admin && role != UserRole.Admin)
            {
                var adminCount = await db.Users.CountAsync(u => u.Role == UserRole.Admin);
                if (adminCount <= 1)
                    return Result.Fail(AuthError.CannotDemoteLastAdmin);
            }

            user.Role = role;
            user.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            return Result.Ok();
        }
    }
}
