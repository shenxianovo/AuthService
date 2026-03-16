using AuthService.Data;
using AuthService.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Services
{
    public interface IOAuthService
    {
        Task<User> ProcessOAuthLoginAsync(AuthProviderType provider, string providerUserId, string? email, string displayName, Guid? currentUserId = null);
    }

    public class OAuthService(AppDbContext db) : IOAuthService
    {
        public async Task<User> ProcessOAuthLoginAsync(AuthProviderType provider, string providerUserId, string? email, string displayName, Guid? currentUserId = null)
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
                    
                if (currentUserId.HasValue && user.Id != currentUserId.Value)
                    throw new InvalidOperationException("This third-party account is already bound to another user.");
            }
            else
            {
                if (currentUserId.HasValue)
                {
                    user = await db.Users.FindAsync(currentUserId.Value) 
                        ?? throw new UnauthorizedAccessException("Current user not found.");
                        
                    authProvider = new AuthProvider
                    {
                        UserId = user.Id,
                        Provider = provider,
                        ProviderUserId = providerUserId
                    };
                    db.AuthProviders.Add(authProvider);

                    if (!string.IsNullOrEmpty(email))
                    {
                        var emailExists = await db.UserEmails.AnyAsync(e => e.Email == email.ToLowerInvariant());
                        if (!emailExists)
                        {
                            db.UserEmails.Add(new UserEmail
                            {
                                UserId = user.Id,
                                Email = email.ToLowerInvariant(),
                                IsPrimary = !await db.UserEmails.AnyAsync(e => e.UserId == user.Id && e.IsPrimary)
                            });
                        }
                    }
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
            }
            
            return user;
        }
    }
}