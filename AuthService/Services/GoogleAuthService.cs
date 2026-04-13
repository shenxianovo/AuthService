using AuthService.DTOs.Auth;
using AuthService.DTOs.Auth.Google;
using AuthService.Entities;
using AuthService.Configuration;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

namespace AuthService.Services
{
    public interface IGoogleAuthService
    {
        Task<AuthResponse> LoginAsync(string code, string ipAddress, string device, Guid? currentUserId = null);
    }

    public class GoogleAuthService(
        HttpClient http,
        IOptions<GoogleOAuthOptions> options,
        IOAuthService oauthService,
        ISessionService sessionService) : IGoogleAuthService
    {
        private readonly GoogleOAuthOptions _options = options.Value;

        public async Task<AuthResponse> LoginAsync(string code, string ipAddress, string device, Guid? currentUserId = null)
        {
            var token = await ExchangeCode(code);
            var googleUser = await GetGoogleUser(token);

            var user = await oauthService.ProcessOAuthLoginAsync(
                AuthProviderType.Google,
                googleUser.Id,
                googleUser.Email,
                googleUser.Name,
                currentUserId);

            return await sessionService.CreateSessionAsync(user.Id, ipAddress, device);
        }

        private async Task<string> ExchangeCode(string code)
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

            var response = await http.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<GoogleTokenResponse>();

            if (result == null || string.IsNullOrEmpty(result.AccessToken))
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Failed to exchange Google code. Google response: {jsonContent}");
            }

            return result.AccessToken;
        }

        private async Task<GoogleUserInfo> GetGoogleUser(string accessToken)
        {
            http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            var user = await http.GetFromJsonAsync<GoogleUserInfo>("https://www.googleapis.com/oauth2/v3/userinfo");

            return user!;
        }
    }
}
