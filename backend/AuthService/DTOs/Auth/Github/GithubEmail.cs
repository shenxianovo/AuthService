using System.Text.Json.Serialization;

namespace AuthService.DTOs.Auth.Github
{
    /// <summary>
    /// One entry from GitHub's GET /user/emails. The /user endpoint only returns the
    /// public profile email (no verification status), so verified state is sourced here.
    /// </summary>
    public class GithubEmail
    {
        [JsonPropertyName("email")]
        public string Email { get; set; } = "";

        [JsonPropertyName("primary")]
        public bool Primary { get; set; }

        [JsonPropertyName("verified")]
        public bool Verified { get; set; }
    }
}
