using AuthService.Common;
using AuthService.Data;
using AuthService.Entities;
using AuthService.Services;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Tests.Unit.Services
{
    public class UserServiceTests : IDisposable
    {
        private readonly AppDbContext _db;
        private readonly UserService _sut;

        public UserServiceTests()
        {
            var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            _db = new AppDbContext(dbOptions);
            _sut = new UserService(_db);
        }

        public void Dispose() => _db.Dispose();

        // ==================== GetUserInfoAsync ====================

        [Fact]
        public async Task GetUserInfo_WithValidUser_ReturnsUserInfo()
        {
            var user = new User { DisplayName = "TestUser" };
            _db.Users.Add(user);
            _db.UserEmails.Add(new UserEmail
            {
                UserId = user.Id,
                Email = "test@example.com",
                IsPrimary = true,
                VerifiedAt = DateTimeOffset.UtcNow,
            });
            _db.AuthProviders.Add(new AuthProvider
            {
                UserId = user.Id,
                Provider = AuthProviderType.Password,
                ProviderUserId = user.Id.ToString(),
            });
            _db.AuthProviders.Add(new AuthProvider
            {
                UserId = user.Id,
                Provider = AuthProviderType.Github,
                ProviderUserId = "github-123",
            });
            _db.PasswordCredentials.Add(new PasswordCredential
            {
                UserId = user.Id,
                PasswordHash = "hashed",
            });
            await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetUserInfoAsync(user.Id);

            Assert.NotNull(result);
            Assert.Equal(user.Id, result.UserId);
            Assert.Equal("TestUser", result.DisplayName);
            Assert.True(result.HasPassword);
            Assert.Single(result.Emails);
            Assert.Equal("test@example.com", result.Emails[0].Email);
            Assert.True(result.Emails[0].IsPrimary);
            Assert.True(result.Emails[0].IsVerified);
            // Password provider should be excluded
            Assert.DoesNotContain(result.Providers, p => p.Provider == "Password");
            Assert.Single(result.Providers);
            Assert.Equal("Github", result.Providers[0].Provider);
        }

        [Fact]
        public async Task GetUserInfo_WithMultipleEmails_ReturnsAll()
        {
            var user = new User { DisplayName = "MultiEmail" };
            _db.Users.Add(user);
            _db.UserEmails.Add(new UserEmail
            {
                UserId = user.Id,
                Email = "primary@example.com",
                IsPrimary = true,
                VerifiedAt = DateTimeOffset.UtcNow,
            });
            _db.UserEmails.Add(new UserEmail
            {
                UserId = user.Id,
                Email = "secondary@example.com",
                IsPrimary = false,
                VerifiedAt = null,
            });
            await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetUserInfoAsync(user.Id);

            Assert.NotNull(result);
            Assert.Equal(2, result.Emails.Count);
            Assert.Contains(result.Emails, e => e.Email == "primary@example.com" && e.IsPrimary && e.IsVerified);
            Assert.Contains(result.Emails, e => e.Email == "secondary@example.com" && !e.IsPrimary && !e.IsVerified);
        }

        [Fact]
        public async Task GetUserInfo_WithoutPassword_HasPasswordIsFalse()
        {
            var user = new User { DisplayName = "OAuthOnly" };
            _db.Users.Add(user);
            _db.AuthProviders.Add(new AuthProvider
            {
                UserId = user.Id,
                Provider = AuthProviderType.Google,
                ProviderUserId = "google-123",
            });
            await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetUserInfoAsync(user.Id);

            Assert.NotNull(result);
            Assert.False(result.HasPassword);
        }

        [Fact]
        public async Task GetUserInfo_WithDeletedUser_ReturnsNull()
        {
            var user = new User { DisplayName = "Deleted", IsDeleted = true };
            _db.Users.Add(user);
            await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetUserInfoAsync(user.Id);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetUserInfo_WithNonExistentUser_ReturnsNull()
        {
            var result = await _sut.GetUserInfoAsync(Guid.NewGuid());

            Assert.Null(result);
        }

        // ==================== UnlinkProviderAsync ====================

        [Fact]
        public async Task UnlinkProvider_WithValidProvider_Succeeds()
        {
            var user = new User { DisplayName = "Test" };
            _db.Users.Add(user);
            _db.AuthProviders.Add(new AuthProvider
            {
                UserId = user.Id,
                Provider = AuthProviderType.Github,
                ProviderUserId = "github-123",
            });
            _db.AuthProviders.Add(new AuthProvider
            {
                UserId = user.Id,
                Provider = AuthProviderType.Google,
                ProviderUserId = "google-456",
            });
            await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.UnlinkProviderAsync(user.Id, AuthProviderType.Github);

            Assert.True(result.IsSuccess);

            var remaining = await _db.AuthProviders
                .Where(p => p.UserId == user.Id)
                .ToListAsync(TestContext.Current.CancellationToken);
            Assert.Single(remaining);
            Assert.Equal(AuthProviderType.Google, remaining[0].Provider);
        }

        [Fact]
        public async Task UnlinkProvider_WithPasswordAsBackup_Succeeds()
        {
            var user = new User { DisplayName = "Test" };
            _db.Users.Add(user);
            _db.AuthProviders.Add(new AuthProvider
            {
                UserId = user.Id,
                Provider = AuthProviderType.Github,
                ProviderUserId = "github-123",
            });
            _db.PasswordCredentials.Add(new PasswordCredential
            {
                UserId = user.Id,
                PasswordHash = "hashed",
            });
            await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Even though this is the only OAuth provider, user has a password so it's fine
            var result = await _sut.UnlinkProviderAsync(user.Id, AuthProviderType.Github);

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task UnlinkProvider_LastLoginMethod_ReturnsCannotUnlink()
        {
            var user = new User { DisplayName = "Test" };
            _db.Users.Add(user);
            _db.AuthProviders.Add(new AuthProvider
            {
                UserId = user.Id,
                Provider = AuthProviderType.Github,
                ProviderUserId = "github-123",
            });
            // No password, only one provider → cannot unlink
            await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.UnlinkProviderAsync(user.Id, AuthProviderType.Github);

            Assert.False(result.IsSuccess);
            Assert.Equal(AuthError.CannotUnlinkLastLoginMethod, result.Error);
        }

        [Fact]
        public async Task UnlinkProvider_NotLinked_ReturnsProviderNotLinked()
        {
            var user = new User { DisplayName = "Test" };
            _db.Users.Add(user);
            _db.AuthProviders.Add(new AuthProvider
            {
                UserId = user.Id,
                Provider = AuthProviderType.Github,
                ProviderUserId = "github-123",
            });
            await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Try to unlink Google which is not linked
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
    }
}
