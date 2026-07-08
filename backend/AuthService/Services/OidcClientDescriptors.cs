using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace AuthService.Services
{
    /// <summary>
    /// Single place that turns client parameters into an OpenIddict application
    /// descriptor — shared by the startup seeder and the admin management API so
    /// both register clients with identical permissions and PKCE rules.
    /// </summary>
    public static class OidcClientDescriptors
    {
        public static bool IsPublicType(string? type) =>
            string.Equals(type, "public", StringComparison.OrdinalIgnoreCase);

        public static OpenIddictApplicationDescriptor Build(
            string clientId,
            string? clientSecret,
            string displayName,
            bool isPublic,
            IEnumerable<string> redirectUris,
            IEnumerable<string> scopes)
        {
            var descriptor = new OpenIddictApplicationDescriptor
            {
                ClientId = clientId,
                ClientSecret = isPublic ? null : clientSecret,
                DisplayName = displayName,
                ClientType = isPublic ? ClientTypes.Public : ClientTypes.Confidential,
                // First-party, self-hosted clients: skip the consent screen.
                ConsentType = ConsentTypes.Implicit,
                Permissions =
                {
                    Permissions.Endpoints.Authorization,
                    Permissions.Endpoints.Token,
                    Permissions.GrantTypes.AuthorizationCode,
                    Permissions.GrantTypes.RefreshToken,
                    Permissions.ResponseTypes.Code,
                },
            };

            // A public client's code flow is only safe with PKCE; requiring it
            // per client prevents downgrade to a bare code exchange.
            if (isPublic)
                descriptor.Requirements.Add(Requirements.Features.ProofKeyForCodeExchange);

            foreach (var scope in scopes)
                descriptor.Permissions.Add(Permissions.Prefixes.Scope + scope);

            foreach (var uri in redirectUris)
                descriptor.RedirectUris.Add(new Uri(uri));

            return descriptor;
        }
    }
}
