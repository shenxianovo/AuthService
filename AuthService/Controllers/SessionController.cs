using AuthService.DTOs.Auth;
using AuthService.Extensions;
using AuthService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.Controllers
{
    [ApiController]
    [Route("api/v1/auth")]
    [Produces("application/json")]
    public class SessionController(
        ISessionService sessionService,
        IJwtService jwtService) : ControllerBase
    {
        /// <summary>
        /// Exchange a valid refresh token for a new access token + rotated refresh token.
        /// The old refresh token is immediately revoked after use.
        /// </summary>
        [HttpPost("refresh")]
        [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
        {
            var result = await sessionService.RefreshSessionAsync(request.RefreshToken);
            return result.IsSuccess ? Ok(result.Value) : this.ToErrorResponse(result.Error);
        }

        /// <summary>
        /// Revoke the current session identified by the Bearer token's `sid` claim.
        /// All refresh tokens belonging to that session are also revoked.
        /// </summary>
        [Authorize]
        [HttpPost("logout")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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