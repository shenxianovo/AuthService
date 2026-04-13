using System.Security.Claims;
using AuthService.DTOs.Auth;
using AuthService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.Controllers
{
    [ApiController]
    [Route("api/v1/auth")]
    public class UserController(
        IPasswordAuthService passwordAuthService,
        IUserService userService) : ControllerBase
    {
        [Authorize]
        [HttpPost("add-password")]
        public async Task<IActionResult> AddPassword([FromBody] AddPasswordRequest request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized();

            await passwordAuthService.AddPasswordAsync(userId, request.Password);
            return Ok(new { message = "Password added successfully." });
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> GetMe()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized();

            var userInfo = await userService.GetUserInfoAsync(userId);
            if (userInfo is null)
                return NotFound();

            return Ok(userInfo);
        }
    }
}