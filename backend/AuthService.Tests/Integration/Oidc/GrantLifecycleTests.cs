using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AuthService.Data;
using AuthService.DTOs.Auth;
using AuthService.Services;
using AuthService.Tests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AuthService.Tests.Integration.Oidc
{
    /// <summary>
    /// OIDC grants live and die with the account, not with a session (ADR-020
    /// mental model, decided 2026-07-09): logout must not end downstream logins,
    /// but a soft-deleted or merged-away user must not keep refreshing tokens.
    /// </summary>
    [Collection("Api Tests")]
    public class GrantLifecycleTests(ApiTestFixture fixture)
    {
        private const string RedirectUri = "https://client.example.com/api/auth/sso_callback";

        // offline_access so the token response carries a refresh token.
        private const string AuthorizeUrl =
            "/connect/authorize?client_id=test-client"
            + "&redirect_uri=https%3A%2F%2Fclient.example.com%2Fapi%2Fauth%2Fsso_callback"
            + "&response_type=code&scope=openid%20profile%20offline_access&state=s";

        private HttpClient CreateClient() =>
            fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        private static async Task<AuthResponse> RegisterUserAsync(HttpClient client)
        {
            var response = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest
            {
                Username = $"u{Guid.NewGuid():N}"[..15],
                DisplayName = "GrantLifecycleUser",
                Email = $"grant-{Guid.NewGuid():N}@example.com",
                Password = "SecurePass123",
            }, TestContext.Current.CancellationToken);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<AuthResponse>(TestContext.Current.CancellationToken))!;
        }

        /// <summary>Complete the code flow on this client's cookie and return the refresh token.</summary>
        private static async Task<string> AcquireRefreshTokenAsync(HttpClient client)
        {
            var authorizeResponse = await client.GetAsync(AuthorizeUrl, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Found, authorizeResponse.StatusCode);
            var code = QueryHelpers.ParseQuery(authorizeResponse.Headers.Location!.Query)["code"].ToString();
            Assert.NotEmpty(code);

            var tokenResponse = await client.PostAsync("/connect/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "authorization_code",
                    ["code"] = code,
                    ["redirect_uri"] = RedirectUri,
                    ["client_id"] = "test-client",
                    ["client_secret"] = "test-secret",
                }),
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, tokenResponse.StatusCode);

            var tokens = JsonDocument.Parse(
                await tokenResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).RootElement;
            return tokens.GetProperty("refresh_token").GetString()!;
        }

        private static Task<HttpResponseMessage> RefreshAsync(HttpClient client, string refreshToken) =>
            client.PostAsync("/connect/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = refreshToken,
                    ["client_id"] = "test-client",
                    ["client_secret"] = "test-secret",
                }),
                TestContext.Current.CancellationToken);

        [Fact]
        public async Task Refresh_AfterSessionRevoked_StillSucceeds()
        {
            using var client = CreateClient();
            var auth = await RegisterUserAsync(client);
            var refreshToken = await AcquireRefreshTokenAsync(client);

            // Logout elsewhere: revoke the session that minted the grant.
            using (var scope = fixture.CreateScope())
            {
                var jwtService = scope.ServiceProvider.GetRequiredService<IJwtService>();
                var sessionService = scope.ServiceProvider.GetRequiredService<ISessionService>();
                var sessionId = jwtService.GetSessionIdFromToken(auth.AccessToken)!.Value;
                await sessionService.RevokeSessionAsync(sessionId);
            }

            // The grant outlives the session: downstream logins survive logout.
            var response = await RefreshAsync(client, refreshToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var tokens = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).RootElement;
            Assert.NotEmpty(tokens.GetProperty("access_token").GetString()!);
        }

        [Fact]
        public async Task Refresh_AfterUserSoftDeleted_ReturnsInvalidGrant()
        {
            using var client = CreateClient();
            var auth = await RegisterUserAsync(client);
            var refreshToken = await AcquireRefreshTokenAsync(client);

            using (var scope = fixture.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var user = await db.Users.SingleAsync(u => u.Id == auth.UserId, TestContext.Current.CancellationToken);
                user.IsDeleted = true;
                await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var response = await RefreshAsync(client, refreshToken);

            Assert.False(response.IsSuccessStatusCode);
            var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.Contains("invalid_grant", body);
        }

        [Fact]
        public async Task Merge_RevokesSourceGrants_TargetUnaffected()
        {
            using var sourceClient = CreateClient();
            using var targetClient = CreateClient();
            var sourceAuth = await RegisterUserAsync(sourceClient);
            var targetAuth = await RegisterUserAsync(targetClient);
            var sourceRefreshToken = await AcquireRefreshTokenAsync(sourceClient);
            var targetRefreshToken = await AcquireRefreshTokenAsync(targetClient);

            using (var scope = fixture.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var accountService = scope.ServiceProvider.GetRequiredService<IAccountService>();
                await accountService.MergeAsync(sourceAuth.UserId, targetAuth.UserId);
                await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var sourceResponse = await RefreshAsync(sourceClient, sourceRefreshToken);
            Assert.False(sourceResponse.IsSuccessStatusCode);
            Assert.Contains("invalid_grant",
                await sourceResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

            var targetResponse = await RefreshAsync(targetClient, targetRefreshToken);
            Assert.Equal(HttpStatusCode.OK, targetResponse.StatusCode);
        }
    }
}
