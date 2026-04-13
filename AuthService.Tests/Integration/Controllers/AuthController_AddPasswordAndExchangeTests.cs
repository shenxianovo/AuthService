using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AuthService.Data;
using AuthService.DTOs.Auth;
using AuthService.Entities;
using AuthService.Services;
using AuthService.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace AuthService.Tests.Integration.Controllers
{
    [Collection("Api Tests")]
    public class AuthController_AddPasswordTests(ApiTestFixture fixture)
    {
        private readonly HttpClient _client = fixture.Client;
        private readonly ApiTestFixture _fixture = fixture;

        [Fact]
        public async Task AddPassword_WithValidToken_OAuthUser_Returns200()
        {
            // Create an OAuth-only user directly in DB
            Guid userId;
            using (var scope = _fixture.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var user = new User { DisplayName = "OAuthOnly" };
                db.Users.Add(user);
                db.AuthProviders.Add(new AuthProvider
                {
                    UserId = user.Id,
                    Provider = AuthProviderType.Github,
                    ProviderUserId = $"github-addpwd-{Guid.NewGuid():N}"
                });
                await db.SaveChangesAsync();
                userId = user.Id;
            }

            var token = _fixture.GenerateAccessToken(userId);

            var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/add-password");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = JsonContent.Create(new AddPasswordRequest { Password = "NewPassword123" });

            var response = await _client.SendAsync(request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task AddPassword_WithoutToken_Returns401()
        {
            var response = await _client.PostAsJsonAsync("/api/v1/auth/add-password",
                new AddPasswordRequest { Password = "NewPassword123" });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task AddPassword_UserAlreadyHasPassword_Returns400()
        {
            // Register a user (who already has a password)
            var email = $"addpwd-dup-{Guid.NewGuid():N}@example.com";
            var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest
            {
                DisplayName = "HasPassword",
                Email = email,
                Password = "ExistingPass123",
            });
            var authResponse = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();

            var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/add-password");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authResponse!.AccessToken);
            request.Content = JsonContent.Create(new AddPasswordRequest { Password = "AnotherPass123" });

            var response = await _client.SendAsync(request);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Collection("Api Tests")]
    public class AuthController_ExchangeTests(ApiTestFixture fixture)
    {
        private readonly HttpClient _client = fixture.Client;
        private readonly ApiTestFixture _fixture = fixture;

        [Fact]
        public async Task Exchange_WithValidCode_Returns200WithTokens()
        {
            // Generate an auth code via the OAuthSecurityService
            string authCode;
            using (var scope = _fixture.CreateScope())
            {
                var oauthSecurity = scope.ServiceProvider.GetRequiredService<IOAuthSecurityService>();
                authCode = oauthSecurity.GenerateAuthCode(
                    Guid.NewGuid(), "test-access-token", "test-refresh-token", DateTimeOffset.UtcNow.AddMinutes(15));
            }

            var response = await _client.PostAsJsonAsync("/api/v1/auth/exchange",
                new ExchangeCodeRequest { Code = authCode });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
            Assert.NotNull(body);
            Assert.Equal("test-access-token", body.AccessToken);
            Assert.Equal("test-refresh-token", body.RefreshToken);
        }

        [Fact]
        public async Task Exchange_WithInvalidCode_Returns400()
        {
            var response = await _client.PostAsJsonAsync("/api/v1/auth/exchange",
                new ExchangeCodeRequest { Code = "invalid-code" });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Exchange_CodeIsOneTimeUse_SecondCallReturns400()
        {
            string authCode;
            using (var scope = _fixture.CreateScope())
            {
                var oauthSecurity = scope.ServiceProvider.GetRequiredService<IOAuthSecurityService>();
                authCode = oauthSecurity.GenerateAuthCode(
                    Guid.NewGuid(), "test-access-token", "test-refresh-token", DateTimeOffset.UtcNow.AddMinutes(15));
            }

            var first = await _client.PostAsJsonAsync("/api/v1/auth/exchange",
                new ExchangeCodeRequest { Code = authCode });
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);

            var second = await _client.PostAsJsonAsync("/api/v1/auth/exchange",
                new ExchangeCodeRequest { Code = authCode });
            Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        }
    }
}
