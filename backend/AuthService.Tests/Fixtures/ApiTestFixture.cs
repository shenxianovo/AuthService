using System.Security.Claims;
using AuthService.Data;
using AuthService.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AuthService.Tests.Fixtures
{
    public class ApiTestFixture : IAsyncLifetime
    {
        public HttpClient Client { get; private set; } = null!;
        public WebApplicationFactory<Program> Factory { get; private set; } = null!;

        public async ValueTask InitializeAsync()
        {
            Factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureAppConfiguration((context, config) =>
                    {
                        config.AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            // Must be an absolute URI: OpenIddict's SetIssuer(new Uri(...)) rejects
                            // plain strings like "test-issuer".
                            ["Jwt:Issuer"] = "https://localhost",
                            ["Jwt:Audience"] = "test-audience",
                            ["Jwt:AccessTokenExpirationMinutes"] = "15",
                            ["Jwt:RefreshTokenExpirationDays"] = "30",
                            ["Jwt:SessionExpirationDays"] = "30",
                            ["OAuthSecurity:AllowedRedirectOrigins:0"] = "https://example.com",
                            ["OAuthSecurity:AuthCodeExpirationSeconds"] = "60",
                            ["OAuthSecurity:StateExpirationSeconds"] = "600",
                            // Fixed 32-byte key so OpenIddict server startup validation passes.
                            ["Oidc:EncryptionKey"] = "3q2+7wEjRRSKPfXpVGVRVKgXltV7Kbk9sMkY1u8F0z4=",
                            // Seeded OIDC client used by the authorization code flow tests.
                            ["Oidc:Clients:0:ClientId"] = "test-client",
                            ["Oidc:Clients:0:ClientSecret"] = "test-secret",
                            ["Oidc:Clients:0:DisplayName"] = "Test Client",
                            ["Oidc:Clients:0:RedirectUris:0"] = "https://client.example.com/api/auth/sso_callback",
                            ["Oidc:Clients:0:Scopes:0"] = "profile",
                            ["Oidc:Clients:0:Scopes:1"] = "email",
                            // Public (SPA) client: no secret, PKCE enforced per client.
                            ["Oidc:Clients:1:ClientId"] = "test-spa",
                            ["Oidc:Clients:1:DisplayName"] = "Test SPA",
                            ["Oidc:Clients:1:Type"] = "public",
                            ["Oidc:Clients:1:RedirectUris:0"] = "https://spa.example.com/callback",
                            ["Oidc:Clients:1:Scopes:0"] = "profile",
                        });
                    });

                    builder.ConfigureServices(services =>
                    {
                        // Replace the file-based key provider with an in-memory one
                        // (no temp PEM files needed for tests).
                        var keyDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IRsaKeyProvider));
                        if (keyDescriptor != null) services.Remove(keyDescriptor);
                        services.AddSingleton<IRsaKeyProvider, InMemoryRsaKeyProvider>();

                        // Remove all DbContext and EF Core related registrations.
                        // OpenIddict's EF Core stores live in the OpenIddict.EntityFrameworkCore
                        // namespace but must survive this sweep — they are provider-agnostic.
                        var efServiceTypes = services
                            .Where(d => d.ServiceType.FullName != null &&
                                       (d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                                     || d.ServiceType == typeof(DbContextOptions)
                                     || d.ServiceType == typeof(AppDbContext)
                                     || (d.ServiceType.FullName.Contains("EntityFrameworkCore")
                                      && !d.ServiceType.FullName.StartsWith("OpenIddict"))))
                            .ToList();
                        foreach (var d in efServiceTypes)
                            services.Remove(d);

                        // Add InMemory database with unique name per fixture instance.
                        // OpenIddict's EF store wraps deletes in a transaction; InMemory
                        // throws on transactions unless the warning is suppressed.
                        var dbName = $"TestDb-{Guid.NewGuid()}";
                        services.AddDbContext<AppDbContext>(options =>
                            options.UseInMemoryDatabase(dbName)
                                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                                .UseOpenIddict());

                        // Replace real email service with a recording no-op fake
                        // (tests don't send emails, but can read what would be sent).
                        var emailDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IEmailService));
                        if (emailDescriptor != null) services.Remove(emailDescriptor);
                        services.AddSingleton<RecordingEmailService>();
                        services.AddSingleton<IEmailService>(sp => sp.GetRequiredService<RecordingEmailService>());
                    });
                });

            Client = Factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false // important for OAuth redirect tests
            });

            await Task.CompletedTask;
        }

        /// <summary>
        /// Create a scope to access services (e.g. for seeding data or getting JWT tokens).
        /// </summary>
        public IServiceScope CreateScope() => Factory.Services.CreateScope();

        /// <summary>
        /// Generate an access token for a given user ID using the test JwtService.
        /// </summary>
        public string GenerateAccessToken(Guid userId, Guid? sessionId = null)
        {
            var jwtService = Factory.Services.GetRequiredService<IJwtService>();
            return jwtService.GenerateAccessToken(userId, new Claim("sid", (sessionId ?? Guid.NewGuid()).ToString()));
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await Factory.DisposeAsync();
        }
    }

    [CollectionDefinition("Api Tests")]
    public class ApiTestCollection : ICollectionFixture<ApiTestFixture> { }
}