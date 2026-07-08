using System.Security.Cryptography;
using AuthService.Common;
using AuthService.DTOs.Admin;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace AuthService.Services
{
    public interface IOidcClientAdminService
    {
        Task<List<OidcClientSummary>> ListAsync(CancellationToken ct = default);
        Task<Result<CreateOidcClientResponse>> CreateAsync(CreateOidcClientRequest request, CancellationToken ct = default);
        Task<Result<OidcClientSummary>> UpdateAsync(string clientId, UpdateOidcClientRequest request, CancellationToken ct = default);
        Task<Result<RegenerateSecretResponse>> RegenerateSecretAsync(string clientId, CancellationToken ct = default);
        Task<Result> DeleteAsync(string clientId, CancellationToken ct = default);
    }

    /// <summary>
    /// Admin CRUD over the OpenIddict application store. Secrets follow the API
    /// Key UX: generated server-side, returned exactly once, hashed at rest by
    /// OpenIddict. The store is the source of truth for clients (ADR-017).
    /// </summary>
    public class OidcClientAdminService(IOpenIddictApplicationManager manager) : IOidcClientAdminService
    {
        public async Task<List<OidcClientSummary>> ListAsync(CancellationToken ct = default)
        {
            var clients = new List<OidcClientSummary>();
            await foreach (var application in manager.ListAsync(cancellationToken: ct))
                clients.Add(await ToSummaryAsync(application, ct));
            return clients;
        }

        public async Task<Result<CreateOidcClientResponse>> CreateAsync(CreateOidcClientRequest request, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(request.ClientId) || string.IsNullOrWhiteSpace(request.DisplayName))
                return Result<CreateOidcClientResponse>.Fail(AuthError.InvalidOidcClient, "ClientId and DisplayName are required.");

            var isPublic = OidcClientDescriptors.IsPublicType(request.Type);
            if (!isPublic && !string.Equals(request.Type, "confidential", StringComparison.OrdinalIgnoreCase))
                return Result<CreateOidcClientResponse>.Fail(AuthError.InvalidOidcClient, "Type must be 'confidential' or 'public'.");

            var uriError = ValidateRedirectUris(request.RedirectUris);
            if (uriError is not null)
                return Result<CreateOidcClientResponse>.Fail(AuthError.InvalidOidcClient, uriError);

            if (await manager.FindByClientIdAsync(request.ClientId, ct) is not null)
                return Result<CreateOidcClientResponse>.Fail(AuthError.OidcClientAlreadyExists);

            var secret = isPublic ? null : GenerateSecret();
            var descriptor = OidcClientDescriptors.Build(
                request.ClientId, secret, request.DisplayName, isPublic,
                request.RedirectUris, request.Scopes);

            var application = await manager.CreateAsync(descriptor, ct);

            return Result<CreateOidcClientResponse>.Ok(new CreateOidcClientResponse
            {
                Client = await ToSummaryAsync(application, ct),
                ClientSecret = secret,
            });
        }

        public async Task<Result<OidcClientSummary>> UpdateAsync(string clientId, UpdateOidcClientRequest request, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(request.DisplayName))
                return Result<OidcClientSummary>.Fail(AuthError.InvalidOidcClient, "DisplayName is required.");

            var uriError = ValidateRedirectUris(request.RedirectUris);
            if (uriError is not null)
                return Result<OidcClientSummary>.Fail(AuthError.InvalidOidcClient, uriError);

            var application = await manager.FindByClientIdAsync(clientId, ct);
            if (application is null)
                return Result<OidcClientSummary>.Fail(AuthError.OidcClientNotFound);

            // Round-trip the descriptor so type, consent and the stored (hashed)
            // secret are preserved — only the fields below change.
            var descriptor = new OpenIddictApplicationDescriptor();
            await manager.PopulateAsync(descriptor, application, ct);

            descriptor.DisplayName = request.DisplayName;
            descriptor.RedirectUris.Clear();
            foreach (var uri in request.RedirectUris)
                descriptor.RedirectUris.Add(new Uri(uri));

            var scopePermissions = descriptor.Permissions
                .Where(p => p.StartsWith(Permissions.Prefixes.Scope, StringComparison.Ordinal))
                .ToList();
            foreach (var permission in scopePermissions)
                descriptor.Permissions.Remove(permission);
            foreach (var scope in request.Scopes)
                descriptor.Permissions.Add(Permissions.Prefixes.Scope + scope);

            await manager.UpdateAsync(application, descriptor, ct);

            return Result<OidcClientSummary>.Ok(await ToSummaryAsync(application, ct));
        }

        public async Task<Result<RegenerateSecretResponse>> RegenerateSecretAsync(string clientId, CancellationToken ct = default)
        {
            var application = await manager.FindByClientIdAsync(clientId, ct);
            if (application is null)
                return Result<RegenerateSecretResponse>.Fail(AuthError.OidcClientNotFound);

            if (OidcClientDescriptors.IsPublicType(await manager.GetClientTypeAsync(application, ct)))
                return Result<RegenerateSecretResponse>.Fail(AuthError.InvalidOidcClient, "Public clients have no secret.");

            var secret = GenerateSecret();
            var descriptor = new OpenIddictApplicationDescriptor();
            await manager.PopulateAsync(descriptor, application, ct);
            descriptor.ClientSecret = secret;
            await manager.UpdateAsync(application, descriptor, ct);

            return Result<RegenerateSecretResponse>.Ok(new RegenerateSecretResponse { ClientSecret = secret });
        }

        public async Task<Result> DeleteAsync(string clientId, CancellationToken ct = default)
        {
            var application = await manager.FindByClientIdAsync(clientId, ct);
            if (application is null)
                return Result.Fail(AuthError.OidcClientNotFound);

            await manager.DeleteAsync(application, ct);
            return Result.Ok();
        }

        private async Task<OidcClientSummary> ToSummaryAsync(object application, CancellationToken ct)
        {
            var permissions = await manager.GetPermissionsAsync(application, ct);
            return new OidcClientSummary
            {
                ClientId = (await manager.GetClientIdAsync(application, ct))!,
                DisplayName = await manager.GetDisplayNameAsync(application, ct) ?? "",
                Type = await manager.GetClientTypeAsync(application, ct) ?? ClientTypes.Confidential,
                RedirectUris = [.. (await manager.GetRedirectUrisAsync(application, ct)).Select(u => u.ToString())],
                Scopes = [.. permissions
                    .Where(p => p.StartsWith(Permissions.Prefixes.Scope, StringComparison.Ordinal))
                    .Select(p => p[Permissions.Prefixes.Scope.Length..])],
            };
        }

        private static string? ValidateRedirectUris(List<string> uris)
        {
            if (uris.Count == 0)
                return "At least one redirect URI is required.";

            foreach (var uri in uris)
            {
                if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed)
                    || (parsed.Scheme != Uri.UriSchemeHttps && parsed.Scheme != Uri.UriSchemeHttp))
                    return $"Invalid redirect URI: {uri}";
            }
            return null;
        }

        private static string GenerateSecret() =>
            Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
    }
}
