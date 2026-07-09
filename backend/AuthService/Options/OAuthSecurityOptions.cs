namespace AuthService.Configuration
{
    public class OAuthSecurityOptions
    {
        public const string Section = "OAuthSecurity";

        /// <summary>
        /// Allowed redirect URL origins. Supports wildcard subdomains with "*." prefix.
        /// e.g. "https://*.shenxianovo.com" allows all subdomains of shenxianovo.com.
        /// e.g. "https://example.com" allows exact match only.
        /// </summary>
        public List<string> AllowedRedirectOrigins { get; set; } = [];

        /// <summary>
        /// How long the one-time authorization code is valid (seconds). Default: 60.
        /// </summary>
        public int AuthCodeExpirationSeconds { get; set; } = 60;
    }

    public class AuthCodePayload
    {
        public Guid UserId { get; set; }
        public string AccessToken { get; set; } = null!;
        public string RefreshToken { get; set; } = null!;
        public DateTimeOffset ExpiresAt { get; set; }
    }
}
