using AuthService.Common;
using AuthService.Entities;
using AuthService.Services;
using AuthService.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Tests.Unit.Services
{
    /// <summary>
    /// Write-behavior tests for AccountService against a real (in-memory) DB.
    /// AccountService write methods do NOT SaveChanges; tests call it explicitly.
    /// </summary>
    public class AccountServiceTests : DbTestBase
    {
        private readonly AccountService _sut;

        public AccountServiceTests()
        {
            _sut = new AccountService(Db, new RecordingGrantRevoker());
        }

        // ==================== CreateFromOAuth ====================

        [Fact]
        public async Task CreateFromOAuth_VerifiedEmail_MarksEmailVerified()
        {
            var user = await _sut.CreateFromOAuthAsync(
                AuthProviderType.Github, "gh-1", "user@example.com", "Test", "octocat", emailVerified: true);
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var email = await Db.UserEmails.SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal("user@example.com", email.Email);
            Assert.True(email.IsPrimary);
            Assert.NotNull(email.VerifiedAt);
            Assert.Equal(user.Id, email.UserId);

            var provider = await Db.AuthProviders.SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal(AuthProviderType.Github, provider.Provider);
        }

        [Fact]
        public async Task CreateFromOAuth_UnverifiedEmail_LeavesEmailUnverified()
        {
            // The provider did not assert the email is verified, so we must not trust it.
            await _sut.CreateFromOAuthAsync(
                AuthProviderType.Github, "gh-1", "user@example.com", "Test", "octocat", emailVerified: false);
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var email = await Db.UserEmails.SingleAsync(TestContext.Current.CancellationToken);
            Assert.Null(email.VerifiedAt);
        }

        [Fact]
        public async Task CreateFromOAuth_NullEmail_CreatesNoEmail()
        {
            await _sut.CreateFromOAuthAsync(AuthProviderType.Github, "gh-2", null, "NoEmail", null);
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            Assert.Equal(0, await Db.UserEmails.CountAsync(TestContext.Current.CancellationToken));
        }
        // APPEND_MARKER

        // ==================== Merge ====================

        [Fact]
        public async Task Merge_MigratesApiKeysToTarget()
        {
            // Regression: before this fix, merge left the source user's API keys
            // orphaned on the soft-deleted source, silently breaking them.
            var target = new User { DisplayName = "Target" };
            var source = new User { DisplayName = "Source" };
            Db.Users.AddRange(target, source);
            Db.ApiKeys.Add(new ApiKey
            {
                UserId = source.Id,
                Name = "source-key",
                Prefix = "abc12345",
                SecretHash = "hash",
            });
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            await _sut.MergeAsync(source.Id, target.Id);
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var key = await Db.ApiKeys.SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal(target.Id, key.UserId);
            Assert.False(key.IsRevoked);

            var sourceAfter = await Db.Users.FindAsync([source.Id], TestContext.Current.CancellationToken);
            Assert.True(sourceAfter!.IsDeleted);
        }

        [Fact]
        public async Task Merge_MovesPasswordCredential_WhenTargetHasNone()
        {
            var target = new User { DisplayName = "Target" };
            var source = new User { DisplayName = "Source" };
            Db.Users.AddRange(target, source);
            Db.PasswordCredentials.Add(new PasswordCredential { UserId = source.Id, PasswordHash = "salt.hash" });
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            await _sut.MergeAsync(source.Id, target.Id);
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var pwd = await Db.PasswordCredentials.SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal(target.Id, pwd.UserId);
            Assert.Equal("salt.hash", pwd.PasswordHash);
        }

        [Fact]
        public async Task Merge_KeepsTargetPassword_WhenBothHaveOne()
        {
            var target = new User { DisplayName = "Target" };
            var source = new User { DisplayName = "Source" };
            Db.Users.AddRange(target, source);
            Db.PasswordCredentials.Add(new PasswordCredential { UserId = target.Id, PasswordHash = "target.hash" });
            Db.PasswordCredentials.Add(new PasswordCredential { UserId = source.Id, PasswordHash = "source.hash" });
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            await _sut.MergeAsync(source.Id, target.Id);
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var pwd = await Db.PasswordCredentials.SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal(target.Id, pwd.UserId);
            Assert.Equal("target.hash", pwd.PasswordHash);
        }

        [Fact]
        public async Task Merge_RevokesAndMovesSourceSessions()
        {
            var target = new User { DisplayName = "Target" };
            var source = new User { DisplayName = "Source" };
            Db.Users.AddRange(target, source);
            Db.Sessions.Add(new Session { UserId = source.Id, IpAddress = "1.2.3.4", Device = "Old", ExpiresAt = DateTimeOffset.UtcNow.AddDays(30) });
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            await _sut.MergeAsync(source.Id, target.Id);
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var session = await Db.Sessions.SingleAsync(TestContext.Current.CancellationToken);
            Assert.True(session.Revoked);
            Assert.Equal(target.Id, session.UserId);
        }

        [Fact]
        public async Task Merge_ReassignsSourceEmailsToTarget_LosingPrimary()
        {
            // Email is globally unique (ADR-011), so source and target can never share an
            // address. Merge therefore reassigns every source email to the target; the
            // target keeps its own primary and the moved ones become non-primary.
            // (The production-impossible "both users hold the same email" case is covered
            // structurally by the global unique index, validated in MergeConstraintTests.)
            var target = new User { DisplayName = "Target" };
            Db.Users.Add(target);
            Db.UserEmails.Add(new UserEmail { UserId = target.Id, Email = "target@example.com", IsPrimary = true });

            var source = new User { DisplayName = "Source" };
            Db.Users.Add(source);
            Db.UserEmails.Add(new UserEmail { UserId = source.Id, Email = "source-a@example.com", IsPrimary = true });
            Db.UserEmails.Add(new UserEmail { UserId = source.Id, Email = "source-b@example.com", IsPrimary = false });
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            await _sut.MergeAsync(source.Id, target.Id);
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var targetEmails = await Db.UserEmails.Where(e => e.UserId == target.Id).ToListAsync(TestContext.Current.CancellationToken);
            Assert.Equal(3, targetEmails.Count);
            Assert.Contains(targetEmails, e => e.Email == "target@example.com" && e.IsPrimary);
            Assert.Contains(targetEmails, e => e.Email == "source-a@example.com" && !e.IsPrimary);
            Assert.Contains(targetEmails, e => e.Email == "source-b@example.com" && !e.IsPrimary);
            Assert.Equal(0, await Db.UserEmails.CountAsync(e => e.UserId == source.Id, TestContext.Current.CancellationToken));
        }
        // APPEND_MARKER2

        // ==================== UnlinkProvider ====================

        [Fact]
        public async Task UnlinkProvider_WithMultipleProviders_Succeeds()
        {
            var user = new User { DisplayName = "Test" };
            Db.Users.Add(user);
            Db.AuthProviders.Add(new AuthProvider { UserId = user.Id, Provider = AuthProviderType.Github, ProviderUserId = "gh" });
            Db.AuthProviders.Add(new AuthProvider { UserId = user.Id, Provider = AuthProviderType.Google, ProviderUserId = "gl" });
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.UnlinkProviderAsync(user.Id, AuthProviderType.Github);
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess);
            var remaining = await Db.AuthProviders.SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal(AuthProviderType.Google, remaining.Provider);
        }

        [Fact]
        public async Task UnlinkProvider_WithPasswordAsBackup_Succeeds()
        {
            var user = new User { DisplayName = "Test" };
            Db.Users.Add(user);
            Db.AuthProviders.Add(new AuthProvider { UserId = user.Id, Provider = AuthProviderType.Github, ProviderUserId = "gh" });
            Db.PasswordCredentials.Add(new PasswordCredential { UserId = user.Id, PasswordHash = "hashed" });
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.UnlinkProviderAsync(user.Id, AuthProviderType.Github);

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task UnlinkProvider_LastLoginMethod_ReturnsCannotUnlink()
        {
            var user = new User { DisplayName = "Test" };
            Db.Users.Add(user);
            Db.AuthProviders.Add(new AuthProvider { UserId = user.Id, Provider = AuthProviderType.Github, ProviderUserId = "gh" });
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.UnlinkProviderAsync(user.Id, AuthProviderType.Github);

            Assert.False(result.IsSuccess);
            Assert.Equal(AuthError.CannotUnlinkLastLoginMethod, result.Error);
        }

        [Fact]
        public async Task UnlinkProvider_NotLinked_ReturnsProviderNotLinked()
        {
            var user = new User { DisplayName = "Test" };
            Db.Users.Add(user);
            Db.AuthProviders.Add(new AuthProvider { UserId = user.Id, Provider = AuthProviderType.Github, ProviderUserId = "gh" });
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.UnlinkProviderAsync(user.Id, AuthProviderType.Google);

            Assert.False(result.IsSuccess);
            Assert.Equal(AuthError.ProviderNotLinked, result.Error);
        }

        [Fact]
        public async Task UnlinkProvider_NonExistentUser_ReturnsProviderNotLinked()
        {
            var result = await _sut.UnlinkProviderAsync(Guid.NewGuid(), AuthProviderType.Github);

            Assert.False(result.IsSuccess);
            Assert.Equal(AuthError.ProviderNotLinked, result.Error);
        }

        // ==================== ChangeUsername ====================

        [Fact]
        public async Task ChangeUsername_ToAvailableName_SetsIt_AndReportsChanged()
        {
            var user = new User { Username = "alice", DisplayName = "Alice" };
            Db.Users.Add(user);
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.ChangeUsernameAsync(user.Id, "alice2");
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess);
            Assert.True(result.Value);
            var reloaded = await Db.Users.FindAsync([user.Id], TestContext.Current.CancellationToken);
            Assert.Equal("alice2", reloaded!.Username);
        }

        [Fact]
        public async Task ChangeUsername_UppercaseInput_IsNormalizedToLowercase()
        {
            var user = new User { Username = "alice", DisplayName = "Alice" };
            Db.Users.Add(user);
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.ChangeUsernameAsync(user.Id, "AliceNew");
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess);
            var reloaded = await Db.Users.FindAsync([user.Id], TestContext.Current.CancellationToken);
            Assert.Equal("alicenew", reloaded!.Username);
        }

        [Fact]
        public async Task ChangeUsername_SameName_IsNoOp_AndReportsUnchanged()
        {
            var user = new User { Username = "alice", DisplayName = "Alice" };
            Db.Users.Add(user);
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.ChangeUsernameAsync(user.Id, "alice");

            Assert.True(result.IsSuccess);
            Assert.False(result.Value);
        }

        [Fact]
        public async Task ChangeUsername_ToTakenName_ReturnsUsernameAlreadyExists()
        {
            Db.Users.Add(new User { Username = "taken", DisplayName = "Other" });
            var user = new User { Username = "alice", DisplayName = "Alice" };
            Db.Users.Add(user);
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.ChangeUsernameAsync(user.Id, "taken");

            Assert.False(result.IsSuccess);
            Assert.Equal(AuthError.UsernameAlreadyExists, result.Error);
        }

        [Fact]
        public async Task ChangeUsername_ToReservedOrMalformedName_ReturnsInvalidUsername()
        {
            var user = new User { Username = "alice", DisplayName = "Alice" };
            Db.Users.Add(user);
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            Assert.Equal(AuthError.InvalidUsername, (await _sut.ChangeUsernameAsync(user.Id, "admin")).Error);
            Assert.Equal(AuthError.InvalidUsername, (await _sut.ChangeUsernameAsync(user.Id, "-bad-")).Error);
        }

        [Fact]
        public async Task ChangeUsername_NonExistentUser_ReturnsUserNotFound()
        {
            var result = await _sut.ChangeUsernameAsync(Guid.NewGuid(), "whoever");

            Assert.False(result.IsSuccess);
            Assert.Equal(AuthError.UserNotFound, result.Error);
        }
    }
}
