using System.Text.Json.Serialization;

namespace AuthService.DTOs.Auth.Github
{
    public class GithubTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = "";
    }
}
