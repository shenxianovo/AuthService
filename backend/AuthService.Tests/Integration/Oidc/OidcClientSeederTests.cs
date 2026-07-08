using AuthService.Configuration;
using AuthService.Services;
using AuthService.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;

namespace AuthService.Tests.Integration.Oidc
{
    [Collection("Api Tests")]
    public class OidcClientSeederTests(ApiTestFixture fixture)
    {
        [Fact]
        public async Task Seeder_RegistersConfiguredClient()
        {
            using var scope = fixture.CreateScope();
            var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

            var app = await manager.FindByClientIdAsync("test-client", TestContext.Current.CancellationToken);

            Assert.NotNull(app);
            Assert.Equal("Test Client", await manager.GetDisplayNameAsync(app, TestContext.Current.CancellationToken));
            Assert.True(await manager.ValidateClientSecretAsync(app, "test-secret", TestContext.Current.CancellationToken));

            var permissions = await manager.GetPermissionsAsync(app, TestContext.Current.CancellationToken);
            Assert.Contains(OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode, permissions);
            Assert.Contains(OpenIddictConstants.Permissions.Prefixes.Scope + "profile", permissions);
        }

        [Fact]
        public async Task Seeder_IsIdempotent_AcrossRestarts()
        {
            // Simulate a second startup: run the seeder again with the same config.
            var options = fixture.Factory.Services.GetRequiredService<IOptions<OidcOptions>>();
            var seeder = new OidcClientSeeder(
                fixture.Factory.Services, options, NullLogger<OidcClientSeeder>.Instance);
            await seeder.StartAsync(TestContext.Current.CancellationToken);

            using var scope = fixture.CreateScope();
            var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

            var count = 0L;
            await foreach (var app in manager.ListAsync(cancellationToken: TestContext.Current.CancellationToken))
            {
                if (await manager.GetClientIdAsync(app, TestContext.Current.CancellationToken) == "test-client")
                    count++;
            }
            Assert.Equal(1, count);

            // The updated registration still validates the same secret.
            var updated = await manager.FindByClientIdAsync("test-client", TestContext.Current.CancellationToken);
            Assert.NotNull(updated);
            Assert.True(await manager.ValidateClientSecretAsync(updated, "test-secret", TestContext.Current.CancellationToken));
        }
    }
}
