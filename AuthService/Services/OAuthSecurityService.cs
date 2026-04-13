using AuthService.Configuration;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace AuthService.Services
{
    public interface IOAuthSecurityService
    {
        string GenerateState(string? redirectUrl, Guid? userId);
        OAuthStatePayload? ValidateState(string protectedState);
        void ValidateRedirectUrl(string? redirectUrl);
        string GenerateAuthCode(Guid userId, string accessToken, string refreshToken, DateTimeOffset expiresAt);
        AuthCodePayload? ExchangeAuthCode(string code);
    }

    public class OAuthSecurityService(
        IDataProtectionProvider dataProtection,
        IMemoryCache cache,
        IOptions<OAuthSecurityOptions> options) : IOAuthSecurityService
    {
        private readonly IDataProtector _protector = dataProtection.CreateProtector("OAuthState");
        private readonly OAuthSecurityOptions _options = options.Value;

        /// <summary>
        /// Generate a signed, tamper-proof state parameter for OAuth flows.
        /// Includes a CSRF nonce and optional redirect URL / user ID.
        /// </summary>
        public string GenerateState(string? redirectUrl, Guid? userId)
        {
            var payload = new OAuthStatePayload
            {
                RedirectUrl = redirectUrl,
                UserId = userId,
                Nonce = Guid.NewGuid().ToString("N"),
                ExpiresAtUnix = DateTimeOffset.UtcNow.AddSeconds(_options.StateExpirationSeconds).ToUnixTimeSeconds()
            };

            var json = JsonSerializer.Serialize(payload);
            return _protector.Protect(json);
        }

        /// <summary>
        /// Validate and deserialize the OAuth state. Returns null if invalid, expired, or tampered.
        /// </summary>
        public OAuthStatePayload? ValidateState(string protectedState)
        {
            try
            {
                var json = _protector.Unprotect(protectedState);
                var payload = JsonSerializer.Deserialize<OAuthStatePayload>(json);

                if (payload == null)
                    return null;

                // Check expiration
                if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > payload.ExpiresAtUnix)
                    return null;

                return payload;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Validate that the redirect URL is in the allowed list.
        /// Throws InvalidOperationException if not allowed.
        /// </summary>
        public void ValidateRedirectUrl(string? redirectUrl)
        {
            if (string.IsNullOrEmpty(redirectUrl))
                return;

            if (!Uri.TryCreate(redirectUrl, UriKind.Absolute, out var uri))
                throw new InvalidOperationException("Invalid redirect URL.");

            if (uri.Scheme != "https" && uri.Scheme != "http")
                throw new InvalidOperationException("Invalid redirect URL scheme.");

            var host = uri.Host;
            var origin = $"{uri.Scheme}://{uri.Authority}";

            foreach (var allowed in _options.AllowedRedirectOrigins)
            {
                var trimmed = allowed.TrimEnd('/');

                // Wildcard subdomain matching: "https://*.example.com"
                if (trimmed.Contains("*.", StringComparison.Ordinal))
                {
                    // Parse the pattern to extract scheme and wildcard domain
                    if (Uri.TryCreate(trimmed.Replace("*.", "placeholder."), UriKind.Absolute, out var patternUri))
                    {
                        if (!string.Equals(uri.Scheme, patternUri.Scheme, StringComparison.OrdinalIgnoreCase))
                            continue;

                        var baseDomain = patternUri.Host.Replace("placeholder.", "", StringComparison.Ordinal);

                        // Match the base domain itself or any subdomain
                        if (string.Equals(host, baseDomain, StringComparison.OrdinalIgnoreCase) ||
                            host.EndsWith($".{baseDomain}", StringComparison.OrdinalIgnoreCase))
                            return;
                    }
                    continue;
                }

                // Exact origin match
                if (string.Equals(origin, trimmed, StringComparison.OrdinalIgnoreCase))
                    return;
            }

            throw new InvalidOperationException("Redirect URL is not allowed.");
        }

        /// <summary>
        /// Generate a one-time short-lived authorization code that can be exchanged for tokens.
        /// The actual tokens are stored server-side in memory cache.
        /// </summary>
        public string GenerateAuthCode(Guid userId, string accessToken, string refreshToken, DateTimeOffset expiresAt)
        {
            var code = Guid.NewGuid().ToString("N");
            var payload = new AuthCodePayload
            {
                UserId = userId,
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = expiresAt
            };

            cache.Set($"authcode:{code}", payload, TimeSpan.FromSeconds(_options.AuthCodeExpirationSeconds));
            return code;
        }

        /// <summary>
        /// Exchange a one-time authorization code for tokens. Returns null if invalid/expired.
        /// The code is consumed and cannot be reused.
        /// </summary>
        public AuthCodePayload? ExchangeAuthCode(string code)
        {
            var key = $"authcode:{code}";
            if (cache.TryGetValue(key, out AuthCodePayload? payload))
            {
                cache.Remove(key); // one-time use
                return payload;
            }
            return null;
        }
    }
}