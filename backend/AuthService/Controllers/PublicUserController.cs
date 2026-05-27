using AuthService.DTOs.User;
using AuthService.Services;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.Controllers
{
    [ApiController]
    [Route("api/v1/users")]
    [Produces("application/json")]
    public class PublicUserController(IUserService userService) : ControllerBase
    {
        /// <summary>
        /// Public lookup: resolve a username to its (id, username, displayName).
        /// Used by other services (e.g. Heartbeat) to map URL slugs to user identifiers.
        /// No authentication required.
        /// </summary>
        [HttpGet("{username}")]
        [ProducesResponseType<PublicUserResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetByUsername(string username)
        {
            var user = await userService.GetPublicUserByUsernameAsync(username);
            return user is null ? NotFound() : Ok(user);
        }
    }
}