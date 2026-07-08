using AuthService.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace AuthService.Controllers
{
    /// <summary>
    /// OIDC userinfo endpoint. Accepts only OpenIddict-issued access tokens
    /// (validation scheme) — session JWTs from JwtService are rejected.
    /// </summary>
    public class UserinfoController(AppDbContext db) : ControllerBase
    {
        [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
        [HttpGet("~/connect/userinfo")]
        [HttpPost("~/connect/userinfo")]
        [Produces("application/json")]
        public async Task<IActionResult> Userinfo()
        {
            Guid.TryParse(User.GetClaim(Claims.Subject), out var userId);
            var user = await db.Users.Include(u => u.Emails)
                .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);
            if (user is null)
            {
                return Challenge(
                    new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictValidationAspNetCoreConstants.Properties.Error] = Errors.InvalidToken,
                        [OpenIddictValidationAspNetCoreConstants.Properties.ErrorDescription] =
                            "The access token is no longer associated with an existing user."
                    }),
                    OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
            }

            var claims = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                [Claims.Subject] = user.Id.ToString(),
            };

            if (User.HasScope(Scopes.Profile))
            {
                claims[Claims.Name] = user.Username;
                claims[Claims.PreferredUsername] = user.Username;
            }

            if (User.HasScope(Scopes.Email))
            {
                // Only a verified primary email is asserted (see AuthorizationController).
                var verifiedEmail = user.Emails.FirstOrDefault(e => e.IsPrimary && e.VerifiedAt != null);
                if (verifiedEmail is not null)
                {
                    claims[Claims.Email] = verifiedEmail.Email;
                    claims[Claims.EmailVerified] = true;
                }
            }

            return Ok(claims);
        }
    }
}
