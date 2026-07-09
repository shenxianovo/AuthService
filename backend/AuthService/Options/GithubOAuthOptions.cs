namespace AuthService.Configuration
{
    public class GithubOAuthOptions
    {
        public const string Section = "GithubOAuth";

        public string ClientId { get; set; } = null!;
        public string ClientSecret { get; set; } = null!;
    }
}
