using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AuthService.Configuration;
using AuthService.Data;
using AuthService.DTOs.Auth;
using AuthService.Entities;
using AuthService.Services;
using AuthService.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AuthService.Tests.Integration.Admin
{
    [Collection("Api Tests")]
    public class AdminRoleTests(ApiTestFixture fixture)
    {
        private readonly HttpClient _client = fixture.Client;

        private async Task<AuthResponse> RegisterUserAsync(string? username = null)
        {
            var request = new RegisterRequest
            {
                Username = username ?? $"u{Guid.NewGuid():N}"[..15],
                DisplayName = "AdminTestUser",
                Email = $"admin-{Guid.NewGuid():N}@example.com",
                Password = "SecurePass123",
            };
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request, TestContext.Current.CancellationToken);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<AuthResponse>(TestContext.Current.CancellationToken))!;
        }

        private async Task PromoteViaDbAsync(Guid userId)
        {
            using var scope = fixture.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(u => u.Id == userId, TestContext.Current.CancellationToken);
            user.Role = UserRole.Admin;
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        private async Task<HttpResponseMessage> SetRoleAsync(string callerToken, Guid targetUserId, string role)
        {
            var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/admin/users/{targetUserId}/role")
            {
                Content = JsonContent.Create(new { role }),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", callerToken);
            return await _client.SendAsync(request, TestContext.Current.CancellationToken);
        }

        [Fact]
        public async Task NonAdmin_Gets403OnAdminEndpoint()
        {
            var caller = await RegisterUserAsync();
            var target = await RegisterUserAsync();

            var response = await SetRoleAsync(caller.AccessToken, target.UserId, "Admin");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task Promotion_TakesEffectWithoutReLogin()
        {
            var admin = await RegisterUserAsync();
            await PromoteViaDbAsync(admin.UserId);
            var target = await RegisterUserAsync();

            // Promote target; target's pre-existing token immediately gains admin
            // access because authorization checks the database, not the token.
            var promote = await SetRoleAsync(admin.AccessToken, target.UserId, "Admin");
            Assert.Equal(HttpStatusCode.NoContent, promote.StatusCode);

            var probe = await SetRoleAsync(target.AccessToken, admin.UserId, "Admin");
            Assert.Equal(HttpStatusCode.NoContent, probe.StatusCode);

            // Demote target again; the same token loses access instantly.
            var demote = await SetRoleAsync(admin.AccessToken, target.UserId, "User");
            Assert.Equal(HttpStatusCode.NoContent, demote.StatusCode);

            var probeAfterDemote = await SetRoleAsync(target.AccessToken, admin.UserId, "Admin");
            Assert.Equal(HttpStatusCode.Forbidden, probeAfterDemote.StatusCode);
        }

        [Fact]
        public async Task DemotingTheLastAdmin_IsRefused()
        {
            var admin = await RegisterUserAsync();
            await PromoteViaDbAsync(admin.UserId);

            // This test may run alongside others that created admins; demote via a
            // dedicated pair to keep the invariant local: create one admin, have it
            // demote itself while it is (locally) the only one we control. To make
            // the check deterministic, strip all other admins inside a scope first.
            using (var scope = fixture.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var others = await db.Users
                    .Where(u => u.Role == UserRole.Admin && u.Id != admin.UserId)
                    .ToListAsync(TestContext.Current.CancellationToken);
                foreach (var other in others) other.Role = UserRole.User;
                await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var response = await SetRoleAsync(admin.AccessToken, admin.UserId, "User");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.Contains("last admin", body);
        }

        [Fact]
        public async Task SessionJwt_CarriesNoRoleClaim()
        {
            var admin = await RegisterUserAsync();
            await PromoteViaDbAsync(admin.UserId);

            // Re-login so the token is minted while the user IS an admin.
            using var scope = fixture.CreateScope();
            var jwtService = scope.ServiceProvider.GetRequiredService<IJwtService>();
            var token = jwtService.GenerateAccessToken(admin.UserId);

            foreach (var jwt in new[] { admin.AccessToken, token })
            {
                var payload = JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(jwt.Split('.')[1])).RootElement;
                Assert.False(payload.TryGetProperty("role", out _));
                Assert.False(payload.TryGetProperty("roles", out _));
            }
        }

        [Fact]
        public async Task Bootstrapper_PromotesConfiguredUser_Idempotently()
        {
            var username = $"boot{Guid.NewGuid():N}"[..15];
            var auth = await RegisterUserAsync(username);

            var options = Microsoft.Extensions.Options.Options.Create(
                new AdminOptions { BootstrapUsername = username });
            var bootstrapper = new AdminBootstrapper(
                fixture.Factory.Services, options, NullLogger<AdminBootstrapper>.Instance);

            await bootstrapper.StartAsync(TestContext.Current.CancellationToken);
            await bootstrapper.StartAsync(TestContext.Current.CancellationToken); // idempotent

            using var scope = fixture.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(u => u.Id == auth.UserId, TestContext.Current.CancellationToken);
            Assert.Equal(UserRole.Admin, user.Role);
        }
    }
}
