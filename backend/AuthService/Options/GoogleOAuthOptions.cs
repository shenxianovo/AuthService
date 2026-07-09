namespace AuthService.Configuration
{
    public class GoogleOAuthOptions
    {
        public const string Section = "GoogleOAuth";

        public string ClientId { get; set; } = null!;
        public string ClientSecret { get; set; } = null!;
    }
}
