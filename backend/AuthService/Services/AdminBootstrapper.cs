using AuthService.Configuration;
using AuthService.Data;
using AuthService.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AuthService.Services
{
    /// <summary>
    /// Promotes the configured bootstrap user to Admin at startup (idempotent,
    /// same pattern as OidcClientSeeder). If the user doesn't exist yet, logs a
    /// warning and does nothing — the promotion happens on the next restart
    /// after registration, or an existing admin can promote via the API.
    /// </summary>
    public sealed class AdminBootstrapper(
        IServiceProvider serviceProvider,
        IOptions<AdminOptions> options,
        ILogger<AdminBootstrapper> logger) : IHostedService
    {
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var username = options.Value.BootstrapUsername;
            if (string.IsNullOrEmpty(username))
                return;

            await using var scope = serviceProvider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var user = await db.Users.FirstOrDefaultAsync(u => u.Username == username, cancellationToken);
            if (user is null)
            {
                logger.LogWarning(
                    "Bootstrap admin '{Username}' not found; register the account and restart to promote it.",
                    username);
                return;
            }

            if (user.Role != UserRole.Admin)
            {
                user.Role = UserRole.Admin;
                await db.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Bootstrap admin '{Username}' promoted.", username);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
