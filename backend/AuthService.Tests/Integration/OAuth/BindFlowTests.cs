using System.Net;
using System.Net.Http.Json;
using AuthService.DTOs.Auth;
using AuthService.Services;
using AuthService.Tests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace AuthService.Tests.Integration.OAuth
{
    /// <summary>
    /// Bind is an interactive flow (ADR-019): a top-level POST authenticated by
    /// the interactive cookie with the same DB liveness backstop as authorize.
    /// As with OAuthChallengeTests, the provider round-trip belongs to the
    /// OpenIddict client — these tests assert the outward-facing halves.
    /// </summary>
    [Collection("Api Tests")]
    public class BindFlowTests(ApiTestFixture fixture)
    {
        private const string RedirectUrl = "https://example.com/dashboard/providers";

        private HttpClient CreateClient() =>
            fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        private static async Task<AuthResponse> RegisterUserAsync(HttpClient client)
        {
            var response = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest
            {
                Username = $"u{Guid.NewGuid():N}"[..15],
                DisplayName = "BindFlowUser",
                Email = $"bind-{Guid.NewGuid():N}@example.com",
                Password = "SecurePass123",
            }, TestContext.Current.CancellationToken);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<AuthResponse>(TestContext.Current.CancellationToken))!;
        }

        private static Task<HttpResponseMessage> PostBindAsync(HttpClient client, string provider = "github") =>
            client.PostAsync($"/connect/bind/{provider}",
                new FormUrlEncodedContent(new Dictionary<string, string> { ["redirectUrl"] = RedirectUrl }),
                TestContext.Current.CancellationToken);

        [Fact]
        public async Task Bind_WithLiveCookieAndSession_ChallengesGithub()
        {
            using var client = CreateClient();
            await RegisterUserAsync(client); // issues the interactive cookie

            var response = await PostBindAsync(client);

            Assert.Equal(HttpStatusCode.Found, response.StatusCode);
            var location = response.Headers.Location!;
            Assert.Equal("github.com", location.Host);
            Assert.Equal("/login/oauth/authorize", location.AbsolutePath);
            var query = QueryHelpers.ParseQuery(location.Query);
            Assert.False(string.IsNullOrEmpty(query["state"].ToString()));
        }

        [Fact]
        public async Task Bind_WithoutCookie_RedirectsToLogin()
        {
            using var client = CreateClient();

            var response = await PostBindAsync(client);

            Assert.Equal(HttpStatusCode.Found, response.StatusCode);
            Assert.StartsWith("/login?returnUrl=", ToPathAndQuery(response.Headers.Location!));
        }

        [Fact]
        public async Task Bind_AfterSessionRevoked_RedirectsToLogin()
        {
            using var client = CreateClient();
            var auth = await RegisterUserAsync(client);

            using (var scope = fixture.CreateScope())
            {
                var jwtService = scope.ServiceProvider.GetRequiredService<IJwtService>();
                var sessionService = scope.ServiceProvider.GetRequiredService<ISessionService>();
                var sessionId = jwtService.GetSessionIdFromToken(auth.AccessToken)!.Value;
                await sessionService.RevokeSessionAsync(sessionId);
            }

            var response = await PostBindAsync(client);

            Assert.Equal(HttpStatusCode.Found, response.StatusCode);
            Assert.StartsWith("/login?returnUrl=", ToPathAndQuery(response.Headers.Location!));
        }

        [Fact]
        public async Task Bind_ViaGet_IsMethodNotAllowed()
        {
            using var client = CreateClient();
            await RegisterUserAsync(client);

            var response = await client.GetAsync(
                $"/connect/bind/github?redirectUrl={Uri.EscapeDataString(RedirectUrl)}",
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        }

        [Fact]
        public async Task Bind_WithDisallowedRedirectUrl_IsRejected()
        {
            using var client = CreateClient();
            await RegisterUserAsync(client);

            var response = await client.PostAsync("/connect/bind/github",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["redirectUrl"] = "https://evil.com/steal",
                }),
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Bind_UnknownProvider_IsNotFound()
        {
            using var client = CreateClient();
            await RegisterUserAsync(client);

            var response = await PostBindAsync(client, provider: "gitlab");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        private static string ToPathAndQuery(Uri location)
            => location.IsAbsoluteUri ? location.PathAndQuery : location.OriginalString;
    }
}
