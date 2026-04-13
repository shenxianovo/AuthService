using AuthService.DTOs.Auth;
using AuthService.Extensions;
using AuthService.Services;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.Controllers
{
    [ApiController]
    [Route("api/v1/auth")]
    public class ExchangeController(IOAuthSecurityService oauthSecurity) : ControllerBase
    {
        /// <summary>
        /// Exchange a one-time authorization code for tokens (POST, no tokens in URL).
        /// </summary>
        [HttpPost("exchange")]
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
