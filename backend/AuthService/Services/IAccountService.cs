using AuthService.Common;
using AuthService.Entities;

namespace AuthService.Services
{
    /// <summary>
    /// Account composition write authority. Owns all operations that create, merge,
    /// or modify the set of data that constitutes a user account: emails, providers,
    /// password, sessions, API keys.
    ///
    /// Write methods do NOT call SaveChangesAsync — callers commit once at the end
    /// to ensure the entire operation (e.g. OAuth login + merge) is atomic.
    /// </summary>
    public interface IAccountService
    {
        /// <summary>
        /// Create a new user from an OAuth login. Adds the provider, email (if present),
        /// and generates a username.
        /// </summary>
        Task<User> CreateFromOAuthAsync(
            AuthProviderType provider,
            string providerUserId,
            string? email,
            string displayName,
            string? providerLogin);

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
        /// Link an OAuth provider to an existing user. Optionally adds/verifies the email.
        /// If the email belongs to a different user, returns Fail(UserNotFoundForMerge)
        /// signaling the caller should merge instead.
        /// </summary>
        Task<Result<User>> AddProviderAsync(
            Guid userId,
            AuthProviderType provider,
            string providerUserId,
            string? email);

        /// <summary>
        /// Add a password to an existing user (OAuth-only user setting a password).
        /// </summary>
        Task<Result> AddPasswordAsync(Guid userId, string passwordHash);

        /// <summary>
        /// Merge all data from sourceUserId into targetUserId, then soft-delete the source.
        /// Migrates: AuthProviders, UserEmails (deduplicates), Sessions, ApiKeys, PasswordCredential.
        /// Revokes source's active sessions.
        /// </summary>
        Task MergeAsync(Guid sourceUserId, Guid targetUserId);

        /// <summary>
        /// Unlink an OAuth provider from a user. Fails if it's the last login method.
        /// </summary>
        Task<Result> UnlinkProviderAsync(Guid userId, AuthProviderType provider);
    }
}
