using AuthService.DTOs.Auth;
using AuthService.Extensions;
using AuthService.Services;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.Controllers
{
    [ApiController]
    [Route("api/v1/auth")]
    [Produces("application/json")]
    public class ExchangeController(IOAuthSecurityService oauthSecurity) : ControllerBase
    {
        /// <summary>
        /// Exchange a one-time authorization code for tokens (POST, no tokens in URL).
        /// </summary>
        [HttpPost("exchange")]
        [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult ExchangeCode([FromBody] ExchangeCodeRequest request)
        {
            var result = oauthSecurity.ExchangeAuthCode(request.Code);
            if (!result.IsSuccess)
                return this.ToErrorResponse(result.Error);

            var payload = result.Value;
            return Ok(new AuthResponse
            {
                UserId = payload.UserId,
                AccessToken = payload.AccessToken,
                RefreshToken = payload.RefreshToken,
                ExpiresAt = payload.ExpiresAt
            });
        }
    }
}
