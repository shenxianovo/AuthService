using AuthService.Data;
using AuthService.Configuration;
using AuthService.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;

namespace AuthService.Tests.Unit.Services
{
    public class SessionServiceTests : IDisposable
    {
        private readonly AppDbContext _db;
        private readonly Mock<IJwtService> _jwtServiceMock;
        private readonly SessionService _sut;

        public SessionServiceTests()
        {
            var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            _db = new AppDbContext(dbOptions);

            _jwtServiceMock = new Mock<IJwtService>();
            _jwtServiceMock
                .Setup(j => j.GenerateAccessToken(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Returns("fake-access-token");

            var jwtOptions = Options.Create(new JwtOptions
            {
                AccessTokenExpirationMinutes = 15,
                RefreshTokenExpirationDays = 30,
                SessionExpirationDays = 30,
            });

            _sut = new SessionService(_db, _jwtServiceMock.Object, jwtOptions);
        }

        public void Dispose() => _db.Dispose();

        [Fact]
        public async Task CreateSession_CreatesSessionAndRefreshToken()
        {
            var userId = Guid.NewGuid();

            var result = await _sut.CreateSessionAsync(userId, "127.0.0.1", "TestAgent");

            Assert.True(result.IsSuccess);
            Assert.Equal(userId, result.Value.UserId);
            Assert.Equal("fake-access-token", result.Value.AccessToken);
            Assert.NotEmpty(result.Value.RefreshToken);

            var session = await _db.Sessions.FirstOrDefaultAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(session);
            Assert.Equal(userId, session.UserId);
            Assert.Equal("127.0.0.1", session.IpAddress);
            Assert.Equal("TestAgent", session.Device);
            Assert.False(session.Revoked);
            Assert.True(session.ExpiresAt > DateTimeOffset.UtcNow);

            var refreshToken = await _db.RefreshTokens.FirstOrDefaultAsync(TestContext.Current.CancellationToken);
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

            await _sut.CreateSessionAsync(userId, "127.0.0.1", "TestAgent");

            _jwtServiceMock.Verify(
                j => j.GenerateAccessToken(userId, It.IsAny<Guid>()),
                Times.Once);
        }

        [Fact]
        public async Task CreateSession_RefreshTokenHashDiffersFromRawToken()
        {
            var userId = Guid.NewGuid();

            var result = await _sut.CreateSessionAsync(userId, "127.0.0.1", "TestAgent");

            var refreshToken = await _db.RefreshTokens.FirstOrDefaultAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(refreshToken);

            // The stored hash should NOT equal the raw token
            Assert.NotEqual(result.Value.RefreshToken, refreshToken.TokenHash);
        }

        [Fact]
        public async Task CreateSession_ExpiresAtIsInTheFuture()
        {
            var userId = Guid.NewGuid();
            var before = DateTimeOffset.UtcNow;

            var result = await _sut.CreateSessionAsync(userId, "127.0.0.1", "TestAgent");

            Assert.True(result.Value.ExpiresAt > before);
            // Should be approximately AccessTokenExpirationMinutes (15) in the future
            Assert.True(result.Value.ExpiresAt < before.AddMinutes(16));
        }

        [Fact]
        public async Task CreateSession_MultipleCallsCreateSeparateSessions()
        {
            var userId = Guid.NewGuid();

            await _sut.CreateSessionAsync(userId, "127.0.0.1", "Device1");
            await _sut.CreateSessionAsync(userId, "192.168.1.1", "Device2");

            var sessions = await _db.Sessions.ToListAsync(TestContext.Current.CancellationToken);
            Assert.Equal(2, sessions.Count);
            Assert.Contains(sessions, s => s.Device == "Device1");
            Assert.Contains(sessions, s => s.Device == "Device2");

            var tokens = await _db.RefreshTokens.ToListAsync(TestContext.Current.CancellationToken);
            Assert.Equal(2, tokens.Count);
        }
    }
}