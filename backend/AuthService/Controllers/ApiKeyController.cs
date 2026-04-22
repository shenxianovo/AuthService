using System.Security.Claims;
using AuthService.DTOs.ApiKeys;
using AuthService.Extensions;
using AuthService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.Controllers
{
    [ApiController]
    [Route("api/v1/apikeys")]
    [Produces("application/json")]
    public class ApiKeyController(IApiKeyService apiKeyService) : ControllerBase
    {
        /// <summary>
        /// Create a new API key for the authenticated user.
        /// The full key is returned only once in the response — store it securely.
        /// </summary>
        [Authorize]
        [HttpPost]
        [ProducesResponseType<CreateApiKeyResponse>(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Create([FromBody] CreateApiKeyRequest request)
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var response = await apiKeyService.CreateAsync(userId, request.Name);
            return CreatedAtAction(nameof(List), null, response);
        }

        /// <summary>
        /// List all API keys belonging to the authenticated user.
        /// The secret portion of the key is never returned.
        /// </summary>
        [Authorize]
        [HttpGet]
        [ProducesResponseType<List<ApiKeyListItem>>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> List()
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var list = await apiKeyService.ListAsync(userId);
            return Ok(list);
        }

        /// <summary>
        /// Revoke (soft-delete) an API key. Already-issued JWTs remain valid until expiry.
        /// </summary>
        [Authorize]
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Revoke(Guid id)
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await apiKeyService.RevokeAsync(userId, id);
            return result.IsSuccess ? NoContent() : this.ToErrorResponse(result.Error);
        }

        /// <summary>
        /// Exchange a raw API key for a short-lived JWT access token.
        /// Downstream services can verify this JWT offline using the AuthService public key.
        /// </summary>
        [HttpPost("exchange")]
        [ProducesResponseType<ExchangeApiKeyResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Exchange([FromBody] ExchangeApiKeyRequest request)
        {
            var result = await apiKeyService.ExchangeAsync(request.ApiKey);
            return result.IsSuccess ? Ok(result.Value) : this.ToErrorResponse(result.Error);
        }
    }
}