using System.Security.Cryptography;
using AuthService.Data;
using AuthService.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AuthService.Tests.Fixtures
{
    public class ApiTestFixture : IAsyncLifetime
    {
        public HttpClient Client { get; private set; } = null!;
        public WebApplicationFactory<Program> Factory { get; private set; } = null!;
        private string _tempKeyDir = null!;

        public async ValueTask InitializeAsync()
        {
            // Generate a test RSA key pair and write to temp files
            _tempKeyDir = Path.Combine(Path.GetTempPath(), $"authservice-test-keys-{Guid.NewGuid()}");
            Directory.CreateDirectory(_tempKeyDir);

            using var rsa = RSA.Create(2048);
            var privateKeyPath = Path.Combine(_tempKeyDir, "private.pem");
            var publicKeyPath = Path.Combine(_tempKeyDir, "public.pem");
            File.WriteAllText(privateKeyPath, rsa.ExportRSAPrivateKeyPem());
            File.WriteAllText(publicKeyPath, rsa.ExportRSAPublicKeyPem());

            Factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureAppConfiguration((context, config) =>
                    {
                        config.AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            ["Jwt:PrivateKeyPath"] = privateKeyPath,
                            ["Jwt:PublicKeyPath"] = publicKeyPath,
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
            return jwtService.GenerateAccessToken(userId, sessionId ?? Guid.NewGuid());
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await Factory.DisposeAsync();
            if (Directory.Exists(_tempKeyDir))
                Directory.Delete(_tempKeyDir, recursive: true);
        }
    }

    [CollectionDefinition("Api Tests")]
    public class ApiTestCollection : ICollectionFixture<ApiTestFixture> { }
}