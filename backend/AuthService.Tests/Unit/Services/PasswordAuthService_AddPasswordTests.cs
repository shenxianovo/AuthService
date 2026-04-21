using AuthService.Common;
using AuthService.Data;
using AuthService.Entities;
using AuthService.Configuration;
using AuthService.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;

namespace AuthService.Tests.Unit.Services
{
    public class PasswordAuthService_AddPasswordTests : IDisposable
    {
        private readonly AppDbContext _db;
        private readonly PasswordAuthService _sut;

        public PasswordAuthService_AddPasswordTests()
        {
            var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            _db = new AppDbContext(dbOptions);

            var jwtServiceMock = new Mock<IJwtService>();
            jwtServiceMock
                .Setup(j => j.GenerateAccessToken(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Returns("fake-access-token");

            var jwtOptions = Options.Create(new JwtOptions
            {
                AccessTokenExpirationMinutes = 15,
                RefreshTokenExpirationDays = 30,
                SessionExpirationDays = 30,
            });

            var sessionService = new SessionService(_db, jwtServiceMock.Object, jwtOptions);
            var passwordHasher = new PasswordHasher<User>();
            _sut = new PasswordAuthService(_db, sessionService, passwordHasher);
        }

        public void Dispose() => _db.Dispose();

        [Fact]
        public async Task AddPassword_ToOAuthUser_Succeeds()
        {
            var user = new User { DisplayName = "OAuthUser" };
            _db.Users.Add(user);
            _db.AuthProviders.Add(new AuthProvider
            {
                UserId = user.Id,
                Provider = AuthProviderType.Github,
                ProviderUserId = "github-123"
            });
            await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.AddPasswordAsync(user.Id, "NewPassword123");

            Assert.True(result.IsSuccess);

            var credential = await _db.PasswordCredentials.FirstOrDefaultAsync(c => c.UserId == user.Id, TestContext.Current.CancellationToken);
            Assert.NotNull(credential);
            Assert.NotEmpty(credential.PasswordHash);
            Assert.NotEqual("NewPassword123", credential.PasswordHash);

            var passwordProvider = await _db.AuthProviders
                .FirstOrDefaultAsync(p => p.UserId == user.Id && p.Provider == AuthProviderType.Password, TestContext.Current.CancellationToken);
            Assert.NotNull(passwordProvider);
        }

        [Fact]
        public async Task AddPassword_ToUserWithExistingPassword_ReturnsPasswordAlreadySet()
        {
            var user = new User { DisplayName = "PasswordUser" };
            _db.Users.Add(user);
            _db.PasswordCredentials.Add(new PasswordCredential
            {
                UserId = user.Id,
                PasswordHash = "existing-hash"
            });
            await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

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