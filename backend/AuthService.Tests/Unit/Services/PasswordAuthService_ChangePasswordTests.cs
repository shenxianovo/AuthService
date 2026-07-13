using AuthService.Common;
using AuthService.Configuration;
using AuthService.Entities;
using AuthService.Services;
using AuthService.Tests.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;

namespace AuthService.Tests.Unit.Services
{
    public class PasswordAuthService_ChangePasswordTests : DbTestBase
    {
        private readonly PasswordHasher<User> _hasher = new();
        private readonly SessionService _sessionService;
        private readonly PasswordAuthService _sut;

        public PasswordAuthService_ChangePasswordTests()
        {
            var jwtOptions = Options.Create(new JwtOptions
            {
                AccessTokenExpirationMinutes = 15,
                RefreshTokenExpirationDays = 30,
                SessionExpirationDays = 30,
            });
            _sessionService = new SessionService(Db, Mock.Of<IJwtService>(), jwtOptions);
            _sut = new PasswordAuthService(Db, new AccountService(Db, new RecordingGrantRevoker()), _sessionService, _hasher);
        }

        private async Task<User> CreateUserAsync(bool hasPassword = true)
        {
            var user = new User { Username = $"u{Guid.NewGuid():N}"[..12], DisplayName = "Test" };
            Db.Users.Add(user);
            if (hasPassword)
            {
                Db.PasswordCredentials.Add(new PasswordCredential
                {
                    UserId = user.Id,
                    PasswordHash = _hasher.HashPassword(null!, "OldPassword123"),
                });
            }
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
            return user;
        }

        [Fact]
        public async Task ChangePassword_WrongCurrentPassword_Fails()
        {
            var user = await CreateUserAsync();

            var result = await _sut.ChangePasswordAsync(user.Id, Guid.NewGuid(), "WrongPassword", "NewPassword456");

            Assert.False(result.IsSuccess);
            Assert.Equal(AuthError.InvalidCredentials, result.Error);
        }

        [Fact]
        public async Task ChangePassword_OAuthOnlyUser_ReturnsPasswordNotSet()
        {
            var user = await CreateUserAsync(hasPassword: false);

            var result = await _sut.ChangePasswordAsync(user.Id, Guid.NewGuid(), "anything", "NewPassword456");

            Assert.False(result.IsSuccess);
            Assert.Equal(AuthError.PasswordNotSet, result.Error);
        }

        [Fact]
        public async Task ChangePassword_Success_UpdatesHash_RevokesOtherSessions_KeepsCurrent()
        {
            var user = await CreateUserAsync();
            await _sessionService.CreateSessionAsync(user, "127.0.0.1", "Current");
            await _sessionService.CreateSessionAsync(user, "10.0.0.1", "Other");
            var currentSession = await Db.Sessions.SingleAsync(s => s.Device == "Current", TestContext.Current.CancellationToken);

            var result = await _sut.ChangePasswordAsync(user.Id, currentSession.Id, "OldPassword123", "NewPassword456");

            Assert.True(result.IsSuccess);

            var credential = await Db.PasswordCredentials.SingleAsync(p => p.UserId == user.Id, TestContext.Current.CancellationToken);
            Assert.Equal(PasswordVerificationResult.Success,
                _hasher.VerifyHashedPassword(user, credential.PasswordHash, "NewPassword456"));

            Assert.False((await Db.Sessions.SingleAsync(s => s.Device == "Current", TestContext.Current.CancellationToken)).Revoked);
            Assert.True((await Db.Sessions.SingleAsync(s => s.Device == "Other", TestContext.Current.CancellationToken)).Revoked);
        }
    }
}
