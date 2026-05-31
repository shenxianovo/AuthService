using AuthService.Common;
using AuthService.Configuration;
using AuthService.DTOs.Auth;
using AuthService.DTOs.Auth.Github;
using AuthService.Entities;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

namespace AuthService.Services
{
    public interface IGithubAuthService
    {
        Task<Result<AuthResponse>> LoginAsync(string code, string ipAddress, string device, Guid? currentUserId = null);
    }

    public class GithubAuthService(
        HttpClient http,
        IOptions<GithubOAuthOptions> options,
        IOAuthService oauthService,
        ISessionService sessionService)
        : OAuthProviderServiceBase(http, oauthService, sessionService), IGithubAuthService
    {
        private readonly GithubOAuthOptions _options = options.Value;

        protected override AuthProviderType ProviderType => AuthProviderType.Github;

        protected override async Task<string> ExchangeCodeAsync(string code)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/oauth/access_token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = _options.ClientId,
                    ["client_secret"] = _options.ClientSecret,
                    ["code"] = code,
                    ["redirect_uri"] = _options.CallbackUrl
                })
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await Http.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var jsonContent = await response.Content.ReadAsStringAsync();
            var result = System.Text.Json.JsonSerializer.Deserialize<GithubTokenResponse>(jsonContent);

            if (result == null || string.IsNullOrEmpty(result.AccessToken))
                throw new InvalidOperationException($"Failed to exchange GitHub code. GitHub response: {jsonContent}");

            return result.AccessToken;
        }

        protected override async Task<OAuthUserInfo> GetUserInfoAsync(string accessToken)
        {
            SetBearerToken(accessToken);
            Http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("AuthService", "1.0"));

            var user = await Http.GetFromJsonAsync<GithubUser>("https://api.github.com/user");

            // GET /user only carries the public profile email with no verification
            // status (and it may be null if hidden). The primary, verified address
            // lives in /user/emails, which is the authoritative source for both.
            var emails = await Http.GetFromJsonAsync<List<GithubEmail>>("https://api.github.com/user/emails");
            var primary = emails?.FirstOrDefault(e => e.Primary);

            return new OAuthUserInfo(
                ProviderUserId: user!.Id.ToString(),
                Email: primary?.Email ?? user.Email,
                DisplayName: user.Login,
                ProviderLogin: user.Login,
                EmailVerified: primary?.Verified ?? false
            );
        }
    }
}