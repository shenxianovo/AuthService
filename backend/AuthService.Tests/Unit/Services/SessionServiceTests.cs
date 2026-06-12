using AuthService.Common;
using AuthService.Configuration;
using AuthService.Entities;
using AuthService.Services;
using AuthService.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using System.Security.Claims;
using System.Security.Cryptography;

namespace AuthService.Tests.Unit.Services
{
    public class SessionServiceTests : DbTestBase
    {
        private readonly Mock<IJwtService> _jwtServiceMock;
        private readonly SessionService _sut;

        public SessionServiceTests()
        {
            _jwtServiceMock = new Mock<IJwtService>();
            _jwtServiceMock
                .Setup(j => j.GenerateAccessToken(It.IsAny<Guid>(), It.IsAny<Claim[]>()))
                .Returns("fake-access-token");

            var jwtOptions = Options.Create(new JwtOptions
            {
                AccessTokenExpirationMinutes = 15,
                RefreshTokenExpirationDays = 30,
                SessionExpirationDays = 30,
            });

            _sut = new SessionService(Db, _jwtServiceMock.Object, jwtOptions);
        }

        [Fact]
        public async Task CreateSession_CreatesSessionAndRefreshToken()
        {
            var user = new User { Username = "tester", DisplayName = "Test" };
            Db.Users.Add(user);
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
            var userId = user.Id;

            var result = await _sut.CreateSessionAsync(user, "127.0.0.1", "TestAgent");

            Assert.True(result.IsSuccess);
            Assert.Equal(userId, result.Value.UserId);
            Assert.Equal("fake-access-token", result.Value.AccessToken);
            Assert.NotEmpty(result.Value.RefreshToken);

            var session = await Db.Sessions.FirstOrDefaultAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(session);
            Assert.Equal(userId, session.UserId);
            Assert.Equal("127.0.0.1", session.IpAddress);
            Assert.Equal("TestAgent", session.Device);
            Assert.False(session.Revoked);
            Assert.True(session.ExpiresAt > DateTimeOffset.UtcNow);

            var refreshToken = await Db.RefreshTokens.FirstOrDefaultAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(refreshToken);
            Assert.Equal(session.Id, refreshToken.SessionId);
            Assert.NotEmpty(refreshToken.TokenHash);
            Assert.False(refreshToken.Revoked);
            Assert.True(refreshToken.ExpiresAt > DateTimeOffset.UtcNow);
        }

        [Fact]
        public async Task CreateSession_CallsJwtServiceWithCorrectParameters()
        {
            var userId = Guid.NewGuid();

            await _sut.CreateSessionAsync(new User { Id = userId, Username = "tester" }, "127.0.0.1", "TestAgent");

            _jwtServiceMock.Verify(
                j => j.GenerateAccessToken(userId, It.IsAny<Claim[]>()),
                Times.Once);
        }

        [Fact]
        public async Task CreateSession_RefreshTokenHashDiffersFromRawToken()
        {
            var user = new User { Username = "tester", DisplayName = "Test" };
            Db.Users.Add(user);
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.CreateSessionAsync(user, "127.0.0.1", "TestAgent");

            var refreshToken = await Db.RefreshTokens.FirstOrDefaultAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(refreshToken);

            // The stored hash should NOT equal the raw token
            Assert.NotEqual(result.Value.RefreshToken, refreshToken.TokenHash);
        }

        [Fact]
        public async Task CreateSession_ExpiresAtIsInTheFuture()
        {
            var userId = Guid.NewGuid();
            var before = DateTimeOffset.UtcNow;

            var result = await _sut.CreateSessionAsync(new User { Id = userId, Username = "tester" }, "127.0.0.1", "TestAgent");

            Assert.True(result.Value.ExpiresAt > before);
            // Should be approximately AccessTokenExpirationMinutes (15) in the future
            Assert.True(result.Value.ExpiresAt < before.AddMinutes(16));
        }

        [Fact]
        public async Task CreateSession_MultipleCallsCreateSeparateSessions()
        {
            var user = new User { Username = "tester", DisplayName = "Test" };
            Db.Users.Add(user);
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            await _sut.CreateSessionAsync(user, "127.0.0.1", "Device1");
            await _sut.CreateSessionAsync(user, "192.168.1.1", "Device2");

            var sessions = await Db.Sessions.ToListAsync(TestContext.Current.CancellationToken);
            Assert.Equal(2, sessions.Count);
            Assert.Contains(sessions, s => s.Device == "Device1");
            Assert.Contains(sessions, s => s.Device == "Device2");

            var tokens = await Db.RefreshTokens.ToListAsync(TestContext.Current.CancellationToken);
            Assert.Equal(2, tokens.Count);
        }

