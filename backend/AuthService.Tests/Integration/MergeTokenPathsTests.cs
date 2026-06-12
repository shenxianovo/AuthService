using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AuthService.Data;
using AuthService.DTOs.ApiKeys;
using AuthService.DTOs.Auth;
using AuthService.Services;
using AuthService.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace AuthService.Tests.Integration
{
    /// <summary>
    /// End-to-end token-path regressions for account merge: what happens to the
    /// merged-away (soft-deleted) source user's credentials, exercised over HTTP.
    /// Completes the merge guard (see MergeCompletenessGuardTests) at the seam
    /// callers actually use.
    /// </summary>
    [Collection("Api Tests")]
    public class MergeTokenPathsTests(ApiTestFixture fixture)
    {
        private readonly HttpClient _client = fixture.Client;
        private readonly ApiTestFixture _fixture = fixture;

        private async Task<AuthResponse> RegisterUserAsync()
        {
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest
            {
                Username = $"u{Guid.NewGuid():N}"[..15],
                DisplayName = "MergePathUser",
                Email = $"merge-{Guid.NewGuid():N}@example.com",
                Password = "SecurePass123",
            }, TestContext.Current.CancellationToken);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<AuthResponse>(TestContext.Current.CancellationToken))!;
        }

        private async Task MergeAsync(Guid sourceUserId, Guid targetUserId)
        {
            using var scope = _fixture.CreateScope();
            var account = scope.ServiceProvider.GetRequiredService<IAccountService>();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await account.MergeAsync(sourceUserId, targetUserId);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        [Fact]
        public async Task AfterMerge_SourceRefreshToken_IsRejected()
        {
            var target = await RegisterUserAsync();
            var source = await RegisterUserAsync();

            await MergeAsync(source.UserId, target.UserId);

            var response = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest
            {
                RefreshToken = source.RefreshToken
            }, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task AfterMerge_TargetRefreshToken_StillWorks()
        {
            var target = await RegisterUserAsync();
            var source = await RegisterUserAsync();

            await MergeAsync(source.UserId, target.UserId);

            var response = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest
            {
                RefreshToken = target.RefreshToken
            }, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task AfterMerge_SourceAccessToken_GetsNotFoundFromMe()
        {
            var target = await RegisterUserAsync();
            var source = await RegisterUserAsync();

            await MergeAsync(source.UserId, target.UserId);

            // ADR-001 window: the JWT itself stays valid up to 15 minutes, but the
            // account behind it is gone — /me must not resolve it.
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", source.AccessToken);
            var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task AfterMerge_SourceApiKey_StillExchanges()
        {
            var target = await RegisterUserAsync();
            var source = await RegisterUserAsync();

            // Source creates an API key before being merged away.
            var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/apikeys")
            {
                Content = JsonContent.Create(new CreateApiKeyRequest { Name = "agent" })
            };
            createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", source.AccessToken);
            var createResponse = await _client.SendAsync(createRequest, TestContext.Current.CancellationToken);
            createResponse.EnsureSuccessStatusCode();
            var created = (await createResponse.Content.ReadFromJsonAsync<CreateApiKeyResponse>(TestContext.Current.CancellationToken))!;

            await MergeAsync(source.UserId, target.UserId);

            // The key migrated to the target (ADR-010) and must keep working.
            var exchange = await _client.PostAsJsonAsync("/api/v1/apikeys/exchange", new ExchangeApiKeyRequest
            {
                ApiKey = created.Key
            }, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, exchange.StatusCode);
        }
    }
}
