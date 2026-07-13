using AuthService.Services;

namespace AuthService.Tests.Fixtures
{
    /// <summary>
    /// Records grant revocations instead of touching OpenIddict stores, so
    /// DB-only unit tests can construct AccountService and assert the
    /// grants-die-with-the-account contract without a full host.
    /// </summary>
    public class RecordingGrantRevoker : IOidcGrantRevoker
    {
        public List<Guid> RevokedUserIds { get; } = [];

        public Task RevokeAllForUserAsync(Guid userId)
        {
            RevokedUserIds.Add(userId);
            return Task.CompletedTask;
        }
    }
}
