using AuthService.Common;
using AuthService.Data;
using AuthService.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Services
{
    public class AccountService(AppDbContext db) : IAccountService
    {
        public async Task<User> CreateFromOAuthAsync(
            AuthProviderType provider,
            string providerUserId,
            string? email,
            string displayName,
            string? providerLogin,
            bool emailVerified = false)
        {
            var username = await UsernameGenerator.GenerateUniqueAsync(
                providerLogin,
                email,
                u => db.Users.AnyAsync(x => x.Username == u));

            var user = new User
            {
                Username = username,
                DisplayName = displayName,
            };
            db.Users.Add(user);

            if (!string.IsNullOrEmpty(email))
            {
                db.UserEmails.Add(new UserEmail
                {
                    UserId = user.Id,
                    Email = email.ToLowerInvariant(),
                    IsPrimary = true,
                    VerifiedAt = emailVerified ? DateTimeOffset.UtcNow : null
                });
            }

            db.AuthProviders.Add(new AuthProvider
            {
                UserId = user.Id,
                Provider = provider,
                ProviderUserId = providerUserId
            });

            return user;
        }

        public Task<User> CreateFromPasswordAsync(
            string username,
            string email,
            string displayName,
            string passwordHash)
        {
            var user = new User
            {
                Username = username.ToLowerInvariant(),
                DisplayName = displayName,
            };

            db.Users.Add(user);
            db.UserEmails.Add(new UserEmail
            {
                Email = email.ToLowerInvariant(),
                IsPrimary = true,
                UserId = user.Id,
            });
            db.PasswordCredentials.Add(new PasswordCredential
            {
                UserId = user.Id,
                PasswordHash = passwordHash,
            });
            db.AuthProviders.Add(new AuthProvider
            {
                Provider = AuthProviderType.Password,
                ProviderUserId = user.Id.ToString(),
                UserId = user.Id,
            });

            return Task.FromResult(user);
        }

        public async Task<Result<User>> AddProviderAsync(
            Guid userId,
            AuthProviderType provider,
            string providerUserId,
            string? email,
            bool emailVerified = false)
        {
            var user = await db.Users.FindAsync(userId);
            if (user is null)
                return Result<User>.Fail(AuthError.UserNotFoundForMerge);

            db.AuthProviders.Add(new AuthProvider
            {
                UserId = user.Id,
                Provider = provider,
                ProviderUserId = providerUserId
            });

            if (!string.IsNullOrEmpty(email))
            {
                var normalized = email.ToLowerInvariant();
                var existingEmail = await db.UserEmails.FirstOrDefaultAsync(e => e.Email == normalized);
                if (existingEmail is null)
                {
                    db.UserEmails.Add(new UserEmail
                    {
                        UserId = user.Id,
                        Email = normalized,
                        IsPrimary = !await db.UserEmails.AnyAsync(e => e.UserId == user.Id && e.IsPrimary),
                        VerifiedAt = emailVerified ? DateTimeOffset.UtcNow : null
                    });
                }
                else if (existingEmail.UserId == user.Id && emailVerified)
                {
                    existingEmail.VerifiedAt ??= DateTimeOffset.UtcNow;
                }
                // If the email belongs to another user, the caller decides to merge;
                // we leave the email untouched here.
            }

            return Result<User>.Ok(user);
        }

        public async Task<Result> AddPasswordAsync(Guid userId, string passwordHash)
        {
            var user = await db.Users
                .Include(u => u.PasswordCredential)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user is null)
                return Result.Fail(AuthError.UserNotFound);

            if (user.PasswordCredential is not null)
                return Result.Fail(AuthError.PasswordAlreadySet);

            db.PasswordCredentials.Add(new PasswordCredential
            {
                UserId = userId,
                PasswordHash = passwordHash
            });
            db.AuthProviders.Add(new AuthProvider
            {
                Provider = AuthProviderType.Password,
                ProviderUserId = userId.ToString(),
                UserId = userId
            });

            return Result.Ok();
        }

        public async Task MergeAsync(Guid sourceUserId, Guid targetUserId)
        {
            var sourceUser = await db.Users.FindAsync(sourceUserId);
            if (sourceUser is null)
                return;

            // Revoke all active sessions of the source user
            var sourceSessions = await db.Sessions
                .Where(s => s.UserId == sourceUserId)
                .ToListAsync();
            foreach (var s in sourceSessions.Where(s => !s.Revoked))
                s.Revoked = true;

            // Move AuthProviders
            var providers = await db.AuthProviders.Where(p => p.UserId == sourceUserId).ToListAsync();
            foreach (var p in providers)
                p.UserId = targetUserId;

            // Move Emails. UserEmail.Email is globally unique (see ADR-011), so the
            // source and target can never hold the same address — no dedup is possible
            // here. Each email simply reassigns to the target, losing primary status.
            var sourceEmails = await db.UserEmails.Where(e => e.UserId == sourceUserId).ToListAsync();
            foreach (var e in sourceEmails)
            {
                e.UserId = targetUserId;
                e.IsPrimary = false; // target keeps its own primary
            }

            // Move Sessions (now belong to target so refresh tokens keep working)
            foreach (var s in sourceSessions)
                s.UserId = targetUserId;

            // Move ApiKeys
            var apiKeys = await db.ApiKeys.Where(k => k.UserId == sourceUserId).ToListAsync();
            foreach (var k in apiKeys)
                k.UserId = targetUserId;

            // Delete pending password resets — reset links issued for the merged-away
            // account must die with it (same spirit as revoking its sessions).
            var passwordResets = await db.PasswordResets.Where(r => r.UserId == sourceUserId).ToListAsync();
            db.PasswordResets.RemoveRange(passwordResets);

            // Move PasswordCredential (only if target doesn't have one).
            // UserId is the PK (1:1), so we delete the source and recreate for target.
            var sourcePassword = await db.PasswordCredentials.FindAsync(sourceUserId);
            if (sourcePassword != null)
            {
                var targetHasPassword = await db.PasswordCredentials.AnyAsync(p => p.UserId == targetUserId);
                if (!targetHasPassword)
                {
                    db.PasswordCredentials.Add(new PasswordCredential
                    {
                        UserId = targetUserId,
                        PasswordHash = sourcePassword.PasswordHash
                    });
                }
                db.PasswordCredentials.Remove(sourcePassword);
            }

            // Soft-delete source user
            sourceUser.IsDeleted = true;
            sourceUser.UpdatedAt = DateTimeOffset.UtcNow;
        }

        public async Task<Result> UnlinkProviderAsync(Guid userId, AuthProviderType provider)
        {
            var authProvider = await db.AuthProviders
                .FirstOrDefaultAsync(a => a.UserId == userId && a.Provider == provider);

            if (authProvider is null)
                return Result.Fail(AuthError.ProviderNotLinked);

            var hasPassword = await db.PasswordCredentials.AnyAsync(p => p.UserId == userId);
            var providerCount = await db.AuthProviders
                .CountAsync(a => a.UserId == userId && a.Provider != AuthProviderType.Password);

            if (!hasPassword && providerCount <= 1)
                return Result.Fail(AuthError.CannotUnlinkLastLoginMethod);

            db.AuthProviders.Remove(authProvider);
            return Result.Ok();
        }
    }
}
