using AuthService.DTOs.Auth;
using AuthService.Exceptions;
using AuthService.Extensions;
using AuthService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

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
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var device = Request.Headers.UserAgent.ToString();
            var result = await passwordAuthService.RegisterAsync(request, ipAddress, device);
            return result.IsSuccess ? Ok(result.Value) : this.ToErrorResponse(result.Error);
        }

        [HttpPost("login")]
        [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var device = Request.Headers.UserAgent.ToString();
            var result = await passwordAuthService.LoginAsync(request, ipAddress, device);
            return result.IsSuccess ? Ok(result.Value) : this.ToErrorResponse(result.Error);
        }

        [Authorize]
        [HttpPost("email/send-code")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> SendVerificationCode()
        {
            var userId = GetCurrentUserId();
            await emailVerificationService.SendVerificationCodeAsync(userId);
            return Ok(new { message = "验证码已发送。" });
        }

        [Authorize]
        [HttpPost("email/verify")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request)
        {
            var userId = GetCurrentUserId();
            await emailVerificationService.VerifyCodeAsync(userId, request.Code);
            return Ok(new { message = "邮箱验证成功。" });
        }

        [Authorize]
        [HttpPost("email")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> AddEmail([FromBody] AddEmailRequest request)
        {
            var userId = GetCurrentUserId();
            await emailManagementService.AddEmailAsync(userId, request.Email);
            return Ok(new { message = "邮箱已添加，请查收验证码。" });
        }

        [Authorize]
        [HttpDelete("email/{email}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RemoveEmail(string email)
        {
            var userId = GetCurrentUserId();
            await emailManagementService.RemoveEmailAsync(userId, email);
            return Ok(new { message = "邮箱已删除。" });
        }

        [Authorize]
        [HttpPut("email/{email}/primary")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SetPrimaryEmail(string email)
        {
            var userId = GetCurrentUserId();
            await emailManagementService.SetPrimaryEmailAsync(userId, email);
            return Ok(new { message = "主邮箱已更新。" });
        }

        private Guid GetCurrentUserId()
        {
            var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub")
                ?? throw new UnauthorizedException("未登录或令牌无效。");
            return Guid.Parse(sub);
        }
    }
}
