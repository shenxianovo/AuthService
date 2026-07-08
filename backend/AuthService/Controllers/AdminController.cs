using AuthService.Common;
using AuthService.DTOs.Admin;
using AuthService.Entities;
using AuthService.Extensions;
using AuthService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.Controllers
{
    /// <summary>
    /// Admin surface. Authorization goes through the RequireAdmin policy, which
    /// consults the database per request — no role claim exists in any token.
    /// </summary>
    [ApiController]
    [Route("api/v1/admin")]
    [Produces("application/json")]
    [Authorize(Policy = AuthConstants.AdminPolicy)]
    public class AdminController(IAdminService adminService) : ControllerBase
    {
        /// <summary>Promote or demote a user. Demoting the last admin is refused.</summary>
        [HttpPut("users/{userId:guid}/role")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> SetRole(Guid userId, [FromBody] SetRoleRequest request)
        {
            if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var role))
                return BadRequest(new { message = "Unknown role." });

            var result = await adminService.SetRoleAsync(userId, role);
            return result.IsSuccess ? NoContent() : this.ToErrorResponse(result.Error);
        }
    }
}
