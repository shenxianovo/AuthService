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
    public class UserController_UnlinkProviderTests(ApiTestFixture fixture)
    {
        private readonly HttpClient _client = fixture.Client;
        private readonly ApiTestFixture _fixture = fixture;

        /// <summary>
        /// Create a user with password + GitHub provider, return (userId, accessToken).
        /// </summary>
        private async Task<(Guid UserId, string AccessToken)> CreateUserWithPasswordAndGithubAsync()
        {
            var email = $"unlink-{Guid.NewGuid():N}@example.com";
            var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest
            {
                DisplayName = "UnlinkTestUser",
                Email = email,
                Password = "SecurePass123",
            }, TestContext.Current.CancellationToken);
            var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>(TestContext.Current.CancellationToken);

            // Add a GitHub provider directly in DB
            using var scope = _fixture.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AuthProviders.Add(new AuthProvider
            {
                UserId = auth!.UserId,
                Provider = AuthProviderType.Github,
                ProviderUserId = $"github-{Guid.NewGuid():N}",
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);

            return (auth.UserId, auth.AccessToken);
        }

        [Fact]
        public async Task UnlinkProvider_WithValidProvider_Returns200()
        {
            var (_, accessToken) = await CreateUserWithPasswordAndGithubAsync();

            var request = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/auth/unlink-provider")
            {
                Content = JsonContent.Create(new UnlinkProviderRequest { Provider = "Github" }),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task UnlinkProvider_LastLoginMethod_Returns400()
        {
            // Create a user with only GitHub (no password)
            Guid userId;
            using (var scope = _fixture.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var user = new User { DisplayName = "OnlyGithub" };
                db.Users.Add(user);
                db.AuthProviders.Add(new AuthProvider
                {
                    UserId = user.Id,
                    Provider = AuthProviderType.Github,
                    ProviderUserId = $"github-only-{Guid.NewGuid():N}",
                });
                await db.SaveChangesAsync(TestContext.Current.CancellationToken);
                userId = user.Id;
            }

            var token = _fixture.GenerateAccessToken(userId);

            var request = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/auth/unlink-provider")
            {
                Content = JsonContent.Create(new UnlinkProviderRequest { Provider = "Github" }),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task UnlinkProvider_NotLinkedProvider_Returns400()
        {
            var (_, accessToken) = await CreateUserWithPasswordAndGithubAsync();

            // Try to unlink Google which is not linked
            var request = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/auth/unlink-provider")
            {
                Content = JsonContent.Create(new UnlinkProviderRequest { Provider = "Google" }),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task UnlinkProvider_PasswordProvider_Returns400()
        {
            var (_, accessToken) = await CreateUserWithPasswordAndGithubAsync();

            // Try to unlink "Password" → should be rejected by controller validation
            var request = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/auth/unlink-provider")
            {
                Content = JsonContent.Create(new UnlinkProviderRequest { Provider = "Password" }),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task UnlinkProvider_InvalidProviderName_Returns400()
        {
            var (_, accessToken) = await CreateUserWithPasswordAndGithubAsync();

            var request = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/auth/unlink-provider")
            {
                Content = JsonContent.Create(new UnlinkProviderRequest { Provider = "NotARealProvider" }),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task UnlinkProvider_WithoutToken_Returns401()
        {
            var request = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/auth/unlink-provider")
            {
                Content = JsonContent.Create(new UnlinkProviderRequest { Provider = "Github" }),
            };

            var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}
