using AuthService.DTOs.Auth;
using AuthService.Extensions;
using AuthService.Services;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.Controllers
{
    [ApiController]
    [Route("api/v1/auth")]
    [Produces("application/json")]
    public class ExchangeController(
        IOAuthSecurityService oauthSecurity,
        IJwtService jwtService) : ControllerBase
    {
        /// <summary>
        /// Exchange a one-time authorization code for tokens (POST, no tokens in URL).
        /// </summary>
        [HttpPost("exchange")]
        [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ExchangeCode([FromBody] ExchangeCodeRequest request)
        {
            var result = oauthSecurity.ExchangeAuthCode(request.Code);
            if (!result.IsSuccess)
                return this.ToErrorResponse(result.Error);

            var payload = result.Value;
            var auth = new AuthResponse
            {
                UserId = payload.UserId,
                AccessToken = payload.AccessToken,
                RefreshToken = payload.RefreshToken,
                ExpiresAt = payload.ExpiresAt
            };

            // OAuth logins (GitHub/Google) land their tokens here, so this is the
            // single place the interactive cookie gets issued for those flows.
            await this.SignInInteractiveAsync(auth, jwtService);
            return Ok(auth);
        }
    }
}
