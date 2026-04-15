namespace AuthService.Configuration
{
    public class JwtOptions
    {
        public const string Section = "Jwt";

        public string PrivateKeyPath { get; set; } = null!;
        public string PublicKeyPath { get; set; } = null!;
        public string Issuer { get; set; } = null!;
        public string Audience { get; set; } = null!;
        public int AccessTokenExpirationMinutes { get; set; } = 15;
        public int RefreshTokenExpirationDays { get; set; } = 30;
        public int SessionExpirationDays { get; set; } = 30;
    }
}
