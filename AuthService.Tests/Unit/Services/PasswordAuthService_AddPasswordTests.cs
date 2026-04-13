using AuthService.Data;
using AuthService.Entities;
using AuthService.Configuration;
using AuthService.Exceptions;
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
            // Create an OAuth-only user
            var user = new User { DisplayName = "OAuthUser" };
            _db.Users.Add(user);
            _db.AuthProviders.Add(new AuthProvider
            {
                UserId = user.Id,
                Provider = AuthProviderType.Github,
                ProviderUserId = "github-123"
            });
            await _db.SaveChangesAsync();

            await _sut.AddPasswordAsync(user.Id, "NewPassword123");

            var credential = await _db.PasswordCredentials.FirstOrDefaultAsync(c => c.UserId == user.Id);
            Assert.NotNull(credential);
            Assert.NotEmpty(credential.PasswordHash);
            Assert.NotEqual("NewPassword123", credential.PasswordHash); // should be hashed, not plaintext

            var passwordProvider = await _db.AuthProviders
                .FirstOrDefaultAsync(p => p.UserId == user.Id && p.Provider == AuthProviderType.Password);
            Assert.NotNull(passwordProvider);
        }

        [Fact]
        public async Task AddPassword_ToUserWithExistingPassword_ThrowsInvalidOperation()
        {
            var user = new User { DisplayName = "PasswordUser" };
            _db.Users.Add(user);
            _db.PasswordCredentials.Add(new PasswordCredential
            {
                UserId = user.Id,
                PasswordHash = "existing-salt.existing-hash"
            });
            await _db.SaveChangesAsync();

            await Assert.ThrowsAsync<BusinessException>(
                () => _sut.AddPasswordAsync(user.Id, "NewPassword123"));
        }

        [Fact]
        public async Task AddPassword_ToNonExistentUser_ThrowsInvalidOperation()
        {
            var nonExistentUserId = Guid.NewGuid();

            await Assert.ThrowsAsync<BusinessException>(
                () => _sut.AddPasswordAsync(nonExistentUserId, "NewPassword123"));
        }
    }
}