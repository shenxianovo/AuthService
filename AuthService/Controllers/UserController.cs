using System.Security.Claims;
using AuthService.Data;
using AuthService.DTOs.Auth;
using AuthService.Entities;
using AuthService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Controllers
{
    [ApiController]
    [Route("api/v1/auth")]
    public class UserController(
        IPasswordAuthService passwordAuthService,
        AppDbContext db) : ControllerBase
    {
        [Authorize]
        [HttpPost("add-password")]
        public async Task<IActionResult> AddPassword([FromBody] AddPasswordRequest request)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!Guid.TryParse(userIdClaim, out var userId))
                    return Unauthorized();

                await passwordAuthService.AddPasswordAsync(userId, request.Password);
                return Ok(new { message = "Password added successfully." });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> GetMe()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized();

            var user = await db.Users
                .Include(u => u.Emails)
                .Include(u => u.Providers)
                .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);

            if (user == null)
                return NotFound();

            var hasPassword = await db.PasswordCredentials.AnyAsync(p => p.UserId == userId);

            return Ok(new UserInfoResponse
            {
                UserId = user.Id,
                DisplayName = user.DisplayName,
                CreatedAt = user.CreatedAt,
                HasPassword = hasPassword,
                Emails = user.Emails.Select(e => new EmailInfo
                {
                    Email = e.Email,
                    IsPrimary = e.IsPrimary,
                    IsVerified = e.VerifiedAt.HasValue
                }).ToList(),
                Providers = user.Providers
                    .Where(p => p.Provider != AuthProviderType.Password)
                    .Select(p => new ProviderInfo
                    {
                        Provider = p.Provider.ToString(),
                        LinkedAt = p.CreatedAt
                    }).ToList()
            });
        }
    }
}
