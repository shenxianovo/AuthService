using AuthService.Common;
using AuthService.Data;
using AuthService.DTOs.Auth;
using AuthService.Entities;
using AuthService.Configuration;
using AuthService.Services;
using Microsoft.AspNetCore.Identity;
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

            var sessionService = new SessionService(_db, _jwtServiceMock.Object, _jwtOptions);
            var passwordHasher = new PasswordHasher<User>();
            _sut = new PasswordAuthService(_db, sessionService, passwordHasher);
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

            Assert.True(result.IsSuccess);
            Assert.Equal("fake-access-token", result.Value.AccessToken);
            Assert.NotEmpty(result.Value.RefreshToken);
            Assert.NotEqual(Guid.Empty, result.Value.UserId);

            var user = await _db.Users.FirstOrDefaultAsync();
            Assert.NotNull(user);
            Assert.Equal("Test User", user.DisplayName);

            var email = await _db.UserEmails.FirstOrDefaultAsync();
            Assert.NotNull(email);
            Assert.Equal("test@example.com", email.Email);
            Assert.True(email.IsPrimary);

            var credential = await _db.PasswordCredentials.FirstOrDefaultAsync();
            Assert.NotNull(credential);
            Assert.NotEmpty(credential.PasswordHash);
            Assert.NotEqual("SecurePass123", credential.PasswordHash);

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
        public async Task Register_WithDuplicateEmail_ReturnsConflictError()
        {
            var request = new RegisterRequest
            {
                DisplayName = "Test",
                Email = "test@example.com",
                Password = "SecurePass123",
            };

            await _sut.RegisterAsync(request, "127.0.0.1", "TestAgent");
            var result = await _sut.RegisterAsync(request, "127.0.0.1", "TestAgent");

            Assert.False(result.IsSuccess);
            Assert.Equal(AuthError.EmailAlreadyExists, result.Error);
        }

        // --- Login ---

        [Fact]
        public async Task Login_WithValidCredentials_ReturnsTokens()
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
                Password = "SecurePass123",
            };
            var result = await _sut.LoginAsync(loginRequest, "192.168.1.1", "AnotherAgent");

            Assert.True(result.IsSuccess);
            Assert.Equal("fake-access-token", result.Value.AccessToken);
            Assert.NotEmpty(result.Value.RefreshToken);

            var sessions = await _db.Sessions.CountAsync();
            Assert.Equal(2, sessions);
        }

        [Fact]
        public async Task Login_WithWrongPassword_ReturnsInvalidCredentials()
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
            var result = await _sut.LoginAsync(loginRequest, "127.0.0.1", "TestAgent");

            Assert.False(result.IsSuccess);
            Assert.Equal(AuthError.InvalidCredentials, result.Error);
        }

        [Fact]
        public async Task Login_WithNonExistentEmail_ReturnsInvalidCredentials()
        {
            var loginRequest = new LoginRequest
            {
                Email = "nobody@example.com",
                Password = "Whatever123",
            };
            var result = await _sut.LoginAsync(loginRequest, "127.0.0.1", "TestAgent");

            Assert.False(result.IsSuccess);
            Assert.Equal(AuthError.InvalidCredentials, result.Error);
        }

        [Fact]
        public async Task Login_WithDeletedUser_ReturnsInvalidCredentials()
        {
            var registerRequest = new RegisterRequest
            {
                DisplayName = "Test",
                Email = "test@example.com",
                Password = "SecurePass123",
            };
            var registerResult = await _sut.RegisterAsync(registerRequest, "127.0.0.1", "TestAgent");

            var user = await _db.Users.FindAsync(registerResult.Value.UserId);
            user!.IsDeleted = true;
            await _db.SaveChangesAsync();

            var loginRequest = new LoginRequest
            {
                Email = "test@example.com",
                Password = "SecurePass123",
            };
            var result = await _sut.LoginAsync(loginRequest, "127.0.0.1", "TestAgent");

            Assert.False(result.IsSuccess);
            Assert.Equal(AuthError.InvalidCredentials, result.Error);
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

            Assert.True(result.IsSuccess);
            Assert.NotEmpty(result.Value.AccessToken);
        }
    }
}