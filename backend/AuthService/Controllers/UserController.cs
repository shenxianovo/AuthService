using AuthService.Data;
using AuthService.DTOs.Auth;
using AuthService.Entities;
using AuthService.Extensions;
using AuthService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.Controllers
{
    [ApiController]
    [Route("api/v1/auth")]
    [Produces("application/json")]
    public class UserController(
        IPasswordAuthService passwordAuthService,
        IAccountService accountService,
        IUserService userService,
        ISessionService sessionService,
        AppDbContext db) : ControllerBase
    {
        [Authorize]
        [HttpPost("add-password")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> AddPassword([FromBody] AddPasswordRequest request)
        {
            if (!this.TryGetUserId(out var userId))
                return Unauthorized();

            var result = await passwordAuthService.AddPasswordAsync(userId, request.Password);
            return result.IsSuccess ? Ok(new { message = "Password added successfully." }) : this.ToErrorResponse(result.Error);
        }

        [Authorize]
        [HttpGet("me")]
        [ProducesResponseType<UserInfoResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetMe()
        {
            if (!this.TryGetUserId(out var userId))
                return Unauthorized();

            var userInfo = await userService.GetUserInfoAsync(userId);
            if (userInfo is null)
                return NotFound();

            return Ok(userInfo);
        }

        [Authorize]
        [HttpPatch("username")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> ChangeUsername([FromBody] ChangeUsernameRequest request)
        {
            if (!this.TryGetUserId(out var userId))
                return Unauthorized();

            var result = await accountService.ChangeUsernameAsync(userId, request.Username);
            if (!result.IsSuccess)
                return this.ToErrorResponse(result.Error);

            if (result.Value)
            {
                // Old tokens carry the stale preferred_username; force re-login
                // everywhere. Revocation defers its commit, so the rename and the
                // revocation land in one SaveChanges — atomic.
                await sessionService.RevokeAllSessionsAsync(userId);
                await db.SaveChangesAsync();
            }

            return Ok(new { message = "Username changed successfully." });
        }

        [Authorize]
        [HttpDelete("unlink-provider")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UnlinkProvider([FromBody] UnlinkProviderRequest request)
        {
            if (!this.TryGetUserId(out var userId))
                return Unauthorized();

            if (!Enum.TryParse<AuthProviderType>(request.Provider, ignoreCase: true, out var providerType)
                || providerType == AuthProviderType.Password)
                return BadRequest(new { message = "Invalid provider." });

            var result = await accountService.UnlinkProviderAsync(userId, providerType);
            if (!result.IsSuccess)
                return this.ToErrorResponse(result.Error, result.ErrorMessage);

            await db.SaveChangesAsync();
            return Ok(new { message = $"{request.Provider} account unlinked successfully." });
        }
    }
}
