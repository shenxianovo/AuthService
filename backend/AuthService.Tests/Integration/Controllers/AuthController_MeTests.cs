using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AuthService.Data;
using AuthService.DTOs.Auth;
using AuthService.Entities;
using AuthService.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace AuthService.Tests.Integration.Controllers
{
    [Collection("Api Tests")]
    public class AuthController_MeTests(ApiTestFixture fixture)
    {
        private readonly HttpClient _client = fixture.Client;
        private readonly ApiTestFixture _fixture = fixture;

        private async Task<AuthResponse> RegisterUserAsync(string? email = null)
        {
            email ??= $"me-{Guid.NewGuid():N}@example.com";
            var request = new RegisterRequest
            {
                Username = $"u{Guid.NewGuid():N}"[..15],
                DisplayName = "MeTestUser",
                Email = email,
                Password = "SecurePass123",
            };
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request, TestContext.Current.CancellationToken);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<AuthResponse>(TestContext.Current.CancellationToken))!;
        }

        [Fact]
        public async Task GetMe_WithValidToken_Returns200WithUserInfo()
        {
            var authResponse = await RegisterUserAsync();

            var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authResponse.AccessToken);

            var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<UserInfoResponse>(TestContext.Current.CancellationToken);
            Assert.NotNull(body);
            Assert.Equal(authResponse.UserId, body.UserId);
            Assert.Equal("MeTestUser", body.DisplayName);
            Assert.True(body.HasPassword);
            Assert.NotEmpty(body.Emails);
            Assert.Contains(body.Emails, e => e.IsPrimary && e.Email.Contains("@example.com"));
        }

        [Fact]
        public async Task GetMe_WithoutToken_Returns401()
        {
            var response = await _client.GetAsync("/api/v1/auth/me", TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetMe_WithInvalidToken_Returns401()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "invalid.jwt.token");

            var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetMe_WithDeletedUser_Returns404()
        {
            var authResponse = await RegisterUserAsync();

            // Soft-delete the user
            using (var scope = _fixture.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var user = await db.Users.FindAsync([authResponse.UserId], TestContext.Current.CancellationToken);
                user!.IsDeleted = true;
                await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authResponse.AccessToken);

            var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task GetMe_ShowsOAuthProviders()
        {
            var authResponse = await RegisterUserAsync();

            // Add a GitHub provider to the user
            using (var scope = _fixture.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.AuthProviders.Add(new AuthProvider
                {
                    UserId = authResponse.UserId,
                    Provider = AuthProviderType.Github,
                    ProviderUserId = "github-test-123"
                });
                await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authResponse.AccessToken);

            var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<UserInfoResponse>(TestContext.Current.CancellationToken);
            Assert.NotNull(body);
            // Password provider should NOT be in the list
            Assert.DoesNotContain(body.Providers, p => p.Provider == "Password");
            // GitHub should be in the list
            Assert.Contains(body.Providers, p => p.Provider == "Github");
        }
    }
}
