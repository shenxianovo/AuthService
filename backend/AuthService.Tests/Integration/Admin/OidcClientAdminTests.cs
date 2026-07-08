using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AuthService.Data;
using AuthService.DTOs.Admin;
using AuthService.DTOs.Auth;
using AuthService.Entities;
using AuthService.Tests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace AuthService.Tests.Integration.Admin
{
    [Collection("Api Tests")]
    public class OidcClientAdminTests(ApiTestFixture fixture)
    {
        private async Task<(HttpClient client, AuthResponse admin)> CreateAdminClientAsync()
        {
            var client = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            var request = new RegisterRequest
            {
                Username = $"u{Guid.NewGuid():N}"[..15],
                DisplayName = "ClientAdmin",
                Email = $"oca-{Guid.NewGuid():N}@example.com",
                Password = "SecurePass123",
            };
            var response = await client.PostAsJsonAsync("/api/v1/auth/register", request, TestContext.Current.CancellationToken);
            response.EnsureSuccessStatusCode();
            var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(TestContext.Current.CancellationToken))!;

            using var scope = fixture.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(u => u.Id == auth.UserId, TestContext.Current.CancellationToken);
            user.Role = UserRole.Admin;
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
            return (client, auth);
        }

        [Fact]
        public async Task NonAdmin_CannotListClients()
        {
            var client = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            var request = new RegisterRequest
            {
                Username = $"u{Guid.NewGuid():N}"[..15],
                DisplayName = "PlainUser",
                Email = $"oca-{Guid.NewGuid():N}@example.com",
                Password = "SecurePass123",
            };
            var reg = await client.PostAsJsonAsync("/api/v1/auth/register", request, TestContext.Current.CancellationToken);
            var auth = (await reg.Content.ReadFromJsonAsync<AuthResponse>(TestContext.Current.CancellationToken))!;
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

            var response = await client.GetAsync("/api/v1/admin/oidc-clients", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task CreatedClient_CompletesTheAuthorizationCodeFlow()
        {
            var (client, _) = await CreateAdminClientAsync();
            var clientId = $"ui-client-{Guid.NewGuid():N}"[..20];
            const string redirectUri = "https://ui.example.com/callback";

            // Create via the admin API — the secret comes back exactly once.
            var create = await client.PostAsJsonAsync("/api/v1/admin/oidc-clients", new CreateOidcClientRequest
            {
                ClientId = clientId,
                DisplayName = "UI Created",
                Type = "confidential",
                RedirectUris = [redirectUri],
                Scopes = ["profile"],
            }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, create.StatusCode);
            var created = (await create.Content.ReadFromJsonAsync<CreateOidcClientResponse>(TestContext.Current.CancellationToken))!;
            Assert.NotNull(created.ClientSecret);

            // The admin registered via this HttpClient, so its cookie jar already
            // holds the interactive cookie — run the code flow with the new client.
            var authorize = await client.GetAsync(
                $"/connect/authorize?client_id={clientId}&redirect_uri={Uri.EscapeDataString(redirectUri)}"
                + "&response_type=code&scope=openid%20profile&state=ui",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Found, authorize.StatusCode);
            var code = QueryHelpers.ParseQuery(authorize.Headers.Location!.Query)["code"].ToString();

            var token = await client.PostAsync("/connect/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "authorization_code",
                    ["code"] = code,
                    ["redirect_uri"] = redirectUri,
                    ["client_id"] = clientId,
                    ["client_secret"] = created.ClientSecret!,
                }),
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, token.StatusCode);

            var tokens = JsonDocument.Parse(await token.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).RootElement;
            Assert.NotEmpty(tokens.GetProperty("id_token").GetString()!);
        }

        [Fact]
        public async Task Update_ChangesFields_ButPreservesTheSecret()
        {
            var (client, _) = await CreateAdminClientAsync();
            var clientId = $"upd-client-{Guid.NewGuid():N}"[..20];

            var create = await client.PostAsJsonAsync("/api/v1/admin/oidc-clients", new CreateOidcClientRequest
            {
                ClientId = clientId,
                DisplayName = "Before",
                RedirectUris = ["https://before.example.com/cb"],
                Scopes = ["profile"],
            }, TestContext.Current.CancellationToken);
            var created = (await create.Content.ReadFromJsonAsync<CreateOidcClientResponse>(TestContext.Current.CancellationToken))!;

            var update = await client.PutAsJsonAsync($"/api/v1/admin/oidc-clients/{clientId}", new UpdateOidcClientRequest
            {
                DisplayName = "After",
                RedirectUris = ["https://after.example.com/cb"],
                Scopes = ["profile", "email"],
            }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, update.StatusCode);
            var updated = (await update.Content.ReadFromJsonAsync<OidcClientSummary>(TestContext.Current.CancellationToken))!;
            Assert.Equal("After", updated.DisplayName);
            Assert.Equal(["https://after.example.com/cb"], updated.RedirectUris);
            Assert.Contains("email", updated.Scopes);

            // The original secret must survive the update (hashed value round-tripped).
            using var scope = fixture.CreateScope();
            var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
            var application = await manager.FindByClientIdAsync(clientId, TestContext.Current.CancellationToken);
            Assert.True(await manager.ValidateClientSecretAsync(application!, created.ClientSecret!, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task RegenerateSecret_InvalidatesTheOldOne()
        {
            var (client, _) = await CreateAdminClientAsync();
            var clientId = $"rot-client-{Guid.NewGuid():N}"[..20];

            var create = await client.PostAsJsonAsync("/api/v1/admin/oidc-clients", new CreateOidcClientRequest
            {
                ClientId = clientId,
                DisplayName = "Rotate",
                RedirectUris = ["https://rot.example.com/cb"],
            }, TestContext.Current.CancellationToken);
            var created = (await create.Content.ReadFromJsonAsync<CreateOidcClientResponse>(TestContext.Current.CancellationToken))!;

            var regen = await client.PostAsync($"/api/v1/admin/oidc-clients/{clientId}/regenerate-secret", null, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, regen.StatusCode);
            var rotated = (await regen.Content.ReadFromJsonAsync<RegenerateSecretResponse>(TestContext.Current.CancellationToken))!;
            Assert.NotEqual(created.ClientSecret, rotated.ClientSecret);

            using var scope = fixture.CreateScope();
            var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
            var application = await manager.FindByClientIdAsync(clientId, TestContext.Current.CancellationToken);
            Assert.False(await manager.ValidateClientSecretAsync(application!, created.ClientSecret!, TestContext.Current.CancellationToken));
            Assert.True(await manager.ValidateClientSecretAsync(application!, rotated.ClientSecret, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task PublicClient_HasNoSecret_AndRequiresPkce()
        {
            var (client, _) = await CreateAdminClientAsync();
            var clientId = $"pub-client-{Guid.NewGuid():N}"[..20];

            var create = await client.PostAsJsonAsync("/api/v1/admin/oidc-clients", new CreateOidcClientRequest
            {
                ClientId = clientId,
                DisplayName = "Public UI",
                Type = "public",
                RedirectUris = ["https://pub.example.com/cb"],
            }, TestContext.Current.CancellationToken);
            var created = (await create.Content.ReadFromJsonAsync<CreateOidcClientResponse>(TestContext.Current.CancellationToken))!;
            Assert.Null(created.ClientSecret);
            Assert.Equal("public", created.Client.Type);

            using var scope = fixture.CreateScope();
            var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
            var application = await manager.FindByClientIdAsync(clientId, TestContext.Current.CancellationToken);
            var requirements = await manager.GetRequirementsAsync(application!, TestContext.Current.CancellationToken);
            Assert.Contains(OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange, requirements);

            // Regenerating a public client's secret is refused.
            var regen = await client.PostAsync($"/api/v1/admin/oidc-clients/{clientId}/regenerate-secret", null, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, regen.StatusCode);
        }

        [Fact]
        public async Task Create_DuplicateClientId_Returns409()
        {
            var (client, _) = await CreateAdminClientAsync();
            var clientId = $"dup-client-{Guid.NewGuid():N}"[..20];

            var body = new CreateOidcClientRequest
            {
                ClientId = clientId,
                DisplayName = "Dup",
                RedirectUris = ["https://dup.example.com/cb"],
            };
            (await client.PostAsJsonAsync("/api/v1/admin/oidc-clients", body, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();

            var duplicate = await client.PostAsJsonAsync("/api/v1/admin/oidc-clients", body, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        }

        [Fact]
        public async Task Delete_UnknownClient_Returns404()
        {
            // Actual deletion uses ExecuteDelete, which the InMemory provider can't
            // translate — the happy path is covered against real Postgres in
            // OidcClientAdminPostgresTests.
            var (client, _) = await CreateAdminClientAsync();

            var response = await client.DeleteAsync($"/api/v1/admin/oidc-clients/no-such-{Guid.NewGuid():N}", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
