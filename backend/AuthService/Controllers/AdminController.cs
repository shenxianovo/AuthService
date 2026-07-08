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
    public class AdminController(
        IAdminService adminService,
        IOidcClientAdminService oidcClients) : ControllerBase
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

        // ===================== OIDC clients =====================

        [HttpGet("oidc-clients")]
        [ProducesResponseType<List<OidcClientSummary>>(StatusCodes.Status200OK)]
        public async Task<IActionResult> ListOidcClients()
            => Ok(await oidcClients.ListAsync(HttpContext.RequestAborted));

        /// <summary>Create a client. The secret is returned exactly once.</summary>
        [HttpPost("oidc-clients")]
        [ProducesResponseType<CreateOidcClientResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateOidcClient([FromBody] CreateOidcClientRequest request)
        {
            var result = await oidcClients.CreateAsync(request, HttpContext.RequestAborted);
            return result.IsSuccess ? Ok(result.Value) : this.ToErrorResponse(result.Error, result.ErrorMessage);
        }

        /// <summary>Update display name, redirect URIs and scopes. Type is immutable.</summary>
        [HttpPut("oidc-clients/{clientId}")]
        [ProducesResponseType<OidcClientSummary>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateOidcClient(string clientId, [FromBody] UpdateOidcClientRequest request)
        {
            var result = await oidcClients.UpdateAsync(clientId, request, HttpContext.RequestAborted);
            return result.IsSuccess ? Ok(result.Value) : this.ToErrorResponse(result.Error, result.ErrorMessage);
        }

        /// <summary>Rotate a confidential client's secret. Returned exactly once.</summary>
        [HttpPost("oidc-clients/{clientId}/regenerate-secret")]
        [ProducesResponseType<RegenerateSecretResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RegenerateOidcClientSecret(string clientId)
        {
            var result = await oidcClients.RegenerateSecretAsync(clientId, HttpContext.RequestAborted);
            return result.IsSuccess ? Ok(result.Value) : this.ToErrorResponse(result.Error, result.ErrorMessage);
        }

        [HttpDelete("oidc-clients/{clientId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteOidcClient(string clientId)
        {
            var result = await oidcClients.DeleteAsync(clientId, HttpContext.RequestAborted);
            return result.IsSuccess ? NoContent() : this.ToErrorResponse(result.Error);
        }
    }
}
