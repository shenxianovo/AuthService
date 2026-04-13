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
                {
                    // Merge: move everything from the OAuth user to the current user
                    var currentUser = await db.Users.FindAsync(currentUserId.Value)
                        ?? throw new UnauthorizedAccessException("Current user not found.");

                    await MergeUserAsync(sourceUser: user, targetUser: currentUser);
                    user = currentUser;
                }
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
                        var existingEmail = await db.UserEmails.FirstOrDefaultAsync(e => e.Email == email.ToLowerInvariant());
                        if (existingEmail == null)
                        {
                            db.UserEmails.Add(new UserEmail
                            {
                                UserId = user.Id,
                                Email = email.ToLowerInvariant(),
                                IsPrimary = !await db.UserEmails.AnyAsync(e => e.UserId == user.Id && e.IsPrimary)
                            });
                        }
                        else if (existingEmail.UserId != user.Id)
                        {
                            // Email belongs to another user — merge that user into current
                            var otherUser = await db.Users.FindAsync(existingEmail.UserId);
                            if (otherUser != null && !otherUser.IsDeleted)
                            {
                                await MergeUserAsync(sourceUser: otherUser, targetUser: user);
                            }
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
            
            await db.SaveChangesAsync();
            return user;
        }

        /// <summary>
        /// Merge all data from sourceUser into targetUser, then soft-delete sourceUser.
        /// </summary>
        private async Task MergeUserAsync(User sourceUser, User targetUser)
        {
            // Revoke all sessions of source user (invalidates their refresh tokens)
            var sourceSessions = await db.Sessions
                .Where(s => s.UserId == sourceUser.Id && !s.Revoked)
                .ToListAsync();
            foreach (var s in sourceSessions)
                s.Revoked = true;

            // Move AuthProviders
            var providers = await db.AuthProviders.Where(p => p.UserId == sourceUser.Id).ToListAsync();
            foreach (var p in providers)
                p.UserId = targetUser.Id;

            // Move Emails (skip duplicates)
            var targetEmails = await db.UserEmails.Where(e => e.UserId == targetUser.Id).Select(e => e.Email).ToListAsync();
            var sourceEmails = await db.UserEmails.Where(e => e.UserId == sourceUser.Id).ToListAsync();
            foreach (var e in sourceEmails)
            {
                if (targetEmails.Contains(e.Email))
                    db.UserEmails.Remove(e);
                else
                {
                    e.UserId = targetUser.Id;
                    e.IsPrimary = false; // target keeps its own primary
                }
            }

            // Move Sessions
            var sessions = await db.Sessions.Where(s => s.UserId == sourceUser.Id).ToListAsync();
            foreach (var s in sessions)
                s.UserId = targetUser.Id;

            // Move PasswordCredential (only if target doesn't have one)
            // UserId is the PK of PasswordCredential (1:1), so we can't just update it.
            // We must delete the source and create a new one for the target.
            var sourcePassword = await db.PasswordCredentials.FindAsync(sourceUser.Id);
            if (sourcePassword != null)
            {
                var targetHasPassword = await db.PasswordCredentials.AnyAsync(p => p.UserId == targetUser.Id);
                if (!targetHasPassword)
                {
                    var newCredential = new PasswordCredential
                    {
                        UserId = targetUser.Id,
                        PasswordHash = sourcePassword.PasswordHash
                    };
                    db.PasswordCredentials.Remove(sourcePassword);
                    db.PasswordCredentials.Add(newCredential);
                }
                else
                {
                    db.PasswordCredentials.Remove(sourcePassword);
                }
            }

            // Soft-delete source user
            sourceUser.IsDeleted = true;
            sourceUser.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }
}