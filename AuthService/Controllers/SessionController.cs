using AuthService.DTOs.Auth;
using AuthService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.Controllers
{
    [ApiController]
    [Route("api/v1/auth")]
    public class SessionController(
        ISessionService sessionService,
        IJwtService jwtService) : ControllerBase
    {
        /// <summary>
        /// Exchange a valid refresh token for a new access token + rotated refresh token.
        /// The old refresh token is immediately revoked after use.
        /// </summary>
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
        {
            var response = await sessionService.RefreshSessionAsync(request.RefreshToken);
            return Ok(response);
        }

        /// <summary>
        /// Revoke the current session identified by the Bearer token's `sid` claim.
        /// All refresh tokens belonging to that session are also revoked.
        /// </summary>
        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            var authHeader = Request.Headers.Authorization.ToString();
            var token = authHeader.StartsWith("Bearer ") ? authHeader["Bearer ".Length..] : null;
            if (token == null) return Unauthorized();

            var sessionId = jwtService.GetSessionIdFromToken(token);
            if (sessionId == null) return Unauthorized();

            await sessionService.RevokeSessionAsync(sessionId.Value);
            return NoContent();
        }
    }
}