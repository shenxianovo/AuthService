using System.Security.Claims;
using AuthService.Data;
using AuthService.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;

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
                            ["Jwt:Issuer"] = "test-issuer",
                            ["Jwt:Audience"] = "test-audience",
                            ["Jwt:AccessTokenExpirationMinutes"] = "15",
                            ["Jwt:RefreshTokenExpirationDays"] = "30",
                            ["Jwt:SessionExpirationDays"] = "30",
                            ["OAuthSecurity:AllowedRedirectOrigins:0"] = "https://example.com",
                            ["OAuthSecurity:AuthCodeExpirationSeconds"] = "60",
                            ["OAuthSecurity:StateExpirationSeconds"] = "600",
                        });
                    });

                    builder.ConfigureServices(services =>
                    {
                        // Replace the file-based key provider with an in-memory one
                        // (no temp PEM files needed for tests).
                        var keyDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IRsaKeyProvider));
                        if (keyDescriptor != null) services.Remove(keyDescriptor);
                        services.AddSingleton<IRsaKeyProvider, InMemoryRsaKeyProvider>();

                        // Remove all DbContext and EF Core related registrations
                        var efServiceTypes = services
                            .Where(d => d.ServiceType.FullName != null &&
                                       (d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                                     || d.ServiceType == typeof(DbContextOptions)
                                     || d.ServiceType == typeof(AppDbContext)
                                     || d.ServiceType.FullName.Contains("EntityFrameworkCore")))
                            .ToList();
                        foreach (var d in efServiceTypes)
                            services.Remove(d);

                        // Add InMemory database with unique name per fixture instance
                        var dbName = $"TestDb-{Guid.NewGuid()}";
                        services.AddDbContext<AppDbContext>(options =>
                            options.UseInMemoryDatabase(dbName));

                        // Replace real email service with a no-op mock (tests don't send emails)
                        var emailDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IEmailService));
                        if (emailDescriptor != null) services.Remove(emailDescriptor);
                        services.AddScoped<IEmailService>(_ =>
                            Mock.Of<IEmailService>(m =>
                                m.SendVerificationCodeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()) == Task.CompletedTask));
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