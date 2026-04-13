using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using AuthService.Services;
using Microsoft.Extensions.Options;

namespace AuthService.Tests.Unit.Services
{
    public class JwtServiceTests : IDisposable
    {
        private readonly JwtService _sut;
        private readonly string _tempKeyDir;
        private readonly JwtOptions _options;

        public JwtServiceTests()
        {
            _tempKeyDir = Path.Combine(Path.GetTempPath(), $"jwt-test-keys-{Guid.NewGuid()}");
            Directory.CreateDirectory(_tempKeyDir);

            using var rsa = RSA.Create(2048);
            var privateKeyPath = Path.Combine(_tempKeyDir, "private.pem");
            var publicKeyPath = Path.Combine(_tempKeyDir, "public.pem");
            File.WriteAllText(privateKeyPath, rsa.ExportRSAPrivateKeyPem());
            File.WriteAllText(publicKeyPath, rsa.ExportRSAPublicKeyPem());

            _options = new JwtOptions
            {
                PrivateKeyPath = privateKeyPath,
                PublicKeyPath = publicKeyPath,
                Issuer = "test-issuer",
                Audience = "test-audience",
                AccessTokenExpirationMinutes = 15,
                RefreshTokenExpirationDays = 30,
                SessionExpirationDays = 30,
            };

            _sut = new JwtService(Options.Create(_options));
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempKeyDir))
                Directory.Delete(_tempKeyDir, recursive: true);
        }

        [Fact]
        public void GenerateAccessToken_ReturnsNonEmptyToken()
        {
            var userId = Guid.NewGuid();
            var sessionId = Guid.NewGuid();

            var token = _sut.GenerateAccessToken(userId, sessionId);

            Assert.NotNull(token);
            Assert.NotEmpty(token);
        }

        [Fact]
        public void GenerateAccessToken_ContainsCorrectClaims()
        {
            var userId = Guid.NewGuid();
            var sessionId = Guid.NewGuid();

            var token = _sut.GenerateAccessToken(userId, sessionId);

            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);

            Assert.Equal(_options.Issuer, jwtToken.Issuer);
            Assert.Contains(_options.Audience, jwtToken.Audiences);
            Assert.Equal(userId.ToString(), jwtToken.Subject);

            var sidClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "sid");
            Assert.NotNull(sidClaim);
            Assert.Equal(sessionId.ToString(), sidClaim.Value);

            var jtiClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti);
            Assert.NotNull(jtiClaim);
            Assert.NotEmpty(jtiClaim.Value);
        }

        [Fact]
        public void GenerateAccessToken_HasCorrectExpiration()
        {
            var token = _sut.GenerateAccessToken(Guid.NewGuid(), Guid.NewGuid());

            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);

            var expectedExpiry = DateTime.UtcNow.AddMinutes(_options.AccessTokenExpirationMinutes);
            // Allow 5 seconds tolerance
            Assert.True(jwtToken.ValidTo <= expectedExpiry.AddSeconds(5));
            Assert.True(jwtToken.ValidTo >= expectedExpiry.AddSeconds(-5));
        }

        [Fact]
        public void GetPublicKey_ReturnsNonNullKey()
        {
            var key = _sut.GetPublicKey();

            Assert.NotNull(key);
        }

        [Fact]
        public void ValidateTokenAndGetUserId_WithValidToken_ReturnsUserId()
        {
            var userId = Guid.NewGuid();
            var sessionId = Guid.NewGuid();

            var token = _sut.GenerateAccessToken(userId, sessionId);
            var result = _sut.ValidateTokenAndGetUserId(token);

            Assert.NotNull(result);
            Assert.Equal(userId, result.Value);
        }

        [Fact]
        public void ValidateTokenAndGetUserId_WithInvalidToken_ReturnsNull()
        {
            var result = _sut.ValidateTokenAndGetUserId("invalid.jwt.token");

            Assert.Null(result);
        }

        [Fact]
        public void ValidateTokenAndGetUserId_WithTokenFromDifferentKey_ReturnsNull()
        {
            // Create another JwtService with different keys
            var otherKeyDir = Path.Combine(Path.GetTempPath(), $"jwt-test-keys-other-{Guid.NewGuid()}");
            Directory.CreateDirectory(otherKeyDir);

            using var rsa = RSA.Create(2048);
            var otherPrivateKeyPath = Path.Combine(otherKeyDir, "private.pem");
            var otherPublicKeyPath = Path.Combine(otherKeyDir, "public.pem");
            File.WriteAllText(otherPrivateKeyPath, rsa.ExportRSAPrivateKeyPem());
            File.WriteAllText(otherPublicKeyPath, rsa.ExportRSAPublicKeyPem());

            var otherOptions = new JwtOptions
            {
                PrivateKeyPath = otherPrivateKeyPath,
                PublicKeyPath = otherPublicKeyPath,
                Issuer = "test-issuer",
                Audience = "test-audience",
                AccessTokenExpirationMinutes = 15,
            };

            var otherService = new JwtService(Options.Create(otherOptions));
            var tokenFromOther = otherService.GenerateAccessToken(Guid.NewGuid(), Guid.NewGuid());

            // Validate with original service's key — should fail
            var result = _sut.ValidateTokenAndGetUserId(tokenFromOther);

            Assert.Null(result);

            Directory.Delete(otherKeyDir, recursive: true);
        }

        [Fact]
        public void GenerateAccessToken_ProducesUniqueJtiEachTime()
        {
            var userId = Guid.NewGuid();
            var sessionId = Guid.NewGuid();

            var token1 = _sut.GenerateAccessToken(userId, sessionId);
            var token2 = _sut.GenerateAccessToken(userId, sessionId);

            var handler = new JwtSecurityTokenHandler();
            var jti1 = handler.ReadJwtToken(token1).Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
            var jti2 = handler.ReadJwtToken(token2).Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;

            Assert.NotEqual(jti1, jti2);
        }
    }
}
