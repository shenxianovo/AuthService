using System.Net;
using System.Net.Http.Json;
using AuthService.DTOs.Auth;
using AuthService.DTOs.User;
using AuthService.Tests.Fixtures;

namespace AuthService.Tests.Integration.Controllers
{
    [Collection("Api Tests")]
    public class PublicUserControllerTests(ApiTestFixture fixture)
    {
        private readonly HttpClient _client = fixture.Client;

        private async Task<AuthResponse> RegisterUserAsync(string username)
        {
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest
            {
                Username = username,
                DisplayName = $"Display-{username}",
                Email = $"{username}-{Guid.NewGuid():N}@example.com",
                Password = "SecurePass123",
            }, TestContext.Current.CancellationToken);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<AuthResponse>(TestContext.Current.CancellationToken))!;
        }

        [Fact]
        public async Task GetByUsername_ExistingUser_Returns200WithPublicInfo()
        {
            var username = $"u{Guid.NewGuid():N}"[..15];
            var auth = await RegisterUserAsync(username);

            var response = await _client.GetAsync($"/api/v1/users/{username}", TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<PublicUserResponse>(TestContext.Current.CancellationToken);
            Assert.NotNull(body);
            Assert.Equal(auth.UserId, body.Id);
            Assert.Equal(username, body.Username);
            Assert.Equal($"Display-{username}", body.DisplayName);
        }

        [Fact]
        public async Task GetByUsername_IsCaseInsensitive()
        {
            var username = $"u{Guid.NewGuid():N}"[..15];
            await RegisterUserAsync(username);

            var response = await _client.GetAsync($"/api/v1/users/{username.ToUpperInvariant()}", TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task GetByUsername_NonExistent_Returns404()
        {
            var response = await _client.GetAsync($"/api/v1/users/no-such-user-{Guid.NewGuid():N}"[..38], TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task GetByUsername_RequiresNoAuth()
        {
            var username = $"u{Guid.NewGuid():N}"[..15];
            await RegisterUserAsync(username);

            // No Authorization header attached
            var response = await _client.GetAsync($"/api/v1/users/{username}", TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}