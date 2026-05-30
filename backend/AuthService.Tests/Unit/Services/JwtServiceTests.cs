using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AuthService.Configuration;
using AuthService.Services;
using AuthService.Tests.Fixtures;
using Microsoft.Extensions.Options;

namespace AuthService.Tests.Unit.Services
{
    public class JwtServiceTests
    {
        private readonly JwtService _sut;
        private readonly JwtOptions _options;

        public JwtServiceTests()
        {
            _options = new JwtOptions
            {
                PrivateKeyPath = "unused",
                PublicKeyPath = "unused",
                Issuer = "test-issuer",
                Audience = "test-audience",
                AccessTokenExpirationMinutes = 15,
                RefreshTokenExpirationDays = 30,
                SessionExpirationDays = 30,
            };

            _sut = new JwtService(Options.Create(_options), new InMemoryRsaKeyProvider());
        }

        [Fact]
        public void GenerateAccessToken_ReturnsNonEmptyToken()
        {
            var userId = Guid.NewGuid();
            var sessionId = Guid.NewGuid();

            var token = _sut.GenerateAccessToken(userId, new Claim("sid", sessionId.ToString()));

            Assert.NotNull(token);
            Assert.NotEmpty(token);
        }

        [Fact]
        public void GenerateAccessToken_ContainsCorrectClaims()
        {
            var userId = Guid.NewGuid();
            var sessionId = Guid.NewGuid();

            var token = _sut.GenerateAccessToken(userId, new Claim("sid", sessionId.ToString()));

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
            var token = _sut.GenerateAccessToken(Guid.NewGuid(), new Claim("sid", Guid.NewGuid().ToString()));

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

            var token = _sut.GenerateAccessToken(userId, new Claim("sid", sessionId.ToString()));
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
            // A service with a different key pair must reject this service's tokens.
            var otherService = new JwtService(Options.Create(_options), new InMemoryRsaKeyProvider());
            var tokenFromOther = otherService.GenerateAccessToken(Guid.NewGuid(), new Claim("sid", Guid.NewGuid().ToString()));

            var result = _sut.ValidateTokenAndGetUserId(tokenFromOther);

            Assert.Null(result);
        }

        [Fact]
        public void GenerateAccessToken_ProducesUniqueJtiEachTime()
        {
            var userId = Guid.NewGuid();
            var sessionId = Guid.NewGuid();

            var token1 = _sut.GenerateAccessToken(userId, new Claim("sid", sessionId.ToString()));
            var token2 = _sut.GenerateAccessToken(userId, new Claim("sid", sessionId.ToString()));

            var handler = new JwtSecurityTokenHandler();
            var jti1 = handler.ReadJwtToken(token1).Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
            var jti2 = handler.ReadJwtToken(token2).Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;

            Assert.NotEqual(jti1, jti2);
        }
    }
}
