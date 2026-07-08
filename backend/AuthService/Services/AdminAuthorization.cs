using System.Security.Claims;
using AuthService.Data;
using AuthService.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Services
{
    /// <summary>
    /// Admin authorization consults the database on every request instead of a
    /// token claim: grants/revocations are immediate, and — the load-bearing
    /// property — Role can never leak into tokens for downstream services to
    /// build on (CONTEXT.md "Role", ADR-017). Admin traffic is tiny, so the
    /// per-request indexed lookup is irrelevant.
    /// </summary>
    public sealed class AdminRequirement : IAuthorizationRequirement;

    public sealed class AdminRequirementHandler(AppDbContext db) : AuthorizationHandler<AdminRequirement>
    {
        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context, AdminRequirement requirement)
        {
            var sub = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? context.User.FindFirstValue("sub");
            if (!Guid.TryParse(sub, out var userId))
                return;

            var isAdmin = await db.Users.AnyAsync(u => u.Id == userId && u.Role == UserRole.Admin);
            if (isAdmin)
                context.Succeed(requirement);
        }
    }
}
