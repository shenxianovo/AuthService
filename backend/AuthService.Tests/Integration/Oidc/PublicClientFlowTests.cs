using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AuthService.DTOs.Auth;
using AuthService.Tests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.Tokens;

namespace AuthService.Tests.Integration.Oidc
{
    [Collection("Api Tests")]
    public class PublicClientFlowTests(ApiTestFixture fixture)
    {
        private const string RedirectUri = "https://spa.example.com/callback";

        private HttpClient CreateClient() =>
            fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        private static async Task RegisterUserAsync(HttpClient client)
        {
            var request = new RegisterRequest
            {
                Username = $"u{Guid.NewGuid():N}"[..15],
                DisplayName = "SpaFlowUser",
                Email = $"spa-{Guid.NewGuid():N}@example.com",
                Password = "SecurePass123",
            };
            var response = await client.PostAsJsonAsync("/api/v1/auth/register", request, TestContext.Current.CancellationToken);
            response.EnsureSuccessStatusCode();
        }

        private static (string verifier, string challenge) CreatePkcePair()
        {
            var verifier = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
            var challenge = Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
            return (verifier, challenge);
        }

        private static string AuthorizeUrl(string? challenge) =>
            "/connect/authorize?client_id=test-spa"
            + "&redirect_uri=" + Uri.EscapeDataString(RedirectUri)
            + "&response_type=code&scope=openid%20profile&state=spastate"
            + (challenge is null ? "" : $"&code_challenge={challenge}&code_challenge_method=S256");

        [Fact]
        public async Task PublicClient_CompletesCodeFlow_WithPkceAndNoSecret()
        {
            using var client = CreateClient();
            await RegisterUserAsync(client);
            var (verifier, challenge) = CreatePkcePair();

            var authorizeResponse = await client.GetAsync(AuthorizeUrl(challenge), TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Found, authorizeResponse.StatusCode);
            Assert.StartsWith(RedirectUri, authorizeResponse.Headers.Location!.OriginalString);
            var code = QueryHelpers.ParseQuery(authorizeResponse.Headers.Location!.Query)["code"].ToString();

            var tokenResponse = await client.PostAsync("/connect/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "authorization_code",
                    ["code"] = code,
                    ["redirect_uri"] = RedirectUri,
                    ["client_id"] = "test-spa",
                    ["code_verifier"] = verifier,
                }),
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, tokenResponse.StatusCode);

            var tokens = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).RootElement;
            Assert.NotEmpty(tokens.GetProperty("access_token").GetString()!);
            Assert.NotEmpty(tokens.GetProperty("id_token").GetString()!);
        }

        [Fact]
        public async Task PublicClient_WithoutPkce_IsRejectedAtAuthorize()
        {
            using var client = CreateClient();
            await RegisterUserAsync(client);

            var response = await client.GetAsync(AuthorizeUrl(challenge: null), TestContext.Current.CancellationToken);

            // The per-client PKCE requirement rejects the request before a code is issued.
            var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            var location = response.Headers.Location?.OriginalString ?? "";
            Assert.True(
                location.Contains("error=invalid_request") || body.Contains("invalid_request"),
                $"Expected invalid_request, got {(int)response.StatusCode} {location} {body[..Math.Min(body.Length, 200)]}");
        }

        [Fact]
        public async Task Preflight_FromRegisteredClientOrigin_IsAllowed()
        {
            using var client = CreateClient();

            var request = new HttpRequestMessage(HttpMethod.Options, "/connect/token");
            request.Headers.Add("Origin", "https://spa.example.com");
            request.Headers.Add("Access-Control-Request-Method", "POST");
            var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

            Assert.Equal("https://spa.example.com",
                Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        }

        [Fact]
        public async Task Preflight_FromUnregisteredOrigin_GetsNoCorsHeaders()
        {
            using var client = CreateClient();

            var request = new HttpRequestMessage(HttpMethod.Options, "/connect/token");
            request.Headers.Add("Origin", "https://evil.example.net");
            request.Headers.Add("Access-Control-Request-Method", "POST");
            var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

            Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        }

        [Fact]
        public async Task NonOidcEndpoints_GetNoCorsHeaders()
        {
            using var client = CreateClient();

            var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/login");
            request.Headers.Add("Origin", "https://spa.example.com");
            request.Headers.Add("Access-Control-Request-Method", "POST");
            var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

            Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        }
    }
}
