using AuthService.Common;
using AuthService.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace AuthService.Services
{
    public interface IOAuthSecurityService
    {
        Result ValidateRedirectUrl(string? redirectUrl);
        string GenerateAuthCode(Guid userId, string accessToken, string refreshToken, DateTimeOffset expiresAt);
        Result<AuthCodePayload> ExchangeAuthCode(string code);
    }

    public class OAuthSecurityService(
        IMemoryCache cache,
        IOptions<OAuthSecurityOptions> options) : IOAuthSecurityService
    {
        private readonly OAuthSecurityOptions _options = options.Value;

        /// <summary>
        /// Validate that the redirect URL is in the allowed list.
        /// Returns Result.Fail(InvalidRedirectUrl) if not allowed; Result.Ok() otherwise.
        /// </summary>
        public Result ValidateRedirectUrl(string? redirectUrl)
        {
            if (string.IsNullOrEmpty(redirectUrl))
                return Result.Ok();

            if (!Uri.TryCreate(redirectUrl, UriKind.Absolute, out var uri))
                return Result.Fail(AuthError.InvalidRedirectUrl, "Invalid redirect URL.");

            if (uri.Scheme != "https" && uri.Scheme != "http")
                return Result.Fail(AuthError.InvalidRedirectUrl, "Invalid redirect URL scheme.");

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
                            return Result.Ok();
                    }
                    continue;
                }

                // Exact origin match
                if (string.Equals(origin, trimmed, StringComparison.OrdinalIgnoreCase))
                    return Result.Ok();
            }

            return Result.Fail(AuthError.InvalidRedirectUrl, "Redirect URL is not allowed.");
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
        /// Exchange a one-time authorization code for tokens.
        /// Returns Result.Fail(InvalidAuthCode) if the code is invalid or expired.
        /// The code is consumed and cannot be reused.
        /// </summary>
        public Result<AuthCodePayload> ExchangeAuthCode(string code)
        {
            var key = $"authcode:{code}";
            if (cache.TryGetValue(key, out AuthCodePayload? payload))
            {
                cache.Remove(key); // one-time use
                return Result<AuthCodePayload>.Ok(payload!);
            }
            return Result<AuthCodePayload>.Fail(AuthError.InvalidAuthCode);
        }
    }
}