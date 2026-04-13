using AuthService.DTOs.Auth;
using AuthService.DTOs.Auth.Github;
using AuthService.Entities;
using AuthService.Configuration;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

namespace AuthService.Services
{
    public interface IGithubAuthService
    {
        Task<AuthResponse> LoginAsync(string code, string ipAddress, string device, Guid? currentUserId = null);
    }

    public class GithubAuthService(
        HttpClient http, 
        IOptions<GithubOAuthOptions> options, 
        IOAuthService oauthService,
        SessionService sessionService) : IGithubAuthService
    {
        private readonly GithubOAuthOptions _options = options.Value;

        public async Task<AuthResponse> LoginAsync(string code, string ipAddress, string device, Guid? currentUserId = null)
        {
            var token = await ExchangeCode(code);
            var githubUser = await GetGithubUser(token);

            var user = await oauthService.ProcessOAuthLoginAsync(
                AuthProviderType.Github,
                githubUser.Id.ToString(),
                githubUser.Email,
                githubUser.Login,
                currentUserId);

            return await sessionService.CreateSessionAsync(user.Id, ipAddress, device);
        }

        private async Task<string> ExchangeCode(string code)
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

            var response = await http.SendAsync(request);
            
            response.EnsureSuccessStatusCode();

            var jsonContent = await response.Content.ReadAsStringAsync();
            var result = System.Text.Json.JsonSerializer.Deserialize<GithubTokenResponse>(jsonContent);

            if (result == null || string.IsNullOrEmpty(result.AccessToken))
            {
                throw new InvalidOperationException($"Failed to exchange GitHub code. GitHub response: {jsonContent}");
            }

            return result.AccessToken;
        }

        private async Task<GithubUser> GetGithubUser(string accessToken)
        {
            http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            http.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("AuthService", "1.0"));

            var user = await http.GetFromJsonAsync<GithubUser>("https://api.github.com/user");

            return user!;
        }
    }
}