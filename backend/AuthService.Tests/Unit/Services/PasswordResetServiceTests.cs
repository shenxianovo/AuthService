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
    public class PasswordResetServiceTests : DbTestBase
    {
        private readonly Mock<IEmailService> _emailMock = new();
        private readonly PasswordHasher<User> _hasher = new();
        private readonly SessionService _sessionService;
        private readonly PasswordResetService _sut;

        private string? _lastResetUrl;

        public PasswordResetServiceTests()
        {
            var jwtOptions = Options.Create(new JwtOptions
            {
                AccessTokenExpirationMinutes = 15,
                RefreshTokenExpirationDays = 30,
                SessionExpirationDays = 30,
            });
            _sessionService = new SessionService(Db, Mock.Of<IJwtService>(), jwtOptions);

            _emailMock
                .Setup(e => e.SendPasswordResetLinkAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string, string>((_, _, url) => _lastResetUrl = url)
                .Returns(Task.CompletedTask);

            _sut = new PasswordResetService(
                Db,
                new AccountService(Db, new RecordingGrantRevoker()),
                _sessionService,
                _emailMock.Object,
                _hasher,
                Options.Create(new ResendOptions { PasswordResetExpirationMinutes = 30 }));
        }

        private async Task<User> CreateUserAsync(
            string email = "user@example.com",
            bool emailVerified = true,
            bool hasPassword = true)
        {
            var user = new User { Username = $"u{Guid.NewGuid():N}"[..12], DisplayName = "Test" };
            Db.Users.Add(user);
            Db.UserEmails.Add(new UserEmail
            {
                UserId = user.Id,
                Email = email,
                IsPrimary = true,
                VerifiedAt = emailVerified ? DateTimeOffset.UtcNow : null,
            });
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

        private string ExtractToken() =>
            Uri.UnescapeDataString(_lastResetUrl!.Split("?token=")[1]);

        // ==================== RequestResetAsync ====================

        [Fact]
        public async Task RequestReset_UnknownEmail_SendsNothing()
        {
            await _sut.RequestResetAsync("nobody@example.com");

            _emailMock.Verify(
                e => e.SendPasswordResetLinkAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
            Assert.Equal(0, await Db.PasswordResets.CountAsync(TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task RequestReset_UnverifiedEmail_SendsNothing()
        {
            await CreateUserAsync(emailVerified: false);

            await _sut.RequestResetAsync("user@example.com");

            _emailMock.Verify(
                e => e.SendPasswordResetLinkAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task RequestReset_VerifiedEmail_StoresHashAndSendsLink()
        {
            var user = await CreateUserAsync();

            await _sut.RequestResetAsync("USER@example.com"); // also exercises normalization

            var reset = await Db.PasswordResets.SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal(user.Id, reset.UserId);
            Assert.False(reset.Used);
            Assert.True(reset.ExpiresAt > DateTimeOffset.UtcNow);

            Assert.NotNull(_lastResetUrl);
            var rawToken = ExtractToken();
            Assert.NotEqual(rawToken, reset.TokenHash); // hash-only storage
        }

        [Fact]
        public async Task RequestReset_WithinRateLimitWindow_SendsOnlyOnce()
        {
            await CreateUserAsync();

            await _sut.RequestResetAsync("user@example.com");
            await _sut.RequestResetAsync("user@example.com");

            _emailMock.Verify(
                e => e.SendPasswordResetLinkAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Once);
            Assert.Equal(1, await Db.PasswordResets.CountAsync(TestContext.Current.CancellationToken));
        }

        // ==================== ResetAsync ====================

        [Fact]
        public async Task Reset_WithValidToken_SetsPasswordRevokesSessionsAndConsumesToken()
        {
            var user = await CreateUserAsync();
            await _sessionService.CreateSessionAsync(user, "127.0.0.1", "Device1");
            await _sut.RequestResetAsync("user@example.com");

            var result = await _sut.ResetAsync(ExtractToken(), "NewPassword456");

            Assert.True(result.IsSuccess);

            var credential = await Db.PasswordCredentials.SingleAsync(p => p.UserId == user.Id, TestContext.Current.CancellationToken);
            Assert.Equal(PasswordVerificationResult.Success,
                _hasher.VerifyHashedPassword(user, credential.PasswordHash, "NewPassword456"));

            Assert.True(await Db.Sessions.AllAsync(s => s.Revoked, TestContext.Current.CancellationToken));
            Assert.True(await Db.PasswordResets.AllAsync(r => r.Used, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Reset_WithGarbageToken_Fails()
        {
            var result = await _sut.ResetAsync("not-a-real-token", "NewPassword456");

            Assert.False(result.IsSuccess);
            Assert.Equal(AuthError.InvalidResetToken, result.Error);
        }

        [Fact]
        public async Task Reset_TokenCannotBeReused()
        {
            await CreateUserAsync();
            await _sut.RequestResetAsync("user@example.com");
            var token = ExtractToken();

            var first = await _sut.ResetAsync(token, "NewPassword456");
            var second = await _sut.ResetAsync(token, "EvenNewer789");

            Assert.True(first.IsSuccess);
            Assert.False(second.IsSuccess);
            Assert.Equal(AuthError.InvalidResetToken, second.Error);
        }

        [Fact]
        public async Task Reset_ExpiredToken_Fails()
        {
            await CreateUserAsync();
            await _sut.RequestResetAsync("user@example.com");
            var reset = await Db.PasswordResets.SingleAsync(TestContext.Current.CancellationToken);
            reset.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.ResetAsync(ExtractToken(), "NewPassword456");

            Assert.False(result.IsSuccess);
            Assert.Equal(AuthError.InvalidResetToken, result.Error);
        }

        [Fact]
        public async Task Reset_OAuthOnlyUser_CreatesCredentialAndPasswordProvider()
        {
            // Fork C decision: a verified mailbox proves ownership just as strongly for
            // an OAuth-only account — the reset sets a first password.
            var user = await CreateUserAsync(hasPassword: false);
            await _sut.RequestResetAsync("user@example.com");

            var result = await _sut.ResetAsync(ExtractToken(), "NewPassword456");

            Assert.True(result.IsSuccess);
            Assert.Equal(1, await Db.PasswordCredentials.CountAsync(p => p.UserId == user.Id, TestContext.Current.CancellationToken));
            Assert.Equal(1, await Db.AuthProviders.CountAsync(
                p => p.UserId == user.Id && p.Provider == AuthProviderType.Password, TestContext.Current.CancellationToken));
        }
    }
}
