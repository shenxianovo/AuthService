namespace AuthService.Options
{
    public class ResendOptions
    {
        public string ApiKey { get; set; } = null!;
        public string FromEmail { get; set; } = "noreply@shenxianovo.com";
        public string FromName { get; set; } = "AuthService";
        public int VerificationCodeExpirationMinutes { get; set; } = 15;
    }
}
