using AuthService.Common;
using AuthService.Configuration;
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
        public void ValidateRedirectUrl_WithNullUrl_ReturnsSuccess()
        {
            var result = _sut.ValidateRedirectUrl(null);
            Assert.True(result.IsSuccess);
        }

        [Fact]
        public void ValidateRedirectUrl_WithEmptyUrl_ReturnsSuccess()
        {
            var result = _sut.ValidateRedirectUrl("");
            Assert.True(result.IsSuccess);
        }

        [Fact]
        public void ValidateRedirectUrl_WithAllowedExactOrigin_ReturnsSuccess()
        {
            var result = _sut.ValidateRedirectUrl("https://example.com/some/path");
            Assert.True(result.IsSuccess);
        }

        [Fact]
        public void ValidateRedirectUrl_WithAllowedWildcardSubdomain_ReturnsSuccess()
        {
            var result = _sut.ValidateRedirectUrl("https://app.shenxianovo.com/callback");
            Assert.True(result.IsSuccess);
        }

        [Fact]
        public void ValidateRedirectUrl_WithAllowedBaseDomainOfWildcard_ReturnsSuccess()
        {
            var result = _sut.ValidateRedirectUrl("https://shenxianovo.com/callback");
            Assert.True(result.IsSuccess);
        }

        [Fact]
        public void ValidateRedirectUrl_WithAllowedLocalhostOrigin_ReturnsSuccess()
        {
            var result = _sut.ValidateRedirectUrl("http://localhost:3000/auth/callback");
            Assert.True(result.IsSuccess);
        }

        [Fact]
        public void ValidateRedirectUrl_WithDisallowedOrigin_ReturnsFailure()
        {
            var result = _sut.ValidateRedirectUrl("https://evil.com/callback");
            Assert.False(result.IsSuccess);
            Assert.Equal(AuthError.InvalidRedirectUrl, result.Error);
        }

        [Fact]
        public void ValidateRedirectUrl_WithInvalidUrl_ReturnsFailure()
        {
            var result = _sut.ValidateRedirectUrl("not-a-url");
            Assert.False(result.IsSuccess);
            Assert.Equal(AuthError.InvalidRedirectUrl, result.Error);
        }

        [Fact]
        public void ValidateRedirectUrl_WithFtpScheme_ReturnsFailure()
        {
            var result = _sut.ValidateRedirectUrl("ftp://example.com/file");
            Assert.False(result.IsSuccess);
            Assert.Equal(AuthError.InvalidRedirectUrl, result.Error);
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
        public void ExchangeAuthCode_WithValidCode_ReturnsSuccess()
        {
            var userId = Guid.NewGuid();
            var accessToken = "test-access-token";
            var refreshToken = "test-refresh-token";
            var expiresAt = DateTimeOffset.UtcNow.AddMinutes(15);

            var code = _sut.GenerateAuthCode(userId, accessToken, refreshToken, expiresAt);
            var result = _sut.ExchangeAuthCode(code);

            Assert.True(result.IsSuccess);
            Assert.Equal(userId, result.Value.UserId);
            Assert.Equal(accessToken, result.Value.AccessToken);
            Assert.Equal(refreshToken, result.Value.RefreshToken);
        }

        [Fact]
        public void ExchangeAuthCode_IsOneTimeUse()
        {
            var code = _sut.GenerateAuthCode(Guid.NewGuid(), "access-token", "refresh-token", DateTimeOffset.UtcNow.AddMinutes(15));

            var first = _sut.ExchangeAuthCode(code);
            var second = _sut.ExchangeAuthCode(code);

            Assert.True(first.IsSuccess);
            Assert.False(second.IsSuccess);
            Assert.Equal(AuthError.InvalidAuthCode, second.Error);
        }

        [Fact]
        public void ExchangeAuthCode_WithInvalidCode_ReturnsFailure()
        {
            var result = _sut.ExchangeAuthCode("non-existent-code");

            Assert.False(result.IsSuccess);
            Assert.Equal(AuthError.InvalidAuthCode, result.Error);
        }

        [Fact]
        public void ExchangeAuthCode_WithExpiredCode_ReturnsFailure()
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
            var result = shortLivedService.ExchangeAuthCode(code);

            Assert.False(result.IsSuccess);
            Assert.Equal(AuthError.InvalidAuthCode, result.Error);
            shortCache.Dispose();
        }
    }
}
