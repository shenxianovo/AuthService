namespace AuthService.DTOs.Auth.Github
{
    public class GithubLoginState
    {
        public string RedirectUrl { get; set; } = string.Empty;
        public string? Token { get; set; }
    }
}