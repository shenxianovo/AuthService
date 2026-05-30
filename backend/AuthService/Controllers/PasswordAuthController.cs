using AuthService.DTOs.Auth;
using AuthService.Extensions;
using AuthService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace AuthService.Controllers
{
    [ApiController]
    [Route("api/v1/auth")]
    [Produces("application/json")]
    public class PasswordAuthController(
        IPasswordAuthService passwordAuthService,
        IEmailVerificationService emailVerificationService,
        IEmailManagementService emailManagementService) : ControllerBase
    {
        [HttpPost("register")]
        [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            var (ipAddress, device) = this.GetClientContext();
            var result = await passwordAuthService.RegisterAsync(request, ipAddress, device);
            return result.IsSuccess ? Ok(result.Value) : this.ToErrorResponse(result.Error);
        }

        [HttpPost("login")]
        [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var (ipAddress, device) = this.GetClientContext();
            var result = await passwordAuthService.LoginAsync(request, ipAddress, device);
            return result.IsSuccess ? Ok(result.Value) : this.ToErrorResponse(result.Error);
        }

        [Authorize]
        [HttpPost("email/send-code")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> SendVerificationCode([FromQuery] string? email = null)
        {
            if (email is not null && !IsValidEmail(email))
                return BadRequest(new { message = "Invalid email format." });

            var userId = this.GetUserId();
            var result = await emailVerificationService.SendVerificationCodeAsync(userId, email != null ? EmailTarget.ByAddress(email) : EmailTarget.Primary);
            return result.IsSuccess ? Ok(new { message = "Verification code sent." }) : this.ToErrorResponse(result.Error);
        }

        [Authorize]
        [HttpPost("email/verify")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request, [FromQuery] string? email = null)
        {
            if (email is not null && !IsValidEmail(email))
                return BadRequest(new { message = "Invalid email format." });

            var userId = this.GetUserId();
            var result = await emailVerificationService.VerifyCodeAsync(userId, request.Code, email != null ? EmailTarget.ByAddress(email) : EmailTarget.Primary);
            return result.IsSuccess ? Ok(new { message = "Email verified." }) : this.ToErrorResponse(result.Error);
        }

        [Authorize]
        [HttpPost("email")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> AddEmail([FromBody] AddEmailRequest request)
        {
            var userId = this.GetUserId();
            var result = await emailManagementService.AddEmailAsync(userId, request.Email);
            return result.IsSuccess ? Ok(new { message = "Email added. Verification code sent." }) : this.ToErrorResponse(result.Error);
        }

        [Authorize]
        [HttpDelete("email/{email}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RemoveEmail(string email)
        {
            if (!IsValidEmail(email))
                return BadRequest(new { message = "Invalid email format." });

            var userId = this.GetUserId();
            var result = await emailManagementService.RemoveEmailAsync(userId, email);
            return result.IsSuccess ? Ok(new { message = "Email removed." }) : this.ToErrorResponse(result.Error);
        }

        [Authorize]
        [HttpPut("email/{email}/primary")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SetPrimaryEmail(string email)
        {
            if (!IsValidEmail(email))
                return BadRequest(new { message = "Invalid email format." });

            var userId = this.GetUserId();
            var result = await emailManagementService.SetPrimaryEmailAsync(userId, email);
            return result.IsSuccess ? Ok(new { message = "Primary email updated." }) : this.ToErrorResponse(result.Error);
        }

        private static bool IsValidEmail(string email)
            => new EmailAddressAttribute().IsValid(email);
    }
}
