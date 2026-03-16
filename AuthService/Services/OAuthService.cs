using AuthService.Data;
using AuthService.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Services
{
    public interface IOAuthService
    {
        Task<User> ProcessOAuthLoginAsync(AuthProviderType provider, string providerUserId, string? email, string displayName);
    }

    public class OAuthService(AppDbContext db) : IOAuthService
    {
        public async Task<User> ProcessOAuthLoginAsync(AuthProviderType provider, string providerUserId, string? email, string displayName)
        {
            var authProvider = await db.AuthProviders
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.Provider == provider && a.ProviderUserId == providerUserId);

            User user;

            if (authProvider is not null)
            {
                user = authProvider.User;
                if (user.IsDeleted)
                    throw new UnauthorizedAccessException("User is deleted.");
            }
            else
            {
                UserEmail? userEmail = null;
                if (!string.IsNullOrEmpty(email))
                {
                    userEmail = await db.UserEmails
                        .Include(e => e.User)
                        .FirstOrDefaultAsync(e => e.Email == email.ToLowerInvariant());
                }

                if (userEmail is not null)
                {
                    user = userEmail.User;
                    if (user.IsDeleted)
                        throw new UnauthorizedAccessException("User is deleted.");
                    
                    authProvider = new AuthProvider
                    {
                        UserId = user.Id,
                        Provider = provider,
                        ProviderUserId = providerUserId
                    };
                    db.AuthProviders.Add(authProvider);
                }
                else
                {
                    user = new User
                    {
                        DisplayName = displayName,
                    };
                    db.Users.Add(user);

                    if (!string.IsNullOrEmpty(email))
                    {
                        db.UserEmails.Add(new UserEmail
                        {
                            UserId = user.Id,
                            Email = email.ToLowerInvariant(),
                            IsPrimary = true
                        });
                    }

                    authProvider = new AuthProvider
                    {
                        UserId = user.Id,
                        Provider = provider,
                        ProviderUserId = providerUserId
                    };
                    db.AuthProviders.Add(authProvider);
                }
            }
            
            return user;
        }
    }
}