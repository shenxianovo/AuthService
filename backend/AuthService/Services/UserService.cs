using AuthService.Common;
using AuthService.Data;
using AuthService.DTOs.Auth;
using AuthService.DTOs.User;
using AuthService.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Services
{
    public interface IUserService
    {
        Task<UserInfoResponse?> GetUserInfoAsync(Guid userId);
        Task<PublicUserResponse?> GetPublicUserByUsernameAsync(string username);
    }

    public class UserService(AppDbContext db) : IUserService
    {
        public async Task<UserInfoResponse?> GetUserInfoAsync(Guid userId)
        {
            var user = await db.Users
                .Include(u => u.Emails)
                .Include(u => u.Providers)
                .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);

            if (user is null)
                return null;

            var hasPassword = await db.PasswordCredentials.AnyAsync(p => p.UserId == userId);

            return new UserInfoResponse
            {
                UserId = user.Id,
                DisplayName = user.DisplayName,
                CreatedAt = user.CreatedAt,
                HasPassword = hasPassword,
                Emails = user.Emails.Select(e => new EmailInfo
                {
                    Email = e.Email,
                    IsPrimary = e.IsPrimary,
                    IsVerified = e.VerifiedAt.HasValue
                }).ToList(),
                Providers = user.Providers
                    .Where(p => p.Provider != AuthProviderType.Password)
                    .Select(p => new ProviderInfo
                    {
                        Provider = p.Provider.ToString(),
                        LinkedAt = p.CreatedAt
                    }).ToList()
            };
        }

        public async Task<PublicUserResponse?> GetPublicUserByUsernameAsync(string username)
        {
            var normalized = username.ToLowerInvariant();
            var user = await db.Users
                .Where(u => !u.IsDeleted && u.Username == normalized)
                .Select(u => new PublicUserResponse
                {
                    Id = u.Id,
                    Username = u.Username,
                    DisplayName = u.DisplayName,
                })
                .FirstOrDefaultAsync();

            return user;
        }
    }
}