using AuthService.Common;
using AuthService.Configuration;
using AuthService.DTOs.Auth;
using AuthService.DTOs.Auth.Google;
using AuthService.Entities;
using Microsoft.Extensions.Options;

namespace AuthService.Services
{
    public interface IGoogleAuthService
    {
        Task<Result<AuthResponse>> LoginAsync(string code, string ipAddress, string device, Guid? currentUserId = null);
    }

    public class GoogleAuthService(
        HttpClient http,
        IOptions<GoogleOAuthOptions> options,
        IOAuthService oauthService,
        ISessionService sessionService)
        : OAuthProviderServiceBase(http, oauthService, sessionService), IGoogleAuthService
    {
        private readonly GoogleOAuthOptions _options = options.Value;

        protected override AuthProviderType ProviderType => AuthProviderType.Google;

        protected override async Task<string> ExchangeCodeAsync(string code)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = _options.ClientId,
                    ["client_secret"] = _options.ClientSecret,
                    ["code"] = code,
                    ["redirect_uri"] = _options.CallbackUrl,
                    ["grant_type"] = "authorization_code"
                })
            };

            var response = await Http.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<GoogleTokenResponse>();

            if (result == null || string.IsNullOrEmpty(result.AccessToken))
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Failed to exchange Google code. Google response: {jsonContent}");
            }

            return result.AccessToken;
        }

        protected override async Task<OAuthUserInfo> GetUserInfoAsync(string accessToken)
        {
            SetBearerToken(accessToken);

            var user = await Http.GetFromJsonAsync<GoogleUserInfo>("https://www.googleapis.com/oauth2/v3/userinfo");

            return new OAuthUserInfo(
                ProviderUserId: user!.Id,
                Email: user.Email,
                DisplayName: user.Name
            );
        }
    }
}