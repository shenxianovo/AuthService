using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AuthService.DTOs.Auth;
using AuthService.Tests.Fixtures;

namespace AuthService.Tests.Integration.Controllers
{
    [Collection("Api Tests")]
    public class SessionController_RefreshAndLogoutTests(ApiTestFixture fixture)
    {
        private readonly HttpClient _client = fixture.Client;

        private async Task<AuthResponse> RegisterUserAsync()
        {
            var email = $"session-{Guid.NewGuid():N}@example.com";
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest
            {
                Username = $"u{Guid.NewGuid():N}"[..15],
                DisplayName = "SessionTestUser",
                Email = email,
                Password = "SecurePass123",
            }, TestContext.Current.CancellationToken);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<AuthResponse>(TestContext.Current.CancellationToken))!;
        }

        // ==================== Refresh ====================

        [Fact]
        public async Task Refresh_WithValidToken_Returns200AndNewTokens()
        {
            var auth = await RegisterUserAsync();

            var response = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest
            {
                RefreshToken = auth.RefreshToken
            }, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<AuthResponse>(TestContext.Current.CancellationToken);
            Assert.NotNull(body);
            Assert.NotEmpty(body.AccessToken);
            Assert.NotEmpty(body.RefreshToken);
            // Rotated: new refresh token must differ from old one
            Assert.NotEqual(auth.RefreshToken, body.RefreshToken);
            Assert.Equal(auth.UserId, body.UserId);
        }

        [Fact]
        public async Task Refresh_OldTokenCannotBeReused()
        {
            var auth = await RegisterUserAsync();

            // First refresh succeeds
            var first = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest
            {
                RefreshToken = auth.RefreshToken
            }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);

            // Replaying the old token must fail
            var second = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest
            {
                RefreshToken = auth.RefreshToken
            }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
        }

        [Fact]
        public async Task Refresh_WithInvalidToken_Returns401()
        {
            var response = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest
            {
                RefreshToken = Convert.ToBase64String(new byte[64])
            }, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        // ==================== Logout ====================

        [Fact]
        public async Task Logout_WithValidToken_Returns204()
        {
            var auth = await RegisterUserAsync();

            var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

            var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        [Fact]
        public async Task Logout_RevokesRefreshToken()
        {
            var auth = await RegisterUserAsync();

            // Logout
            var logoutReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
            logoutReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
            var logoutResp = await _client.SendAsync(logoutReq, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NoContent, logoutResp.StatusCode);

            // Refresh should now fail
            var refreshResp = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest
            {
                RefreshToken = auth.RefreshToken
            }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, refreshResp.StatusCode);
        }

        [Fact]
        public async Task Logout_WithoutToken_Returns401()
        {
            var response = await _client.PostAsync("/api/v1/auth/logout", null, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}