        // ==================== RefreshSessionAsync ====================

        [Fact]
        public async Task Refresh_WithValidToken_ReturnsNewTokensAndRotates()
        {
            var userId = Guid.NewGuid();
            Db.Users.Add(new User { Id = userId, Username = "tester", DisplayName = "Test" });
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var createResult = await _sut.CreateSessionAsync(new User { Id = userId, Username = "tester" }, "127.0.0.1", "TestAgent");
            var oldRefreshToken = createResult.Value.RefreshToken;

            var refreshResult = await _sut.RefreshSessionAsync(oldRefreshToken);

            Assert.True(refreshResult.IsSuccess);
            Assert.Equal(userId, refreshResult.Value.UserId);
            Assert.Equal("fake-access-token", refreshResult.Value.AccessToken);
            Assert.NotEmpty(refreshResult.Value.RefreshToken);
            Assert.NotEqual(oldRefreshToken, refreshResult.Value.RefreshToken);
        }

        [Fact]
        public async Task Refresh_OldTokenIsRevokedAfterRotation()
        {
            var userId = Guid.NewGuid();
            Db.Users.Add(new User { Id = userId, Username = "tester", DisplayName = "Test" });
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var createResult = await _sut.CreateSessionAsync(new User { Id = userId, Username = "tester" }, "127.0.0.1", "TestAgent");
            var oldRefreshToken = createResult.Value.RefreshToken;

            await _sut.RefreshSessionAsync(oldRefreshToken);

            // Old token should be revoked
            var tokens = await Db.RefreshTokens.ToListAsync(TestContext.Current.CancellationToken);
            Assert.Equal(2, tokens.Count);
            Assert.Contains(tokens, t => t.Revoked);
            Assert.Contains(tokens, t => !t.Revoked);
        }

        [Fact]
        public async Task Refresh_ReplayingOldToken_Fails()
        {
            var userId = Guid.NewGuid();
            Db.Users.Add(new User { Id = userId, Username = "tester", DisplayName = "Test" });
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var createResult = await _sut.CreateSessionAsync(new User { Id = userId, Username = "tester" }, "127.0.0.1", "TestAgent");
            var oldRefreshToken = createResult.Value.RefreshToken;

            // First refresh succeeds
            var first = await _sut.RefreshSessionAsync(oldRefreshToken);
            Assert.True(first.IsSuccess);

            // Replaying the same old token must fail
            var second = await _sut.RefreshSessionAsync(oldRefreshToken);
            Assert.False(second.IsSuccess);
            Assert.Equal(AuthError.InvalidRefreshToken, second.Error);
        }

        [Fact]
        public async Task Refresh_WithRevokedSession_Fails()
        {
            var userId = Guid.NewGuid();
            Db.Users.Add(new User { Id = userId, Username = "tester", DisplayName = "Test" });
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var createResult = await _sut.CreateSessionAsync(new User { Id = userId, Username = "tester" }, "127.0.0.1", "TestAgent");
            var refreshToken = createResult.Value.RefreshToken;

            // Revoke the session
            var session = await Db.Sessions.FirstAsync(TestContext.Current.CancellationToken);
            await _sut.RevokeSessionAsync(session.Id);

            var result = await _sut.RefreshSessionAsync(refreshToken);

            Assert.False(result.IsSuccess);
            Assert.Equal(AuthError.InvalidRefreshToken, result.Error);
        }

        [Fact]
        public async Task Refresh_WithExpiredToken_Fails()
        {
            var userId = Guid.NewGuid();
            Db.Users.Add(new User { Id = userId, Username = "tester", DisplayName = "Test" });
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var createResult = await _sut.CreateSessionAsync(new User { Id = userId, Username = "tester" }, "127.0.0.1", "TestAgent");
            var refreshToken = createResult.Value.RefreshToken;

            // Manually expire the token
            var storedToken = await Db.RefreshTokens.FirstAsync(TestContext.Current.CancellationToken);
            storedToken.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.RefreshSessionAsync(refreshToken);

            Assert.False(result.IsSuccess);
            Assert.Equal(AuthError.InvalidRefreshToken, result.Error);
        }

