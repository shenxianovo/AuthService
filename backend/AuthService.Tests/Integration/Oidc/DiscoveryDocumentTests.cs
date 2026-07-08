using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using AuthService.Services;
using AuthService.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace AuthService.Tests.Integration.Oidc
{
    [Collection("Api Tests")]
    public class DiscoveryDocumentTests(ApiTestFixture fixture)
    {
        private readonly HttpClient _client = fixture.Client;

        [Fact]
        public async Task Discovery_ExposesOidcEndpoints()
        {
            var response = await _client.GetAsync("/.well-known/openid-configuration", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            var root = doc.RootElement;

            Assert.StartsWith("https://localhost", root.GetProperty("issuer").GetString());
            Assert.EndsWith("/connect/authorize", root.GetProperty("authorization_endpoint").GetString());
            Assert.EndsWith("/connect/token", root.GetProperty("token_endpoint").GetString());
            Assert.EndsWith("/connect/userinfo", root.GetProperty("userinfo_endpoint").GetString());
            // The JWKS keeps its pre-OpenIddict path so downstream verifiers are unaffected.
            Assert.EndsWith("/.well-known/jwks.json", root.GetProperty("jwks_uri").GetString());

            var responseTypes = root.GetProperty("response_types_supported").EnumerateArray()
                .Select(e => e.GetString()).ToList();
            Assert.Contains("code", responseTypes);

            var grantTypes = root.GetProperty("grant_types_supported").EnumerateArray()
                .Select(e => e.GetString()).ToList();
            Assert.Contains("authorization_code", grantTypes);
            Assert.Contains("refresh_token", grantTypes);
        }

        [Fact]
        public async Task Jwks_ServesTheSameRsaKeyAsJwtService()
        {
            var response = await _client.GetAsync("/.well-known/jwks.json", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            var keys = doc.RootElement.GetProperty("keys").EnumerateArray().ToList();
            var rsaKey = Assert.Single(keys, k => k.GetProperty("kty").GetString() == "RSA");

            // Regression guard: downstream services verify session JWTs against this
            // JWKS — the published key must stay the JwtService signing key.
            var expected = fixture.Factory.Services.GetRequiredService<IRsaKeyProvider>()
                .PublicKey.ExportParameters(includePrivateParameters: false);

            Assert.Equal(Base64UrlEncoder.Encode(expected.Modulus!), rsaKey.GetProperty("n").GetString());
            Assert.Equal(Base64UrlEncoder.Encode(expected.Exponent!), rsaKey.GetProperty("e").GetString());
        }
    }
}
