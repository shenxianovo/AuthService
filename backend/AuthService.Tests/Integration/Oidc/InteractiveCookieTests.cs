using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AuthService.DTOs.Auth;
using AuthService.Tests.Fixtures;

namespace AuthService.Tests.Integration.Oidc
{
    [Collection("Api Tests")]
    public class InteractiveCookieTests(ApiTestFixture fixture)
    {
        private readonly HttpClient _client = fixture.Client;

        private async Task<(AuthResponse auth, string email)> RegisterUserAsync()
        {
            var email = $"cookie-{Guid.NewGuid():N}@example.com";
            var request = new RegisterRequest
            {
                Username = $"u{Guid.NewGuid():N}"[..15],
                DisplayName = "CookieTestUser",
                Email = email,
                Password = "SecurePass123",
            };
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request, TestContext.Current.CancellationToken);
            response.EnsureSuccessStatusCode();
            var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(TestContext.Current.CancellationToken))!;
            return (auth, email);
        }

        [Fact]
        public async Task Register_SetsInteractiveCookie_ScopedToConnect()
        {
            var request = new RegisterRequest
            {
                Username = $"u{Guid.NewGuid():N}"[..15],
                DisplayName = "CookieTestUser",
                Email = $"cookie-{Guid.NewGuid():N}@example.com",
                Password = "SecurePass123",
            };
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request, TestContext.Current.CancellationToken);
            response.EnsureSuccessStatusCode();

            var setCookie = Assert.Single(response.Headers.GetValues("Set-Cookie"),
                v => v.StartsWith("authservice.sso="));
            Assert.Contains("path=/connect", setCookie, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("samesite=lax", setCookie, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Login_SetsInteractiveCookie()
        {
            var (_, email) = await RegisterUserAsync();

            var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new LoginRequest { Email = email, Password = "SecurePass123" },
                TestContext.Current.CancellationToken);
            response.EnsureSuccessStatusCode();

            Assert.Contains(response.Headers.GetValues("Set-Cookie"),
                v => v.StartsWith("authservice.sso="));
        }

        [Fact]
        public async Task Logout_ClearsInteractiveCookie()
        {
            var (auth, _) = await RegisterUserAsync();

            var logout = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
            logout.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
            var response = await _client.SendAsync(logout, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

            // Sign-out emits an expired cookie so the browser drops its copy.
            var setCookie = Assert.Single(response.Headers.GetValues("Set-Cookie"),
                v => v.StartsWith("authservice.sso="));
            Assert.Contains("expires=", setCookie, StringComparison.OrdinalIgnoreCase);
        }
    }
}
