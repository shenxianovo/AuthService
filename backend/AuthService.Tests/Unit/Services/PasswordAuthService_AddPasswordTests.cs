using System.Security.Claims;
using AuthService.Common;
using AuthService.Entities;
using AuthService.Configuration;
using AuthService.Services;
using AuthService.Tests.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;

namespace AuthService.Tests.Unit.Services
{
    public class PasswordAuthService_AddPasswordTests : DbTestBase
    {
        private readonly PasswordAuthService _sut;

        public PasswordAuthService_AddPasswordTests()
        {
            var jwtServiceMock = new Mock<IJwtService>();
            jwtServiceMock
                .Setup(j => j.GenerateAccessToken(It.IsAny<Guid>(), It.IsAny<Claim[]>()))
                .Returns("fake-access-token");

            var jwtOptions = Options.Create(new JwtOptions
            {
                AccessTokenExpirationMinutes = 15,
                RefreshTokenExpirationDays = 30,
                SessionExpirationDays = 30,
            });

            var sessionService = new SessionService(Db, jwtServiceMock.Object, jwtOptions);
            var passwordHasher = new PasswordHasher<User>();
            _sut = new PasswordAuthService(Db, sessionService, passwordHasher);
        }

        [Fact]
        public async Task AddPassword_ToOAuthUser_Succeeds()
        {
            var user = new User { DisplayName = "OAuthUser" };
            Db.Users.Add(user);
            Db.AuthProviders.Add(new AuthProvider
            {
                UserId = user.Id,
                Provider = AuthProviderType.Github,
                ProviderUserId = "github-123"
            });
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.AddPasswordAsync(user.Id, "NewPassword123");

            Assert.True(result.IsSuccess);

            var credential = await Db.PasswordCredentials.FirstOrDefaultAsync(c => c.UserId == user.Id, TestContext.Current.CancellationToken);
            Assert.NotNull(credential);
            Assert.NotEmpty(credential.PasswordHash);
            Assert.NotEqual("NewPassword123", credential.PasswordHash);

            var passwordProvider = await Db.AuthProviders
                .FirstOrDefaultAsync(p => p.UserId == user.Id && p.Provider == AuthProviderType.Password, TestContext.Current.CancellationToken);
            Assert.NotNull(passwordProvider);
        }

        [Fact]
        public async Task AddPassword_ToUserWithExistingPassword_ReturnsPasswordAlreadySet()
        {
            var user = new User { DisplayName = "PasswordUser" };
            Db.Users.Add(user);
            Db.PasswordCredentials.Add(new PasswordCredential
            {
                UserId = user.Id,
                PasswordHash = "existing-hash"
            });
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.AddPasswordAsync(user.Id, "NewPassword123");

            Assert.False(result.IsSuccess);
            Assert.Equal(AuthError.PasswordAlreadySet, result.Error);
        }

        [Fact]
        public async Task AddPassword_ToNonExistentUser_ReturnsUserNotFound()
        {
            var result = await _sut.AddPasswordAsync(Guid.NewGuid(), "NewPassword123");

            Assert.False(result.IsSuccess);
            Assert.Equal(AuthError.UserNotFound, result.Error);
        }
    }
}