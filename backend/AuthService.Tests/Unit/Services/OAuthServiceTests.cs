using AuthService.Common;
using AuthService.Entities;
using AuthService.Services;
using AuthService.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Tests.Unit.Services
{
    /// <summary>
    /// Decision-layer tests for OAuthService. OAuthService queries the real DB to
    /// resolve which account a login maps to, then delegates the write to
    /// IAccountService. These tests use a recording fake to assert WHICH action was
    /// dispatched (create / link / merge) without exercising the write internals —
    /// those are covered by AccountServiceTests.
    /// </summary>
    public class OAuthServiceTests : DbTestBase
    {
        private readonly RecordingAccountService _account;
        private readonly OAuthService _sut;

        public OAuthServiceTests()
        {
            _account = new RecordingAccountService(Db);
            _sut = new OAuthService(Db, _account);
        }
        // APPEND_MARKER

        [Fact]
        public async Task NewProvider_NewEmail_DispatchesCreate()
        {
            var result = await _sut.ProcessOAuthLoginAsync(
                AuthProviderType.Github, "gh-123", "user@example.com", "TestUser");

            Assert.True(result.IsSuccess);
            Assert.Contains(nameof(RecordingAccountService.CreateFromOAuthAsync), _account.Calls);
            Assert.Equal("TestUser", result.Value.DisplayName);
        }

        [Fact]
        public async Task ExistingProvider_DispatchesNothing_ReturnsSameUser()
        {
            var first = await _sut.ProcessOAuthLoginAsync(
                AuthProviderType.Github, "gh-123", "user@example.com", "TestUser");
            _account.Calls.Clear();

            var second = await _sut.ProcessOAuthLoginAsync(
                AuthProviderType.Github, "gh-123", "user@example.com", "TestUser");

            Assert.Equal(first.Value.Id, second.Value.Id);
            Assert.Empty(_account.Calls); // straight login, no write dispatched
            Assert.Equal(1, await Db.Users.CountAsync(TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Binding_CurrentUserDeleted_ReturnsUserNotFoundForMerge()
        {
            // Reachable state (ADR-001): an access token stays valid up to 15 minutes
            // after its user was merged away — a binding callback in that window must
            // not resolve. The cascade soft-delete filter hides the user entirely.
            var result = await _sut.ProcessOAuthLoginAsync(
                AuthProviderType.Github, "gh-123", "user@example.com", "TestUser");
            result.Value.IsDeleted = true;
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var second = await _sut.ProcessOAuthLoginAsync(
                AuthProviderType.Google, "gl-deleted", null, "TestUser",
                currentUserId: result.Value.Id);

            Assert.False(second.IsSuccess);
            Assert.Equal(AuthError.UserNotFoundForMerge, second.Error);
        }

        [Fact]
        public async Task NewProvider_ExistingEmail_DispatchesLinkToExistingUser()
        {
            var existing = new User { DisplayName = "Existing" };
            Db.Users.Add(existing);
            Db.UserEmails.Add(new UserEmail { UserId = existing.Id, Email = "shared@example.com", IsPrimary = true });
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.ProcessOAuthLoginAsync(
                AuthProviderType.Github, "gh-789", "shared@example.com", "GithubUser");

            Assert.True(result.IsSuccess);
            Assert.Equal(existing.Id, result.Value.Id);
            Assert.Contains(nameof(RecordingAccountService.AddProviderAsync), _account.Calls);
        }

        [Fact]
        public async Task PureLogin_ExistingUnverifiedEmail_ProviderVerified_UpgradesVerification()
        {
            // Mirrors the real scenario: a user registered with email+password (email left
            // unverified), then logs in via OAuth returning the same, provider-verified
            // email. The existing email row should be upgraded to verified — not left
            // stale as it was before email_verified was threaded through.
            var existing = new User { DisplayName = "Existing" };
            Db.Users.Add(existing);
            Db.UserEmails.Add(new UserEmail { UserId = existing.Id, Email = "user@example.com", IsPrimary = true, VerifiedAt = null });
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.ProcessOAuthLoginAsync(
                AuthProviderType.Google, "gl-1", "user@example.com", "GoogleUser", emailVerified: true);

            Assert.True(result.IsSuccess);
            var email = await Db.UserEmails.SingleAsync(e => e.UserId == existing.Id, TestContext.Current.CancellationToken);
            Assert.NotNull(email.VerifiedAt);
        }

        [Fact]
        public async Task PureLogin_ExistingUnverifiedEmail_ProviderNotVerified_LeavesUnverified()
        {
            // Same path, but the provider did NOT assert verification — we must not
            // silently trust the address. This is the security half of the fix.
            var existing = new User { DisplayName = "Existing" };
            Db.Users.Add(existing);
            Db.UserEmails.Add(new UserEmail { UserId = existing.Id, Email = "user@example.com", IsPrimary = true, VerifiedAt = null });
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.ProcessOAuthLoginAsync(
                AuthProviderType.Github, "gh-1", "user@example.com", "GithubUser", emailVerified: false);

            Assert.True(result.IsSuccess);
            var email = await Db.UserEmails.SingleAsync(e => e.UserId == existing.Id, TestContext.Current.CancellationToken);
            Assert.Null(email.VerifiedAt);
        }

        [Fact]
        public async Task NormalizesEmailToLowerCase()
        {
            await _sut.ProcessOAuthLoginAsync(
                AuthProviderType.Github, "gh-case", "USER@EXAMPLE.COM", "TestUser");

            var email = await Db.UserEmails.SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal("user@example.com", email.Email);
        }
        // APPEND_MARKER2

        // ==================== Binding (currentUserId provided) ====================

        [Fact]
        public async Task Binding_NewProvider_DispatchesLinkToCurrentUser()
        {
            var current = new User { DisplayName = "Current" };
            Db.Users.Add(current);
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.ProcessOAuthLoginAsync(
                AuthProviderType.Google, "gl-123", "google@example.com", "GoogleUser",
                currentUserId: current.Id);

            Assert.True(result.IsSuccess);
            Assert.Equal(current.Id, result.Value.Id);
            Assert.Contains(nameof(RecordingAccountService.AddProviderAsync), _account.Calls);
            Assert.DoesNotContain(nameof(RecordingAccountService.MergeAsync), _account.Calls);
        }

        [Fact]
        public async Task Binding_EmailBelongsToOtherUser_DispatchesMerge()
        {
            var current = new User { DisplayName = "Current" };
            Db.Users.Add(current);
            Db.UserEmails.Add(new UserEmail { UserId = current.Id, Email = "current@example.com", IsPrimary = true });

            var other = new User { DisplayName = "Other" };
            Db.Users.Add(other);
            Db.UserEmails.Add(new UserEmail { UserId = other.Id, Email = "shared@example.com", IsPrimary = true });
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.ProcessOAuthLoginAsync(
                AuthProviderType.Google, "gl-456", "shared@example.com", "GoogleUser",
                currentUserId: current.Id);

            Assert.True(result.IsSuccess);
            Assert.Equal(current.Id, result.Value.Id);
            Assert.Contains(nameof(RecordingAccountService.MergeAsync), _account.Calls);
            Assert.Equal((other.Id, current.Id), _account.LastMerge);
        }

        [Fact]
        public async Task Binding_ExistingProvider_BelongsToOtherUser_DispatchesMerge()
        {
            var current = new User { DisplayName = "Current" };
            Db.Users.Add(current);

            var other = new User { DisplayName = "Other" };
            Db.Users.Add(other);
            Db.AuthProviders.Add(new AuthProvider { UserId = other.Id, Provider = AuthProviderType.Github, ProviderUserId = "gh-999" });
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.ProcessOAuthLoginAsync(
                AuthProviderType.Github, "gh-999", null, "GithubUser",
                currentUserId: current.Id);

            Assert.True(result.IsSuccess);
            Assert.Equal(current.Id, result.Value.Id);
            Assert.Equal((other.Id, current.Id), _account.LastMerge);
        }

        [Fact]
        public async Task Binding_ExistingProvider_SameUser_DispatchesNothing()
        {
            var current = new User { DisplayName = "Current" };
            Db.Users.Add(current);
            Db.AuthProviders.Add(new AuthProvider { UserId = current.Id, Provider = AuthProviderType.Github, ProviderUserId = "gh-111" });
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.ProcessOAuthLoginAsync(
                AuthProviderType.Github, "gh-111", null, "GithubUser",
                currentUserId: current.Id);

            Assert.True(result.IsSuccess);
            Assert.Equal(current.Id, result.Value.Id);
            Assert.Empty(_account.Calls);
        }

        [Fact]
        public async Task Binding_InvalidCurrentUserId_ReturnsUserNotFoundForMerge()
        {
            var result = await _sut.ProcessOAuthLoginAsync(
                AuthProviderType.Github, "gh-new", null, "GithubUser",
                currentUserId: Guid.NewGuid());

            Assert.False(result.IsSuccess);
            Assert.Equal(AuthError.UserNotFoundForMerge, result.Error);
        }
    }
}
