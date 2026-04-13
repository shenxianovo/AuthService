using AuthService.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace AuthService.Tests.Unit.Services
{
    public class OAuthSecurityServiceTests : IDisposable
    {
        private readonly OAuthSecurityService _sut;
        private readonly IMemoryCache _cache;

        public OAuthSecurityServiceTests()
        {
            var dataProtectionProvider = DataProtectionProvider.Create("TestApp");
            _cache = new MemoryCache(new MemoryCacheOptions());

            var options = Options.Create(new OAuthSecurityOptions
            {
                AllowedRedirectOrigins =
                [
                    "https://example.com",
                    "https://*.shenxianovo.com",
                    "http://localhost:3000",
                ],
                AuthCodeExpirationSeconds = 60,
                StateExpirationSeconds = 600,
            });

            _sut = new OAuthSecurityService(dataProtectionProvider, _cache, options);
        }

        public void Dispose() => _cache.Dispose();

        // ===================== State Generation & Validation =====================

        [Fact]
        public void GenerateState_ReturnsNonEmptyString()
        {
            var state = _sut.GenerateState("https://example.com/callback", null);

            Assert.NotNull(state);
            Assert.NotEmpty(state);
        }

        [Fact]
        public void ValidateState_WithValidState_ReturnsPayload()
        {
            var redirectUrl = "https://example.com/callback";
            var userId = Guid.NewGuid();
            var state = _sut.GenerateState(redirectUrl, userId);

            var payload = _sut.ValidateState(state);

            Assert.NotNull(payload);
            Assert.Equal(redirectUrl, payload.RedirectUrl);
            Assert.Equal(userId, payload.UserId);
            Assert.NotEmpty(payload.Nonce);
        }

        [Fact]
        public void ValidateState_WithNullRedirectAndUser_ReturnsPayloadWithNulls()
        {
            var state = _sut.GenerateState(null, null);

            var payload = _sut.ValidateState(state);

            Assert.NotNull(payload);
            Assert.Null(payload.RedirectUrl);
            Assert.Null(payload.UserId);
        }

        [Fact]
        public void ValidateState_WithTamperedState_ReturnsNull()
        {
            var state = _sut.GenerateState("https://example.com", null);
            var tampered = state + "tampered";

            var payload = _sut.ValidateState(tampered);

            Assert.Null(payload);
        }

        [Fact]
        public void ValidateState_WithGarbageString_ReturnsNull()
        {
            var payload = _sut.ValidateState("totally-not-a-valid-state");

            Assert.Null(payload);
        }

        [Fact]
        public void ValidateState_WithExpiredState_ReturnsNull()
        {
            // Create a service with very short state expiration (1 second)
            var shortLivedOptions = Options.Create(new OAuthSecurityOptions
            {
                StateExpirationSeconds = 1,
            });
            var shortLivedService = new OAuthSecurityService(
                DataProtectionProvider.Create("TestApp"), _cache, shortLivedOptions);

            var state = shortLivedService.GenerateState("https://example.com", null);

            // Wait long enough to ensure the 1-second expiration has passed
            // (using 2100ms to account for second-level timestamp truncation)
            Thread.Sleep(2100);
            var payload = shortLivedService.ValidateState(state);

            Assert.Null(payload);
        }

        // ===================== Redirect URL Validation =====================

        [Fact]
        public void ValidateRedirectUrl_WithNullUrl_DoesNotThrow()
        {
            var ex = Record.Exception(() => _sut.ValidateRedirectUrl(null));
            Assert.Null(ex);
        }

        [Fact]
        public void ValidateRedirectUrl_WithEmptyUrl_DoesNotThrow()
        {
            var ex = Record.Exception(() => _sut.ValidateRedirectUrl(""));
            Assert.Null(ex);
        }

        [Fact]
        public void ValidateRedirectUrl_WithAllowedExactOrigin_DoesNotThrow()
        {
            var ex = Record.Exception(() => _sut.ValidateRedirectUrl("https://example.com/some/path"));
            Assert.Null(ex);
        }

        [Fact]
        public void ValidateRedirectUrl_WithAllowedWildcardSubdomain_DoesNotThrow()
        {
            var ex = Record.Exception(() => _sut.ValidateRedirectUrl("https://app.shenxianovo.com/callback"));
            Assert.Null(ex);
        }

        [Fact]
        public void ValidateRedirectUrl_WithAllowedBaseDomainOfWildcard_DoesNotThrow()
        {
            var ex = Record.Exception(() => _sut.ValidateRedirectUrl("https://shenxianovo.com/callback"));
            Assert.Null(ex);
        }

        [Fact]
        public void ValidateRedirectUrl_WithAllowedLocalhostOrigin_DoesNotThrow()
        {
            var ex = Record.Exception(() => _sut.ValidateRedirectUrl("http://localhost:3000/auth/callback"));
            Assert.Null(ex);
        }

        [Fact]
        public void ValidateRedirectUrl_WithDisallowedOrigin_ThrowsInvalidOperation()
        {
            Assert.Throws<InvalidOperationException>(
                () => _sut.ValidateRedirectUrl("https://evil.com/callback"));
        }

        [Fact]
        public void ValidateRedirectUrl_WithInvalidUrl_ThrowsInvalidOperation()
        {
            Assert.Throws<InvalidOperationException>(
                () => _sut.ValidateRedirectUrl("not-a-url"));
        }

        [Fact]
        public void ValidateRedirectUrl_WithFtpScheme_ThrowsInvalidOperation()
        {
            Assert.Throws<InvalidOperationException>(
                () => _sut.ValidateRedirectUrl("ftp://example.com/file"));
        }

        // ===================== Auth Code Generation & Exchange =====================

        [Fact]
        public void GenerateAuthCode_ReturnsNonEmptyCode()
        {
            var code = _sut.GenerateAuthCode(Guid.NewGuid(), "access-token", "refresh-token", DateTimeOffset.UtcNow.AddMinutes(15));

            Assert.NotNull(code);
            Assert.NotEmpty(code);
        }

        [Fact]
        public void ExchangeAuthCode_WithValidCode_ReturnsPayload()
        {
            var userId = Guid.NewGuid();
            var accessToken = "test-access-token";
            var refreshToken = "test-refresh-token";
            var expiresAt = DateTimeOffset.UtcNow.AddMinutes(15);

            var code = _sut.GenerateAuthCode(userId, accessToken, refreshToken, expiresAt);
            var payload = _sut.ExchangeAuthCode(code);

            Assert.NotNull(payload);
            Assert.Equal(userId, payload.UserId);
            Assert.Equal(accessToken, payload.AccessToken);
            Assert.Equal(refreshToken, payload.RefreshToken);
        }

        [Fact]
        public void ExchangeAuthCode_IsOneTimeUse()
        {
            var code = _sut.GenerateAuthCode(Guid.NewGuid(), "access-token", "refresh-token", DateTimeOffset.UtcNow.AddMinutes(15));

            var first = _sut.ExchangeAuthCode(code);
            var second = _sut.ExchangeAuthCode(code);

            Assert.NotNull(first);
            Assert.Null(second);
        }

        [Fact]
        public void ExchangeAuthCode_WithInvalidCode_ReturnsNull()
        {
            var payload = _sut.ExchangeAuthCode("non-existent-code");

            Assert.Null(payload);
        }

        [Fact]
        public void ExchangeAuthCode_WithExpiredCode_ReturnsNull()
        {
            // Create a service with very short auth code expiration (1 second)
            var shortCache = new MemoryCache(new MemoryCacheOptions());
            var shortLivedOptions = Options.Create(new OAuthSecurityOptions
            {
                AuthCodeExpirationSeconds = 1,
            });
            var shortLivedService = new OAuthSecurityService(
                DataProtectionProvider.Create("TestApp"), shortCache, shortLivedOptions);

            var code = shortLivedService.GenerateAuthCode(Guid.NewGuid(), "access", "refresh", DateTimeOffset.UtcNow.AddMinutes(15));

            // Wait for expiration
            Thread.Sleep(1100);
            var payload = shortLivedService.ExchangeAuthCode(code);

            Assert.Null(payload);
            shortCache.Dispose();
        }
    }
}
