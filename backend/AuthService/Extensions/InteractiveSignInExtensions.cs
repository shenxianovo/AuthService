using System.Security.Claims;
using AuthService.Common;
using AuthService.DTOs.Auth;
using AuthService.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.Extensions
{
    /// <summary>
    /// Establishes the interactive cookie session consumed by the OIDC authorize
    /// endpoint. The cookie carries only sub + sid; the authorize endpoint reloads
    /// user data and re-validates the session against the database on every request,
    /// so a stale cookie after logout is harmless.
    /// </summary>
    public static class InteractiveSignInExtensions
    {
        public static async Task SignInInteractiveAsync(
            this ControllerBase controller, AuthResponse auth, IJwtService jwtService)
        {
            // API-key style tokens carry no session — no interactive cookie for them.
            var sessionId = jwtService.GetSessionIdFromToken(auth.AccessToken);
            if (sessionId is null)
                return;

            var identity = new ClaimsIdentity(AuthConstants.InteractiveScheme);
            identity.AddClaim(new Claim("sub", auth.UserId.ToString()));
            identity.AddClaim(new Claim("sid", sessionId.Value.ToString()));

            await controller.HttpContext.SignInAsync(
                AuthConstants.InteractiveScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties { IsPersistent = true });
        }
    }
}
