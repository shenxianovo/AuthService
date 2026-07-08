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

            var primaryEmail = user.Emails.FirstOrDefault(e => e.IsPrimary) ?? user.Emails.FirstOrDefault();
            if (primaryEmail is not null)
            {
                identity.SetClaim(Claims.Email, primaryEmail.Email);
                identity.SetClaim(Claims.EmailVerified, primaryEmail.VerifiedAt != null);
            }

            // Seeded clients use implicit consent, so no consent screen is shown.
            identity.SetScopes(request.GetScopes());
            identity.SetDestinations(GetDestinations);

            return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

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
