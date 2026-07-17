using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AuthService.Data;
using AuthService.DTOs.Auth;
using AuthService.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AuthService.Tests.Integration.Controllers
{
    [Collection("Api Tests")]
    public class UserController_ChangeUsernameTests(ApiTestFixture fixture)
    {
        private readonly HttpClient _client = fixture.Client;
        private readonly ApiTestFixture _fixture = fixture;

        private async Task<AuthResponse> RegisterAsync(string username)
        {
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest
            {
                Username = username,
                DisplayName = "RenameTestUser",
                Email = $"{username}-{Guid.NewGuid():N}@example.com",
                Password = "SecurePass123",
            }, TestContext.Current.CancellationToken);
            return (await response.Content.ReadFromJsonAsync<AuthResponse>(TestContext.Current.CancellationToken))!;
        }

        private HttpRequestMessage Patch(string newUsername, string accessToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Patch, "/api/v1/auth/username")
            {
                Content = JsonContent.Create(new ChangeUsernameRequest { Username = newUsername }),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            return request;
        }

        [Fact]
        public async Task ChangeUsername_ToAvailableName_Returns200_UpdatesRow_RevokesSessions()
        {
            var auth = await RegisterAsync($"u{Guid.NewGuid():N}"[..15]);
            var newName = $"u{Guid.NewGuid():N}"[..15];

            var response = await _client.SendAsync(Patch(newName, auth.AccessToken), TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var scope = _fixture.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FindAsync([auth.UserId], TestContext.Current.CancellationToken);
            Assert.Equal(newName, user!.Username);
            Assert.All(
                await db.Sessions.Where(s => s.UserId == auth.UserId).ToListAsync(TestContext.Current.CancellationToken),
                s => Assert.True(s.Revoked));
        }

        [Fact]
        public async Task ChangeUsername_ToTakenName_Returns409()
        {
            var takenName = $"u{Guid.NewGuid():N}"[..15];
            await RegisterAsync(takenName);
            var auth = await RegisterAsync($"u{Guid.NewGuid():N}"[..15]);

            var response = await _client.SendAsync(Patch(takenName, auth.AccessToken), TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        [Fact]
        public async Task ChangeUsername_ToReservedName_Returns400()
        {
            var auth = await RegisterAsync($"u{Guid.NewGuid():N}"[..15]);

            var response = await _client.SendAsync(Patch("admin", auth.AccessToken), TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task ChangeUsername_ReleasesOldName_ForImmediateReRegistration()
        {
            // GitHub model: the old name frees up the moment the rename commits.
            var oldName = $"u{Guid.NewGuid():N}"[..15];
            var auth = await RegisterAsync(oldName);
            await _client.SendAsync(
                Patch($"u{Guid.NewGuid():N}"[..15], auth.AccessToken), TestContext.Current.CancellationToken);

            var reRegister = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest
            {
                Username = oldName,
                DisplayName = "NewOwner",
                Email = $"reclaim-{Guid.NewGuid():N}@example.com",
                Password = "SecurePass123",
            }, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, reRegister.StatusCode);
        }

        [Fact]
        public async Task ChangeUsername_WithoutToken_Returns401()
        {
            var request = new HttpRequestMessage(HttpMethod.Patch, "/api/v1/auth/username")
            {
                Content = JsonContent.Create(new ChangeUsernameRequest { Username = "whoever" }),
            };

            var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}
