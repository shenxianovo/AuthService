using AuthService.Common;
using AuthService.Data;
using AuthService.Entities;
using AuthService.Services;

namespace AuthService.Tests.Fixtures
{
    /// <summary>
    /// Records which IAccountService method OAuthService dispatched to, while
    /// delegating to a real AccountService so DB state stays correct for assertions.
    /// </summary>
    public sealed class RecordingAccountService(AppDbContext db) : IAccountService
    {
        private readonly AccountService _inner = new(db);

        public List<string> Calls { get; } = [];
        public (Guid Source, Guid Target)? LastMerge { get; private set; }

        public Task<User> CreateFromOAuthAsync(AuthProviderType provider, string providerUserId, string? email, string displayName, string? providerLogin)
        {
            Calls.Add(nameof(CreateFromOAuthAsync));
            return _inner.CreateFromOAuthAsync(provider, providerUserId, email, displayName, providerLogin);
        }

        public Task<User> CreateFromPasswordAsync(string username, string email, string displayName, string passwordHash)
        {
            Calls.Add(nameof(CreateFromPasswordAsync));
            return _inner.CreateFromPasswordAsync(username, email, displayName, passwordHash);
        }

        public Task<Result<User>> AddProviderAsync(Guid userId, AuthProviderType provider, string providerUserId, string? email)
        {
            Calls.Add(nameof(AddProviderAsync));
            return _inner.AddProviderAsync(userId, provider, providerUserId, email);
        }

        public Task<Result> AddPasswordAsync(Guid userId, string passwordHash)
        {
            Calls.Add(nameof(AddPasswordAsync));
            return _inner.AddPasswordAsync(userId, passwordHash);
        }

        public Task MergeAsync(Guid sourceUserId, Guid targetUserId)
        {
            Calls.Add(nameof(MergeAsync));
            LastMerge = (sourceUserId, targetUserId);
            return _inner.MergeAsync(sourceUserId, targetUserId);
        }

        public Task<Result> UnlinkProviderAsync(Guid userId, AuthProviderType provider)
        {
            Calls.Add(nameof(UnlinkProviderAsync));
            return _inner.UnlinkProviderAsync(userId, provider);
        }
    }
}
