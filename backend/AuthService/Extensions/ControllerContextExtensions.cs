using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.Extensions
{
    /// <summary>
    /// Helpers for pulling common values out of the request/principal, so controllers
    /// don't each re-spell claim lookups and request-metadata extraction.
    /// </summary>
    public static class ControllerContextExtensions
    {
        /// <summary>
        /// The authenticated user's id. Use only on [Authorize] endpoints, where the
        /// claim is guaranteed present — a missing claim indicates a pipeline bug, not
        /// a client error, so this throws rather than returning a status.
        /// </summary>
        public static Guid GetUserId(this ControllerBase controller)
        {
            var sub = controller.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? controller.User.FindFirstValue("sub")
                ?? throw new InvalidOperationException("Missing user ID claim in authenticated request.");
            return Guid.Parse(sub);
        }

        /// <summary>
        /// Try to read the authenticated user's id. Returns false if the claim is
        /// absent or unparseable, letting the caller return Unauthorized().
        /// </summary>
        public static bool TryGetUserId(this ControllerBase controller, out Guid userId)
        {
            var sub = controller.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? controller.User.FindFirstValue("sub");
            return Guid.TryParse(sub, out userId);
        }

        /// <summary>Client IP and user-agent, as passed to session creation.</summary>
        public static (string IpAddress, string Device) GetClientContext(this ControllerBase controller)
        {
            var ip = controller.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var device = controller.Request.Headers.UserAgent.ToString();
            return (ip, device);
        }
    }
}
