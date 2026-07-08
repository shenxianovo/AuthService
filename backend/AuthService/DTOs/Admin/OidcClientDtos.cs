namespace AuthService.DTOs.Admin
{
    /// <summary>An OIDC client as shown in the admin UI. Never carries a secret.</summary>
    public class OidcClientSummary
    {
        public string ClientId { get; set; } = null!;
        public string DisplayName { get; set; } = null!;

        /// <summary>"confidential" or "public".</summary>
        public string Type { get; set; } = null!;

        public List<string> RedirectUris { get; set; } = [];
        public List<string> Scopes { get; set; } = [];
    }

    public class CreateOidcClientRequest
    {
        public string ClientId { get; set; } = null!;
        public string DisplayName { get; set; } = null!;

        /// <summary>"confidential" (default) or "public". Immutable after creation.</summary>
        public string Type { get; set; } = "confidential";

        public List<string> RedirectUris { get; set; } = [];
        public List<string> Scopes { get; set; } = [];
    }

    /// <summary>
    /// The secret is generated server-side and returned exactly once — it is
    /// hashed at rest and can never be retrieved again (null for public clients).
    /// </summary>
    public class CreateOidcClientResponse
    {
        public OidcClientSummary Client { get; set; } = null!;
        public string? ClientSecret { get; set; }
    }

    public class UpdateOidcClientRequest
    {
        public string DisplayName { get; set; } = null!;
        public List<string> RedirectUris { get; set; } = [];
        public List<string> Scopes { get; set; } = [];
    }

    public class RegenerateSecretResponse
    {
        public string ClientSecret { get; set; } = null!;
    }
}
