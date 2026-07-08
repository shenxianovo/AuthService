using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AuthService.DTOs.Auth;
using AuthService.Services;
using AuthService.Tests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace AuthService.Tests.Integration.Oidc
{
    [Collection("Api Tests")]
    public class AuthorizeFlowTests(ApiTestFixture fixture)
    {
        private const string RedirectUri = "https://client.example.com/api/auth/sso_callback";

        private const string AuthorizeUrl =
            "/connect/authorize?client_id=test-client"
            + "&redirect_uri=https%3A%2F%2Fclient.example.com%2Fapi%2Fauth%2Fsso_callback"
            + "&response_type=code&scope=openid%20profile%20email&state=teststate";

        /// <summary>
        /// Fresh client per test: the fixture's shared client accumulates cookies
        /// across tests, which would leak login state into these flow tests.
        /// </summary>
        private HttpClient CreateClient() =>
            fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        private static async Task<(AuthResponse auth, string email)> RegisterUserAsync(HttpClient client)
        {
            var email = $"oidc-{Guid.NewGuid():N}@example.com";
            var request = new RegisterRequest
            {
                Username = $"u{Guid.NewGuid():N}"[..15],
                DisplayName = "OidcFlowUser",
                Email = email,
                Password = "SecurePass123",
            };
            var response = await client.PostAsJsonAsync("/api/v1/auth/register", request, TestContext.Current.CancellationToken);
            response.EnsureSuccessStatusCode();
            var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(TestContext.Current.CancellationToken))!;
            return (auth, email);
        }

        [Fact]
        public async Task Authorize_WithoutCookie_RedirectsToLoginWithReturnUrl()
        {
            using var client = CreateClient();

            var response = await client.GetAsync(AuthorizeUrl, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.Found, response.StatusCode);
            var location = ToPathAndQuery(response.Headers.Location!);
            Assert.StartsWith("/login?returnUrl=", location);
            Assert.Contains("%2Fconnect%2Fauthorize", location);
            Assert.Contains("test-client", Uri.UnescapeDataString(location));
        }

        [Fact]
        public async Task FullAuthorizationCodeFlow_IssuesIdTokenAndUserinfo()
        {
            using var client = CreateClient();
            var (auth, email) = await RegisterUserAsync(client);

            // 1. Authorize: the interactive cookie from registration rides along.
            var authorizeResponse = await client.GetAsync(AuthorizeUrl, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Found, authorizeResponse.StatusCode);

            var location = authorizeResponse.Headers.Location!;
            Assert.StartsWith(RedirectUri, location.OriginalString);
            var query = QueryHelpers.ParseQuery(location.Query);
            Assert.Equal("teststate", query["state"]);
            var code = query["code"].ToString();
            Assert.NotEmpty(code);

            // 2. Token: exchange the code with client secret authentication.
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

            var tokens = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).RootElement;
            var accessToken = tokens.GetProperty("access_token").GetString()!;
            var idToken = tokens.GetProperty("id_token").GetString()!;

            // 3. The id_token carries the username under both keys OpenList understands.
            //    The email is absent: registration leaves it unverified, and only a
            //    verified primary email is asserted to downstream clients.
            var payload = JsonDocument.Parse(
                Base64UrlEncoder.DecodeBytes(idToken.Split('.')[1])).RootElement;
            Assert.Equal(auth.UserId.ToString(), payload.GetProperty("sub").GetString());
            Assert.Equal(auth.Username, payload.GetProperty("name").GetString());
            Assert.Equal(auth.Username, payload.GetProperty("preferred_username").GetString());
            Assert.False(payload.TryGetProperty("email", out _));
            Assert.False(payload.TryGetProperty("email_verified", out _));

            // 4. Userinfo with the OIDC access token.
            var userinfoRequest = new HttpRequestMessage(HttpMethod.Get, "/connect/userinfo");
            userinfoRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var userinfoResponse = await client.SendAsync(userinfoRequest, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, userinfoResponse.StatusCode);

            var userinfo = JsonDocument.Parse(await userinfoResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).RootElement;
            Assert.Equal(auth.UserId.ToString(), userinfo.GetProperty("sub").GetString());
            Assert.Equal(auth.Username, userinfo.GetProperty("name").GetString());
            Assert.False(userinfo.TryGetProperty("email", out _));
        }

        [Fact]
        public async Task AuthorizationCodeFlow_WithVerifiedEmail_EmitsEmailClaims()
        {
            using var client = CreateClient();
            var (auth, email) = await RegisterUserAsync(client);

            using (var scope = fixture.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AuthService.Data.AppDbContext>();
                var userEmail = await db.UserEmails.SingleAsync(
                    e => e.UserId == auth.UserId && e.IsPrimary, TestContext.Current.CancellationToken);
                userEmail.VerifiedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var authorizeResponse = await client.GetAsync(AuthorizeUrl, TestContext.Current.CancellationToken);
            var code = QueryHelpers.ParseQuery(authorizeResponse.Headers.Location!.Query)["code"].ToString();

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

            var tokens = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).RootElement;
            var payload = JsonDocument.Parse(
                Base64UrlEncoder.DecodeBytes(tokens.GetProperty("id_token").GetString()!.Split('.')[1])).RootElement;
            Assert.Equal(email, payload.GetProperty("email").GetString());
            Assert.True(payload.GetProperty("email_verified").GetBoolean());

            var userinfoRequest = new HttpRequestMessage(HttpMethod.Get, "/connect/userinfo");
            userinfoRequest.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer", tokens.GetProperty("access_token").GetString());
            var userinfoResponse = await client.SendAsync(userinfoRequest, TestContext.Current.CancellationToken);
            var userinfo = JsonDocument.Parse(await userinfoResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).RootElement;
            Assert.Equal(email, userinfo.GetProperty("email").GetString());
            Assert.True(userinfo.GetProperty("email_verified").GetBoolean());
        }

        [Fact]
        public async Task TokenEndpoint_WithWrongClientSecret_ReturnsInvalidClient()
        {
            using var client = CreateClient();
            await RegisterUserAsync(client);

            var authorizeResponse = await client.GetAsync(AuthorizeUrl, TestContext.Current.CancellationToken);
            var code = QueryHelpers.ParseQuery(authorizeResponse.Headers.Location!.Query)["code"].ToString();

            var tokenResponse = await client.PostAsync("/connect/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "authorization_code",
                    ["code"] = code,
                    ["redirect_uri"] = RedirectUri,
                    ["client_id"] = "test-client",
                    ["client_secret"] = "wrong-secret",
                }),
                TestContext.Current.CancellationToken);

            Assert.False(tokenResponse.IsSuccessStatusCode);
            var body = await tokenResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.Contains("invalid_client", body);
        }

        [Fact]
        public async Task Userinfo_RejectsSessionJwt()
        {
            using var client = CreateClient();
            var (auth, _) = await RegisterUserAsync(client);

            // A JwtService session token is not an OpenIddict access token.
            var request = new HttpRequestMessage(HttpMethod.Get, "/connect/userinfo");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
            var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Authorize_AfterSessionRevoked_RedirectsToLogin()
        {
            using var client = CreateClient();
            var (auth, _) = await RegisterUserAsync(client);

            // Revoke the session directly in the DB — the browser keeps its cookie,
            // proving the authorize endpoint's session check is the backstop.
            using (var scope = fixture.CreateScope())
            {
                var jwtService = scope.ServiceProvider.GetRequiredService<IJwtService>();
                var sessionService = scope.ServiceProvider.GetRequiredService<ISessionService>();
                var sessionId = jwtService.GetSessionIdFromToken(auth.AccessToken)!.Value;
                await sessionService.RevokeSessionAsync(sessionId);
            }

            var response = await client.GetAsync(AuthorizeUrl, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.Found, response.StatusCode);
            Assert.StartsWith("/login?returnUrl=", ToPathAndQuery(response.Headers.Location!));
        }

        /// <summary>The cookie handler emits absolute redirect URLs; compare path+query only.</summary>
        private static string ToPathAndQuery(Uri location)
            => location.IsAbsoluteUri ? location.PathAndQuery : location.OriginalString;
    }
}
