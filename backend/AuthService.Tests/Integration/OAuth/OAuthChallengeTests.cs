using System.Net;
using AuthService.Tests.Fixtures;
using Microsoft.AspNetCore.WebUtilities;

namespace AuthService.Tests.Integration.OAuth
{
    /// <summary>
    /// The OpenIddict client owns the provider round-trip, so integration tests
    /// assert the outward-facing halves: the challenge redirect it produces and
    /// the redirect-URL gate in front of it (ADR-018). Only GitHub is asserted —
    /// its endpoints are static, while Google requires live OIDC discovery
    /// (a network call) at challenge time.
    /// </summary>
    [Collection("Api Tests")]
    public class OAuthChallengeTests(ApiTestFixture fixture)
    {
        private readonly HttpClient _client = fixture.Client;

        [Fact]
        public async Task GithubLogin_RedirectsToGithubAuthorize_WithProtectedState()
        {
            var response = await _client.GetAsync(
                "/api/v1/auth/github/login?redirectUrl=https://example.com/callback",
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.Found, response.StatusCode);
            var location = response.Headers.Location!;
            Assert.Equal("github.com", location.Host);
            Assert.Equal("/login/oauth/authorize", location.AbsolutePath);

            var query = QueryHelpers.ParseQuery(location.Query);
            Assert.Equal("test-github-client", query["client_id"].ToString());
            Assert.False(string.IsNullOrEmpty(query["state"].ToString()));
            Assert.Contains("api/v1/auth/github/callback", query["redirect_uri"].ToString());
            Assert.Contains("user:email", query["scope"].ToString());
        }

        [Fact]
        public async Task Login_WithDisallowedRedirectUrl_IsRejected()
        {
            var response = await _client.GetAsync(
                "/api/v1/auth/github/login?redirectUrl=https://evil.com/steal",
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Callback_WithoutValidState_Fails()
        {
            var response = await _client.GetAsync(
                "/api/v1/auth/github/callback?code=fake&state=forged",
                TestContext.Current.CancellationToken);

            // The OpenIddict client rejects the forged state before our action runs.
            Assert.True(
                response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.InternalServerError or HttpStatusCode.NotFound,
                $"Expected a rejection, got {(int)response.StatusCode}");
        }
    }
}
