namespace AuthService.Configuration
{
    public class OAuthSecurityOptions
    {
        public const string Section = "OAuthSecurity";

        /// <summary>
        /// Allowed redirect URL origins, exact match only (e.g. "https://example.com").
        /// The only legitimate consumer is the SPA's own OAuth round-trip (ADR-018);
        /// wildcards were retired with the ?redirect= era and are rejected at startup.
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
