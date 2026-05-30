using AuthService.Common;
using AuthService.Entities;
using AuthService.Services;
using AuthService.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Tests.Unit.Services
{
    public class UserServiceTests : DbTestBase
    {
        private readonly UserService _sut;

        public UserServiceTests()
        {
            _sut = new UserService(Db);
        }

        // ==================== GetUserInfoAsync ====================

        [Fact]
        public async Task GetUserInfo_WithValidUser_ReturnsUserInfo()
        {
            var user = new User { DisplayName = "TestUser" };
            Db.Users.Add(user);
            Db.UserEmails.Add(new UserEmail
            {
                UserId = user.Id,
                Email = "test@example.com",
                IsPrimary = true,
                VerifiedAt = DateTimeOffset.UtcNow,
            });
            Db.AuthProviders.Add(new AuthProvider
            {
                UserId = user.Id,
                Provider = AuthProviderType.Password,
                ProviderUserId = user.Id.ToString(),
            });
            Db.AuthProviders.Add(new AuthProvider
            {
                UserId = user.Id,
                Provider = AuthProviderType.Github,
                ProviderUserId = "github-123",
            });
            Db.PasswordCredentials.Add(new PasswordCredential
            {
                UserId = user.Id,
                PasswordHash = "hashed",
            });
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

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
            Db.Users.Add(user);
            Db.UserEmails.Add(new UserEmail
            {
                UserId = user.Id,
                Email = "primary@example.com",
                IsPrimary = true,
                VerifiedAt = DateTimeOffset.UtcNow,
            });
            Db.UserEmails.Add(new UserEmail
            {
                UserId = user.Id,
                Email = "secondary@example.com",
                IsPrimary = false,
                VerifiedAt = null,
            });
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

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
            Db.Users.Add(user);
            Db.AuthProviders.Add(new AuthProvider
            {
                UserId = user.Id,
                Provider = AuthProviderType.Google,
                ProviderUserId = "google-123",
            });
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetUserInfoAsync(user.Id);

            Assert.NotNull(result);
            Assert.False(result.HasPassword);
        }

        [Fact]
        public async Task GetUserInfo_WithDeletedUser_ReturnsNull()
        {
            var user = new User { DisplayName = "Deleted", IsDeleted = true };
            Db.Users.Add(user);
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.GetUserInfoAsync(user.Id);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetUserInfo_WithNonExistentUser_ReturnsNull()
        {
            var result = await _sut.GetUserInfoAsync(Guid.NewGuid());

            Assert.Null(result);
        }
    }
}
