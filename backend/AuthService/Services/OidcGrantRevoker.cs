using OpenIddict.Abstractions;

namespace AuthService.Services
{
    /// <summary>
    /// Revokes a user's OIDC grants — the OpenIddict authorizations and tokens
    /// keyed on their <c>sub</c>. Grants deliberately outlive sessions (logout
    /// must not end downstream logins, ADR-020 mental model), so this is only
    /// called when the account itself dies: merge / soft-delete.
    /// </summary>
    public interface IOidcGrantRevoker
    {
        Task RevokeAllForUserAsync(Guid userId);
    }

    public class OidcGrantRevoker(
        IOpenIddictAuthorizationManager authorizations,
        IOpenIddictTokenManager tokens) : IOidcGrantRevoker
    {
        public async Task RevokeAllForUserAsync(Guid userId)
        {
            var subject = userId.ToString();

            await foreach (var authorization in authorizations.FindBySubjectAsync(subject))
                await authorizations.TryRevokeAsync(authorization);

            await foreach (var token in tokens.FindBySubjectAsync(subject))
                await tokens.TryRevokeAsync(token);
        }
    }
}
