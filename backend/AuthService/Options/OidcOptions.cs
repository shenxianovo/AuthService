namespace AuthService.Configuration
{
    public class OidcOptions
    {
        public const string Section = "Oidc";

        /// <summary>
        /// Base64-encoded 32-byte symmetric key encrypting OpenIddict authorization
        /// codes and refresh tokens. Must stay stable across restarts.
        /// Generate with: openssl rand -base64 32
        /// </summary>
        public string EncryptionKey { get; set; } = string.Empty;

        /// <summary>
        /// OIDC clients seeded at startup (e.g. OpenList). Seeding is idempotent:
        /// existing clients are updated in place, removed entries are left alone.
        /// </summary>
        public List<OidcClientOptions> Clients { get; set; } = [];
    }

    public class OidcClientOptions
    {
        public string ClientId { get; set; } = null!;

        /// <summary>Required for confidential clients; must be empty for public ones.</summary>
        public string ClientSecret { get; set; } = string.Empty;

        public string DisplayName { get; set; } = null!;

        /// <summary>
        /// "confidential" (default; server-side client with a secret) or "public"
        /// (browser/native client, no secret, PKCE enforced per client).
        /// </summary>
        public string Type { get; set; } = "confidential";

        /// <summary>
        /// Exact-match redirect URIs, query string included. OpenList calls back on
        /// /api/auth/sso_callback with ?method=... variants — list every variant.
        /// </summary>
        public List<string> RedirectUris { get; set; } = [];

        /// <summary>Scopes the client may request besides openid (e.g. profile, email).</summary>
        public List<string> Scopes { get; set; } = [];
    }
}
