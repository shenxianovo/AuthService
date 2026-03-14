using System.Security.Cryptography;
using AuthService.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AuthService.Tests.Fixtures
{
    public class ApiTestFixture : IDisposable
    {
        public HttpClient Client { get; }
        private readonly string _tempKeyDir;

        public ApiTestFixture()
        {
            // Generate a test RSA key pair and write to temp files
            _tempKeyDir = Path.Combine(Path.GetTempPath(), $"authservice-test-keys-{Guid.NewGuid()}");
            Directory.CreateDirectory(_tempKeyDir);

            using var rsa = RSA.Create(2048);
            var privateKeyPath = Path.Combine(_tempKeyDir, "private.pem");
            var publicKeyPath = Path.Combine(_tempKeyDir, "public.pem");
            File.WriteAllText(privateKeyPath, rsa.ExportRSAPrivateKeyPem());
            File.WriteAllText(publicKeyPath, rsa.ExportRSAPublicKeyPem());

            var factory = new WebApplicationFactory<Program>()
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

                        // Add InMemory database
                        services.AddDbContext<AppDbContext>(options =>
                            options.UseInMemoryDatabase("TestDb"));
                    });
                });

            Client = factory.CreateClient();
        }

        public void Dispose()
        {
            Client.Dispose();
            if (Directory.Exists(_tempKeyDir))
                Directory.Delete(_tempKeyDir, recursive: true);
        }
    }

    [CollectionDefinition("Api Tests")]
    public class ApiTestCollection : ICollectionFixture<ApiTestFixture> { }
}
