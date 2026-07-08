using AuthService.Common;
using AuthService.Data;
using AuthService.DTOs.Admin;
using AuthService.Services;
using AuthService.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace AuthService.Tests.Integration.Admin
{
    /// <summary>
    /// Delete goes through OpenIddict's ExecuteDelete-based store method, which the
    /// InMemory provider cannot translate — so the happy path runs against the real
    /// Postgres container.
    /// </summary>
    [Collection(PostgresCollection.Name)]
    public class OidcClientAdminPostgresTests(PostgresContainerFixture fixture)
    {
        private ServiceProvider BuildProvider()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddMemoryCache();
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(fixture.ConnectionString).UseOpenIddict());
            services.AddOpenIddict()
                .AddCore(options => options.UseEntityFrameworkCore().UseDbContext<AppDbContext>());
            return services.BuildServiceProvider();
        }

        [Fact]
        public async Task Delete_RemovesTheClient()
        {
            await using var provider = BuildProvider();
            await using var scope = provider.CreateAsyncScope();
            var service = new OidcClientAdminService(
                scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>());

            var clientId = $"pg-del-{Guid.NewGuid():N}"[..20];
            var created = await service.CreateAsync(new CreateOidcClientRequest
            {
                ClientId = clientId,
                DisplayName = "Postgres Delete",
                RedirectUris = ["https://pgdel.example.com/cb"],
            }, TestContext.Current.CancellationToken);
            Assert.True(created.IsSuccess);

            var delete = await service.DeleteAsync(clientId, TestContext.Current.CancellationToken);
            Assert.True(delete.IsSuccess);

            var deleteAgain = await service.DeleteAsync(clientId, TestContext.Current.CancellationToken);
            Assert.False(deleteAgain.IsSuccess);
            Assert.Equal(AuthError.OidcClientNotFound, deleteAgain.Error);
        }
    }
}
