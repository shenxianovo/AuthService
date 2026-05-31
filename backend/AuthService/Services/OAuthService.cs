using AuthService.Common;
using AuthService.Data;
using AuthService.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Services
{
    public interface IOAuthService
    {
        Task<Result<User>> ProcessOAuthLoginAsync(AuthProviderType provider, string providerUserId, string? email, string displayName, Guid? currentUserId = null, string? providerLogin = null, bool emailVerified = false);
    }

    /// <summary>
    /// Resolves an OAuth provider callback to a user account. This is the decision
    /// layer: it queries to determine which account the login maps to and what
    /// action is needed (login / link / merge / create), then delegates the writes
    /// to <see cref="IAccountService"/> and commits once.
    /// </summary>
    public class OAuthService(AppDbContext db, IAccountService account) : IOAuthService
    {
        public async Task<Result<User>> ProcessOAuthLoginAsync(
            AuthProviderType provider,
            string providerUserId,
            string? email,
            string displayName,
            Guid? currentUserId = null,
            string? providerLogin = null,
            bool emailVerified = false)
        {
            var resolution = await ResolveAsync(provider, providerUserId, email, displayName, currentUserId, providerLogin, emailVerified);
            if (!resolution.IsSuccess)
                return resolution;

            await db.SaveChangesAsync();
            return resolution;
        }
        // APPEND_MARKER

        /// <summary>
        /// Pure decision: resolve which account this OAuth login maps to and apply
        /// the corresponding account write (login / link / merge / create) via
        /// IAccountService. Does not commit — the caller does.
        /// </summary>
        private async Task<Result<User>> ResolveAsync(
            AuthProviderType provider,
            string providerUserId,
            string? email,
            string displayName,
            Guid? currentUserId,
            string? providerLogin,
            bool emailVerified)
        {
            var existingLink = await db.AuthProviders
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.Provider == provider && a.ProviderUserId == providerUserId);

            // Case 1: provider already linked to a user.
            if (existingLink is not null)
            {
                var linkedUser = existingLink.User;
                if (linkedUser.IsDeleted)
                    return Result<User>.Fail(AuthError.UserDeleted);

                // Binding flow: the provider belongs to a different user than the one
                // currently logged in — merge the linked user into the current user.
                if (currentUserId.HasValue && linkedUser.Id != currentUserId.Value)
                {
                    var currentUser = await db.Users.FindAsync(currentUserId.Value);
                    if (currentUser is null)
                        return Result<User>.Fail(AuthError.UserNotFoundForMerge);

                    await account.MergeAsync(sourceUserId: linkedUser.Id, targetUserId: currentUser.Id);
                    return Result<User>.Ok(currentUser);
                }

                return Result<User>.Ok(linkedUser);
            }

            // Case 2: provider not linked, binding flow — link it to the current user.
            if (currentUserId.HasValue)
            {
                var linkResult = await account.AddProviderAsync(currentUserId.Value, provider, providerUserId, email, emailVerified);
                if (!linkResult.IsSuccess)
                    return linkResult;

                // If the email belongs to another (live) user, merge them in.
                if (!string.IsNullOrEmpty(email))
                {
                    var normalized = email.ToLowerInvariant();
                    var emailOwner = await db.UserEmails
                        .Include(e => e.User)
                        .FirstOrDefaultAsync(e => e.Email == normalized);

                    if (emailOwner is not null
                        && emailOwner.UserId != currentUserId.Value
                        && !emailOwner.User.IsDeleted)
                    {
                        await account.MergeAsync(sourceUserId: emailOwner.UserId, targetUserId: currentUserId.Value);
                    }
                }

                return linkResult;
            }

            // Case 3: not binding — does the email match an existing user? Link to them.
            if (!string.IsNullOrEmpty(email))
            {
                var emailOwner = await db.UserEmails
                    .Include(e => e.User)
                    .FirstOrDefaultAsync(e => e.Email == email.ToLowerInvariant());

                if (emailOwner is not null)
                {
                    if (emailOwner.User.IsDeleted)
                        return Result<User>.Fail(AuthError.UserDeleted);

                    // Email already belongs to this user — link the provider and, if the
                    // provider asserts the email is verified, let AddProviderAsync upgrade
                    // the existing row's VerifiedAt (it won't insert a duplicate).
                    return await account.AddProviderAsync(emailOwner.UserId, provider, providerUserId, email, emailVerified);
                }
            }

            // Case 4: brand new user.
            var created = await account.CreateFromOAuthAsync(provider, providerUserId, email, displayName, providerLogin, emailVerified);
            return Result<User>.Ok(created);
        }
    }
}