        [Fact]
        public async Task Refresh_WithExpiredSession_Fails()
        {
            var userId = Guid.NewGuid();
            Db.Users.Add(new User { Id = userId, Username = "tester", DisplayName = "Test" });
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var createResult = await _sut.CreateSessionAsync(new User { Id = userId, Username = "tester" }, "127.0.0.1", "TestAgent");
            var refreshToken = createResult.Value.RefreshToken;

            // Manually expire the session
            var session = await Db.Sessions.FirstAsync(TestContext.Current.CancellationToken);
            session.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.RefreshSessionAsync(refreshToken);

            Assert.False(result.IsSuccess);
            Assert.Equal(AuthError.InvalidRefreshToken, result.Error);
        }

        [Fact]
        public async Task Refresh_WithDeletedUser_Fails()
        {
            var userId = Guid.NewGuid();
            var user = new User { Id = userId, Username = "tester", DisplayName = "Test" };
            Db.Users.Add(user);
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var createResult = await _sut.CreateSessionAsync(new User { Id = userId, Username = "tester" }, "127.0.0.1", "TestAgent");
            var refreshToken = createResult.Value.RefreshToken;

            // Mark user as deleted
            user.IsDeleted = true;
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await _sut.RefreshSessionAsync(refreshToken);

            Assert.False(result.IsSuccess);
            Assert.Equal(AuthError.InvalidRefreshToken, result.Error);
        }

        [Fact]
        public async Task Refresh_WithNonExistentToken_Fails()
        {
            var fakeToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

            var result = await _sut.RefreshSessionAsync(fakeToken);

            Assert.False(result.IsSuccess);
            Assert.Equal(AuthError.InvalidRefreshToken, result.Error);
        }

        // ==================== RevokeSessionAsync ====================

        [Fact]
        public async Task Revoke_MarksSessionAsRevoked()
        {
            var userId = Guid.NewGuid();
            Db.Users.Add(new User { Id = userId, Username = "tester", DisplayName = "Test" });
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var createResult = await _sut.CreateSessionAsync(new User { Id = userId, Username = "tester" }, "127.0.0.1", "TestAgent");

            var session = await Db.Sessions.FirstAsync(TestContext.Current.CancellationToken);
            await _sut.RevokeSessionAsync(session.Id);

            var revokedSession = await Db.Sessions.FirstAsync(TestContext.Current.CancellationToken);
            Assert.True(revokedSession.Revoked);
        }

        [Fact]
        public async Task Revoke_RevokesAllRefreshTokensInSession()
        {
            var userId = Guid.NewGuid();
            Db.Users.Add(new User { Id = userId, Username = "tester", DisplayName = "Test" });
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var createResult = await _sut.CreateSessionAsync(new User { Id = userId, Username = "tester" }, "127.0.0.1", "TestAgent");

            // Refresh once to have 2 tokens (one revoked by rotation, one active)
            await _sut.RefreshSessionAsync(createResult.Value.RefreshToken);

            var session = await Db.Sessions.FirstAsync(TestContext.Current.CancellationToken);
            await _sut.RevokeSessionAsync(session.Id);

            var allTokens = await Db.RefreshTokens.ToListAsync(TestContext.Current.CancellationToken);
            Assert.All(allTokens, t => Assert.True(t.Revoked));
        }

        [Fact]
        public async Task Revoke_NonExistentSession_DoesNothing()
        {
            // Should not throw
            await _sut.RevokeSessionAsync(Guid.NewGuid());

            var sessions = await Db.Sessions.CountAsync(TestContext.Current.CancellationToken);
            Assert.Equal(0, sessions);
        }

        [Fact]
        public async Task Revoke_AlreadyRevokedSession_DoesNothing()
        {
            var userId = Guid.NewGuid();
            Db.Users.Add(new User { Id = userId, Username = "tester", DisplayName = "Test" });
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            await _sut.CreateSessionAsync(new User { Id = userId, Username = "tester" }, "127.0.0.1", "TestAgent");

            var session = await Db.Sessions.FirstAsync(TestContext.Current.CancellationToken);
            await _sut.RevokeSessionAsync(session.Id);
            await _sut.RevokeSessionAsync(session.Id); // second call

            // Should still be revoked, no exceptions
            var revokedSession = await Db.Sessions.FirstAsync(TestContext.Current.CancellationToken);
            Assert.True(revokedSession.Revoked);
        }
    }
}