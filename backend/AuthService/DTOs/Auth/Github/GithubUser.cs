using System.Text.Json.Serialization;

namespace AuthService.DTOs.Auth.Github
{
    public class GithubUser
    {
        public long Id { get; set; }
        public string Login { get; set; } = "";
        public string? Email { get; set; }
        [JsonPropertyName("avatar_url")]
        public string AvatarUrl { get; set; } = "";
    }
}
