using AuthService.Data;
using AuthService.Services;
using AuthService.Tests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AuthService.Tests.Integration
{
    /// <summary>
    /// A wildcard redirect origin must fail options validation, not silently
    /// reopen the subdomain redirect surface the ?redirect= era left behind
    /// (issue 06, 2026-07-13). Resolving the options triggers the Validate
    /// delegate wired in Program.cs.
    /// </summary>
    public class RedirectOriginStartupValidationTests
    {
        [Fact]
        public void WildcardOrigin_FailsOptionsValidation()
        {
            using var factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureAppConfiguration((_, config) =>
                        config.AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            ["Jwt:Issuer"] = "https://localhost",
                            ["Jwt:Audience"] = "test-audience",
                            ["Oidc:EncryptionKey"] = "3q2+7wEjRRSKPfXpVGVRVKgXltV7Kbk9sMkY1u8F0z4=",
                            // The line under test: a wildcard must be rejected.
                            ["OAuthSecurity:AllowedRedirectOrigins:0"] = "https://*.shenxianovo.com",
                        }));

                    // InMemory DB + key provider so the host builds without Postgres
                    // or PEM files (mirrors ApiTestFixture); the options guard is
                    // independent of these but the host must construct to reach it.
                    builder.ConfigureServices(services =>
                    {
                        var keyDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IRsaKeyProvider));
                        if (keyDescriptor != null) services.Remove(keyDescriptor);
                        services.AddSingleton<IRsaKeyProvider, InMemoryRsaKeyProvider>();

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

                        services.AddDbContext<AppDbContext>(options =>
                            options.UseInMemoryDatabase($"TestDb-{Guid.NewGuid()}")
                                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                                .UseOpenIddict());
                    });
                });

            // ValidateOnStart surfaces the failure when the host's service
            // provider is first built (accessing factory.Services), so assert
            // around that rather than a later .Value read.
            var ex = Assert.Throws<OptionsValidationException>(() =>
            {
                using var scope = factory.Services.CreateScope();
                _ = scope.ServiceProvider
                    .GetRequiredService<IOptions<AuthService.Configuration.OAuthSecurityOptions>>().Value;
            });
            Assert.Contains("wildcard", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
    }
}
