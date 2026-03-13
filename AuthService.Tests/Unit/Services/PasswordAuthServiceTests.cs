using AuthService.Data;
using AuthService.DTOs.Auth;
using AuthService.Entities;
using AuthService.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;

namespace AuthService.Tests.Unit.Services
{
    public class PasswordAuthServiceTests : IDisposable
    {
        private readonly AppDbContext _db;
        private readonly Mock<IJwtService> _jwtServiceMock;
        private readonly IOptions<JwtOptions> _jwtOptions;
        private readonly PasswordAuthService _sut;

        public PasswordAuthServiceTests()
        {
            var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            _db = new AppDbContext(dbOptions);

            _jwtServiceMock = new Mock<IJwtService>();
            _jwtServiceMock
                .Setup(j => j.GenerateAccessToken(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Returns("fake-access-token");

            _jwtOptions = Options.Create(new JwtOptions
            {
                AccessTokenExpirationMinutes = 15,
                RefreshTokenExpirationDays = 30,
                SessionExpirationDays = 30,
            });

            _sut = new PasswordAuthService(_db, _jwtServiceMock.Object, _jwtOptions);
        }

        public void Dispose() => _db.Dispose();

        // --- Register ---

        [Fact]
        public async Task Register_WithValidData_CreatesUserAndReturnsTokens()
        {
            var request = new RegisterRequest
            {
                DisplayName = "Test User",
                Email = "test@example.com",
                Password = "SecurePass123",
            };

            var result = await _sut.RegisterAsync(request, "127.0.0.1", "TestAgent");

            Assert.NotNull(result);
            Assert.Equal("fake-access-token", result.AccessToken);
            Assert.NotEmpty(result.RefreshToken);
            Assert.NotEqual(Guid.Empty, result.UserId);

            // Verify database records
            var user = await _db.Users.FirstOrDefaultAsync();
            Assert.NotNull(user);
            Assert.Equal("Test User", user.DisplayName);

            var email = await _db.UserEmails.FirstOrDefaultAsync();
            Assert.NotNull(email);
            Assert.Equal("test@example.com", email.Email);
            Assert.True(email.IsPrimary);

            var credential = await _db.PasswordCredentials.FirstOrDefaultAsync();
            Assert.NotNull(credential);
            Assert.Contains(".", credential.PasswordHash); // salt.hash format

            var provider = await _db.AuthProviders.FirstOrDefaultAsync();
            Assert.NotNull(provider);
            Assert.Equal(AuthProviderType.Password, provider.Provider);

            var session = await _db.Sessions.FirstOrDefaultAsync();
            Assert.NotNull(session);
            Assert.Equal("127.0.0.1", session.IpAddress);
            Assert.Equal("TestAgent", session.Device);

            var refreshToken = await _db.RefreshTokens.FirstOrDefaultAsync();
            Assert.NotNull(refreshToken);
            Assert.False(refreshToken.Revoked);
        }

        [Fact]
        public async Task Register_NormalizesEmailToLowerCase()
        {
            var request = new RegisterRequest
            {
                DisplayName = "Test",
                Email = "Test@EXAMPLE.COM",
                Password = "SecurePass123",
            };

            await _sut.RegisterAsync(request, "127.0.0.1", "TestAgent");

            var email = await _db.UserEmails.FirstOrDefaultAsync();
            Assert.NotNull(email);
            Assert.Equal("test@example.com", email.Email);
        }

        [Fact]
        public async Task Register_WithDuplicateEmail_ThrowsInvalidOperation()
        {
            var request = new RegisterRequest
            {
                DisplayName = "Test",
                Email = "test@example.com",
                Password = "SecurePass123",
            };

            await _sut.RegisterAsync(request, "127.0.0.1", "TestAgent");

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _sut.RegisterAsync(request, "127.0.0.1", "TestAgent"));
        }

        // --- Login ---

        [Fact]
        public async Task Login_WithValidCredentials_ReturnsTokens()
        {
            // Arrange: register first
            var registerRequest = new RegisterRequest
            {
                DisplayName = "Test",
                Email = "test@example.com",
                Password = "SecurePass123",
            };
            await _sut.RegisterAsync(registerRequest, "127.0.0.1", "TestAgent");

            // Act: login
            var loginRequest = new LoginRequest
            {
                Email = "test@example.com",
                Password = "SecurePass123",
            };
            var result = await _sut.LoginAsync(loginRequest, "192.168.1.1", "AnotherAgent");

            Assert.NotNull(result);
            Assert.Equal("fake-access-token", result.AccessToken);
            Assert.NotEmpty(result.RefreshToken);

            // Should have 2 sessions now
            var sessions = await _db.Sessions.CountAsync();
            Assert.Equal(2, sessions);
        }

        [Fact]
        public async Task Login_WithWrongPassword_ThrowsUnauthorized()
        {
            var registerRequest = new RegisterRequest
            {
                DisplayName = "Test",
                Email = "test@example.com",
                Password = "SecurePass123",
            };
            await _sut.RegisterAsync(registerRequest, "127.0.0.1", "TestAgent");

            var loginRequest = new LoginRequest
            {
                Email = "test@example.com",
                Password = "WrongPassword",
            };

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => _sut.LoginAsync(loginRequest, "127.0.0.1", "TestAgent"));
        }

        [Fact]
        public async Task Login_WithNonExistentEmail_ThrowsUnauthorized()
        {
            var loginRequest = new LoginRequest
            {
                Email = "nobody@example.com",
                Password = "Whatever123",
            };

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => _sut.LoginAsync(loginRequest, "127.0.0.1", "TestAgent"));
        }

        [Fact]
        public async Task Login_WithDeletedUser_ThrowsUnauthorized()
        {
            var registerRequest = new RegisterRequest
            {
                DisplayName = "Test",
                Email = "test@example.com",
                Password = "SecurePass123",
            };
            var registerResult = await _sut.RegisterAsync(registerRequest, "127.0.0.1", "TestAgent");

            // Soft delete the user
            var user = await _db.Users.FindAsync(registerResult.UserId);
            user!.IsDeleted = true;
            await _db.SaveChangesAsync();

            var loginRequest = new LoginRequest
            {
                Email = "test@example.com",
                Password = "SecurePass123",
            };

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => _sut.LoginAsync(loginRequest, "127.0.0.1", "TestAgent"));
        }

        [Fact]
        public async Task Login_IsCaseInsensitiveForEmail()
        {
            var registerRequest = new RegisterRequest
            {
                DisplayName = "Test",
                Email = "test@example.com",
                Password = "SecurePass123",
            };
            await _sut.RegisterAsync(registerRequest, "127.0.0.1", "TestAgent");

            var loginRequest = new LoginRequest
            {
                Email = "TEST@EXAMPLE.COM",
                Password = "SecurePass123",
            };
            var result = await _sut.LoginAsync(loginRequest, "127.0.0.1", "TestAgent");

            Assert.NotNull(result);
            Assert.NotEmpty(result.AccessToken);
        }
    }
}
