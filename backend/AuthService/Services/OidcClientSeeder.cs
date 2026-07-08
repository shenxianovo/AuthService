using AuthService.Configuration;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace AuthService.Services
{
    /// <summary>
    /// Registers the OIDC clients declared under Oidc:Clients at startup.
    /// Idempotent: existing clients are updated in place from configuration
    /// (config is the source of truth); entries removed from config are left alone.
    /// Runs after the inline migration block in Program.cs, so the OpenIddict
    /// tables always exist by the time this executes.
    /// </summary>
    public sealed class OidcClientSeeder(
        IServiceProvider serviceProvider,
        IOptions<OidcOptions> options,
        ILogger<OidcClientSeeder> logger) : IHostedService
    {
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var clients = options.Value.Clients;
            if (clients.Count == 0)
                return;

            await using var scope = serviceProvider.CreateAsyncScope();
            var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

            foreach (var client in clients)
            {
                // appsettings.json ships the client skeleton with an empty secret;
                // the real secret lives in user-secrets/env. Until it's provided the
                // client simply isn't registered (confidential clients need a secret).
                if (string.IsNullOrEmpty(client.ClientId) || string.IsNullOrEmpty(client.ClientSecret))
                {
                    logger.LogWarning(
                        "Skipping OIDC client '{ClientId}': ClientId/ClientSecret not configured.",
                        client.ClientId);
                    continue;
                }
                var descriptor = new OpenIddictApplicationDescriptor
                {
                    ClientId = client.ClientId,
                    ClientSecret = client.ClientSecret,
                    DisplayName = client.DisplayName,
                    ClientType = ClientTypes.Confidential,
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

                foreach (var scopeName in client.Scopes)
                    descriptor.Permissions.Add(Permissions.Prefixes.Scope + scopeName);

                foreach (var uri in client.RedirectUris)
                    descriptor.RedirectUris.Add(new Uri(uri));

                var existing = await manager.FindByClientIdAsync(client.ClientId, cancellationToken);
                if (existing is null)
                    await manager.CreateAsync(descriptor, cancellationToken);
                else
                    await manager.UpdateAsync(existing, descriptor, cancellationToken);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
