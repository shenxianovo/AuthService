using System.Net;
using System.Net.Http.Json;
using AuthService.DTOs.Auth;
using AuthService.Tests.Fixtures;

namespace AuthService.Tests.Integration.Controllers
{
    [Collection("Api Tests")]
    public class AuthController_LoginTests(ApiTestFixture fixture)
    {
        private readonly HttpClient _client = fixture.Client;

        private async Task<AuthResponse> RegisterUserAsync(string? email = null, string password = "SecurePass123")
        {
            email ??= $"login-{Guid.NewGuid():N}@example.com";
            var request = new RegisterRequest
            {
                DisplayName = "LoginTestUser",
                Email = email,
                Password = password,
            };
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        }

        [Fact]
        public async Task Login_WithValidCredentials_Returns200WithTokens()
        {
            var email = $"login-{Guid.NewGuid():N}@example.com";
            await RegisterUserAsync(email);

            var loginRequest = new LoginRequest
            {
                Email = email,
                Password = "SecurePass123",
            };

            var response = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
            Assert.NotNull(body);
            Assert.NotEqual(Guid.Empty, body.UserId);
            Assert.NotEmpty(body.AccessToken);
            Assert.NotEmpty(body.RefreshToken);
        }

        [Fact]
        public async Task Login_WithWrongPassword_Returns401()
        {
            var email = $"login-{Guid.NewGuid():N}@example.com";
            await RegisterUserAsync(email);

            var loginRequest = new LoginRequest
            {
                Email = email,
                Password = "WrongPassword",
            };

            var response = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Login_WithNonExistentEmail_Returns401()
        {
            var loginRequest = new LoginRequest
            {
                Email = $"nobody-{Guid.NewGuid():N}@example.com",
                Password = "Whatever123",
            };

            var response = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Login_CaseInsensitiveEmail_Returns200()
        {
            var email = $"case-{Guid.NewGuid():N}@example.com";
            await RegisterUserAsync(email);

            var loginRequest = new LoginRequest
            {
                Email = email.ToUpperInvariant(),
                Password = "SecurePass123",
            };

            var response = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
