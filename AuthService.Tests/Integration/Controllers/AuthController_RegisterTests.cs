using System.Net;
using System.Net.Http.Json;
using AuthService.DTOs.Auth;
using AuthService.Tests.Fixtures;

namespace AuthService.Tests.Integration.Controllers
{
    [Collection("Api Tests")]
    public class AuthController_RegisterTests(ApiTestFixture fixture)
    {
        private readonly HttpClient _client = fixture.Client;

        [Fact]
        public async Task Register_WithValidData_Returns200WithTokens()
        {
            var request = new RegisterRequest
            {
                DisplayName = $"IntTest-{Guid.NewGuid():N}",
                Email = $"reg-{Guid.NewGuid():N}@example.com",
                Password = "SecurePass123",
            };

            var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
            Assert.NotNull(body);
            Assert.NotEqual(Guid.Empty, body.UserId);
            Assert.NotEmpty(body.AccessToken);
            Assert.NotEmpty(body.RefreshToken);
            Assert.True(body.ExpiresAt > DateTimeOffset.UtcNow);
        }

        [Fact]
        public async Task Register_WithDuplicateEmail_Returns409()
        {
            var email = $"dup-{Guid.NewGuid():N}@example.com";

            var request = new RegisterRequest
            {
                DisplayName = "First",
                Email = email,
                Password = "SecurePass123",
            };

            var firstResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", request);
            Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

            var request2 = new RegisterRequest
            {
                DisplayName = "Second",
                Email = email,
                Password = "SecurePass456",
            };

            var secondResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", request2);
            Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
        }

        [Fact]
        public async Task Register_WithMissingFields_Returns400()
        {
            var request = new { };

            var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Register_WithInvalidEmail_Returns400()
        {
            var request = new RegisterRequest
            {
                DisplayName = "Test",
                Email = "not-an-email",
                Password = "SecurePass123",
            };

            var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Register_WithTooShortPassword_Returns400()
        {
            var request = new RegisterRequest
            {
                DisplayName = "Test",
                Email = $"short-{Guid.NewGuid():N}@example.com",
                Password = "123", // less than 8 chars
            };

            var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
