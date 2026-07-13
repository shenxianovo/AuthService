using System.Security.Claims;
using AuthService.Common;
using AuthService.Data;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace AuthService.Controllers
{
    /// <summary>
    /// OIDC authorization endpoint (OpenIddict passthrough). Authenticates the
    /// browser via the interactive cookie; the protocol work (client/redirect_uri
    /// validation, code issuance) is done by OpenIddict before/after this action.
    /// </summary>
    public class AuthorizationController(AppDbContext db) : ControllerBase
    {
        [HttpGet("~/connect/authorize")]
        [HttpPost("~/connect/authorize")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Authorize()
        {
            var request = HttpContext.GetOpenIddictServerRequest()
                ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

            var auth = await HttpContext.AuthenticateAsync(AuthConstants.InteractiveScheme);
            if (!auth.Succeeded
                || !Guid.TryParse(auth.Principal.FindFirstValue("sub"), out var userId)
                || !Guid.TryParse(auth.Principal.FindFirstValue("sid"), out var sessionId))
            {
                return ChallengeInteractive();
            }

            // The cookie can outlive the session (logout elsewhere, admin revocation):
            // the database, not the cookie, is the authority on whether SSO is allowed.
            var sessionAlive = await db.Sessions.AnyAsync(s =>
                s.Id == sessionId && !s.Revoked && s.ExpiresAt > DateTimeOffset.UtcNow);
            var user = sessionAlive
                ? await db.Users.Include(u => u.Emails).FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted)
                : null;
            if (user is null)
            {
                await HttpContext.SignOutAsync(AuthConstants.InteractiveScheme);
                return ChallengeInteractive();
            }

            var identity = new ClaimsIdentity(
                authenticationType: TokenValidationParameters.DefaultAuthenticationType,
                nameType: Claims.Name,
                roleType: Claims.Role);

            // Emit the username as both `name` and `preferred_username` so clients
            // (e.g. OpenList's "OIDC username key") work with either setting.
            identity.SetClaim(Claims.Subject, user.Id.ToString())
                    .SetClaim(Claims.Name, user.Username)
                    .SetClaim(Claims.PreferredUsername, user.Username);

            // Only a verified primary email is asserted to downstream clients —
            // registration doesn't require verification, and off-the-shelf RPs
            // can't be trusted to check email_verified (mirror of ADR-012).
            var verifiedEmail = user.Emails.FirstOrDefault(e => e.IsPrimary && e.VerifiedAt != null);
            if (verifiedEmail is not null)
            {
                identity.SetClaim(Claims.Email, verifiedEmail.Email);
                identity.SetClaim(Claims.EmailVerified, true);
            }

            // Seeded clients use implicit consent, so no consent screen is shown.
            identity.SetScopes(request.GetScopes());
            identity.SetDestinations(GetDestinations);

            return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        /// <summary>
        /// OIDC token endpoint (passthrough). OpenIddict has already validated the
        /// client and the code/refresh token; this action only enforces account
        /// liveness before re-issuing. Grants deliberately outlive sessions —
        /// logout must not end downstream logins (ADR-020 mental model) — but not
        /// the account: a soft-deleted/merged user must not refresh.
        /// </summary>
        [HttpPost("~/connect/token")]
        [IgnoreAntiforgeryToken]
        [Produces("application/json")]
        public async Task<IActionResult> Exchange()
        {
            var request = HttpContext.GetOpenIddictServerRequest()
                ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

            if (!request.IsAuthorizationCodeGrantType() && !request.IsRefreshTokenGrantType())
                throw new InvalidOperationException("The specified grant type is not supported.");

            // The principal stored inside the authorization code / refresh token.
            var result = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            if (!result.Succeeded
                || !Guid.TryParse(result.Principal?.GetClaim(Claims.Subject), out var userId))
            {
                return ForbidInvalidGrant("The token is no longer valid.");
            }

            // The soft-delete query filter hides dead users, so existence == liveness.
            if (!await db.Users.AnyAsync(u => u.Id == userId))
                return ForbidInvalidGrant("The user account is no longer available.");

            // Claims, scopes and destinations round-trip inside the code/refresh
            // token; re-signing in the stored principal re-issues them unchanged.
            return SignIn(result.Principal!, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        private IActionResult ForbidInvalidGrant(string description)
            => Forbid(
                new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description,
                }),
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        /// <summary>
        /// 302 to the SPA login page with returnUrl pointing back at this authorize
        /// request, so the flow resumes after the user signs in.
        /// </summary>
        private IActionResult ChallengeInteractive()
            => Challenge(
                new AuthenticationProperties
                {
                    RedirectUri = Request.PathBase + Request.Path + Request.QueryString
                },
                AuthConstants.InteractiveScheme);

        private static IEnumerable<string> GetDestinations(Claim claim)
        {
            switch (claim.Type)
            {
                case Claims.Name or Claims.PreferredUsername:
                    yield return Destinations.AccessToken;
                    if (claim.Subject!.HasScope(Scopes.Profile))
                        yield return Destinations.IdentityToken;
                    yield break;

                case Claims.Email or Claims.EmailVerified:
                    // Keep the access token lean: email travels via id_token/userinfo only.
                    if (claim.Subject!.HasScope(Scopes.Email))
                        yield return Destinations.IdentityToken;
                    yield break;

                default:
                    yield return Destinations.AccessToken;
                    yield break;
            }
        }
    }
}
