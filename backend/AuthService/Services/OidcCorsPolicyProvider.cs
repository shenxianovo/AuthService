using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using OpenIddict.Abstractions;

namespace AuthService.Services
{
    /// <summary>
    /// CORS policy for the OIDC endpoints only (/connect/*, /.well-known/*).
    /// A browser-based (public) client fetches discovery, JWKS, the token
    /// endpoint and userinfo cross-origin, so those endpoints must allow the
    /// client's origin. Allowed origins are derived from the redirect URIs of
    /// the registered OpenIddict applications (cached briefly), so client
    /// registration is the single source of truth — no separate origin list
    /// to keep in sync. All other endpoints get no CORS headers at all.
    /// </summary>
    public sealed class OidcCorsPolicyProvider(IMemoryCache cache) : ICorsPolicyProvider
    {
        private const string CacheKey = "oidc:cors-origins";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

        public async Task<CorsPolicy?> GetPolicyAsync(HttpContext context, string? policyName)
        {
            var path = context.Request.Path;
            if (!path.StartsWithSegments("/connect") && !path.StartsWithSegments("/.well-known"))
                return null;

            var origins = await cache.GetOrCreateAsync(CacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheTtl;
                return await LoadClientOriginsAsync(context.RequestServices, context.RequestAborted);
            });

            if (origins is not { Length: > 0 })
                return null;

            return new CorsPolicyBuilder()
                .WithOrigins(origins)
                .WithMethods("GET", "POST")
                .WithHeaders("Authorization", "Content-Type")
                .Build();
        }

        private static async Task<string[]> LoadClientOriginsAsync(
            IServiceProvider requestServices, CancellationToken cancellationToken)
        {
            var manager = requestServices.GetRequiredService<IOpenIddictApplicationManager>();

            var origins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await foreach (var application in manager.ListAsync(cancellationToken: cancellationToken))
            {
                foreach (var redirectUri in await manager.GetRedirectUrisAsync(application, cancellationToken))
                {
                    if (Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri))
                        origins.Add(uri.GetLeftPart(UriPartial.Authority));
                }
            }

            return [.. origins];
        }
    }
}
