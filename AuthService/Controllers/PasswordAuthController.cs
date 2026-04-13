using AuthService.DTOs.Auth;
using AuthService.Extensions;
using AuthService.Services;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.Controllers
{
    [ApiController]
    [Route("api/v1/auth")]
    public class PasswordAuthController(IPasswordAuthService passwordAuthService) : ControllerBase
    {
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var device = Request.Headers.UserAgent.ToString();
            var result = await passwordAuthService.RegisterAsync(request, ipAddress, device);
            return result.IsSuccess ? Ok(result.Value) : this.ToErrorResponse(result.Error);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var device = Request.Headers.UserAgent.ToString();
            var result = await passwordAuthService.LoginAsync(request, ipAddress, device);
            return result.IsSuccess ? Ok(result.Value) : this.ToErrorResponse(result.Error);
        }
    }
}