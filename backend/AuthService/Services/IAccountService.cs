using AuthService.Common;
using AuthService.Entities;

namespace AuthService.Services
{
    /// <summary>
    /// Account composition write authority. Owns all operations that create, merge,
    /// or modify the set of data that constitutes a user account: emails, providers,
    /// password, sessions, API keys, OIDC grants.
    ///
    /// Write methods do NOT call SaveChangesAsync — callers commit once at the end
    /// to ensure the entire operation (e.g. OAuth login + merge) is atomic.
    /// </summary>
    public interface IAccountService
    {
        /// <summary>
        /// Create a new user from an OAuth login. Adds the provider, email (if present),
        /// and generates a username. The email is marked verified only when the provider
        /// asserts it (<paramref name="emailVerified"/>).
        /// </summary>
        Task<User> CreateFromOAuthAsync(
            AuthProviderType provider,
            string providerUserId,
            string? email,
            string displayName,
            string? providerLogin,
            bool emailVerified = false);

        /// <summary>
        /// Create a new user from password registration. Adds the email, password,
        /// and a Password provider entry.
        /// </summary>
        Task<User> CreateFromPasswordAsync(
            string username,
            string email,
            string displayName,
            string passwordHash);

        /// <summary>
        /// Link an OAuth provider to an existing user. Optionally adds the email; if the
        /// email already exists on this user and the provider asserts it verified
        /// (<paramref name="emailVerified"/>), upgrades the existing row's VerifiedAt.
        /// If the email belongs to a different user, the caller decides to merge.
        /// </summary>
        Task<Result<User>> AddProviderAsync(
            Guid userId,
            AuthProviderType provider,
            string providerUserId,
            string? email,
            bool emailVerified = false);

        /// <summary>
        /// Add a password to an existing user (OAuth-only user setting a password).
        /// </summary>
        Task<Result> AddPasswordAsync(Guid userId, string passwordHash);

        /// <summary>
        /// Set the user's password unconditionally: overwrites an existing credential,
        /// or creates one (plus the Password provider) when none exists. Used by the
        /// password reset and change flows, where ownership was already proven.
        /// </summary>
        Task<Result> SetPasswordAsync(Guid userId, string passwordHash);

        /// <summary>
        /// Merge all data from sourceUserId into targetUserId, then soft-delete the source.
        /// Migrates: AuthProviders, UserEmails (reassigned; global email uniqueness means
        /// no overlap is possible), Sessions, ApiKeys, PasswordCredential.
        /// Revokes source's active sessions and OIDC grants (the latter commit
        /// immediately via the OpenIddict managers, outside the caller's unit of work).
        /// </summary>
        Task MergeAsync(Guid sourceUserId, Guid targetUserId);

        /// <summary>
        /// Unlink an OAuth provider from a user. Fails if it's the last login method.
        /// </summary>
        Task<Result> UnlinkProviderAsync(Guid userId, AuthProviderType provider);
    }
}
