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
    /// layer: it gathers the facts the resolution depends on, lets the pure decision
    /// tree (<see cref="OAuthResolver"/>) pick an action, then applies it via
    /// <see cref="IAccountService"/> and commits once.
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
            var (facts, users) = await GatherFactsAsync(provider, providerUserId, email, currentUserId);
            var decision = OAuthResolver.Decide(facts);

            var result = await ApplyAsync(decision, users, provider, providerUserId, email, displayName, providerLogin, emailVerified);
            if (!result.IsSuccess)
                return result;

            await db.SaveChangesAsync();
            return result;
        }
        // APPEND_MARKER

        private sealed record LoadedUsers(User? LinkedUser, User? CurrentUser, User? EmailOwner);

        private async Task<(OAuthFacts Facts, LoadedUsers Users)> GatherFactsAsync(
            AuthProviderType provider,
            string providerUserId,
            string? email,
            Guid? currentUserId)
        {
            var existingLink = await db.AuthProviders
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.Provider == provider && a.ProviderUserId == providerUserId);

            // Not FindAsync: a tracked (e.g. just-soft-deleted) entity would bypass
            // the soft-delete query filter via the change tracker.
            var currentUser = currentUserId.HasValue
                ? await db.Users.FirstOrDefaultAsync(u => u.Id == currentUserId.Value)
                : null;

            UserEmail? emailOwner = null;
            if (!string.IsNullOrEmpty(email))
            {
                var normalized = email.ToLowerInvariant();
                emailOwner = await db.UserEmails
                    .Include(e => e.User)
                    .FirstOrDefaultAsync(e => e.Email == normalized);
            }

            var facts = new OAuthFacts
            {
                LinkedUserId = existingLink?.UserId,
                CurrentUserId = currentUserId,
                CurrentUserExists = currentUser is not null,
                EmailOwnerUserId = emailOwner?.UserId,
            };

            return (facts, new LoadedUsers(existingLink?.User, currentUser, emailOwner?.User));
        }

        private async Task<Result<User>> ApplyAsync(
            OAuthDecision decision,
            LoadedUsers users,
            AuthProviderType provider,
            string providerUserId,
            string? email,
            string displayName,
            string? providerLogin,
            bool emailVerified)
        {
            switch (decision)
            {
                case OAuthDecision.Reject reject:
                    return Result<User>.Fail(reject.Error);

                case OAuthDecision.LoginAsLinked:
                    return Result<User>.Ok(users.LinkedUser!);

                case OAuthDecision.MergeLinkedIntoCurrent merge:
                    await account.MergeAsync(sourceUserId: merge.LinkedUserId, targetUserId: merge.CurrentUserId);
                    return Result<User>.Ok(users.CurrentUser!);

                case OAuthDecision.LinkToCurrent link:
                {
                    var linkResult = await account.AddProviderAsync(link.CurrentUserId, provider, providerUserId, email, emailVerified);
                    if (!linkResult.IsSuccess)
                        return linkResult;

                    if (link.MergeEmailOwnerUserId is Guid owner)
                        await account.MergeAsync(sourceUserId: owner, targetUserId: link.CurrentUserId);

                    return linkResult;
                }

                case OAuthDecision.LinkToEmailOwner adopt:
                    // If the provider asserts the email is verified, AddProviderAsync
                    // upgrades the existing row's VerifiedAt (it won't insert a duplicate).
                    return await account.AddProviderAsync(adopt.EmailOwnerUserId, provider, providerUserId, email, emailVerified);

                case OAuthDecision.CreateNewUser:
                    var created = await account.CreateFromOAuthAsync(provider, providerUserId, email, displayName, providerLogin, emailVerified);
                    return Result<User>.Ok(created);

                default:
                    throw new InvalidOperationException($"Unhandled OAuth decision: {decision.GetType().Name}");
            }
        }
    }
}
