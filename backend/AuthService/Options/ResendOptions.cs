namespace AuthService.Configuration
{
    public class ResendOptions
    {
        public string ApiKey { get; set; } = null!;
        public string FromEmail { get; set; } = "noreply@shenxianovo.com";
        public string FromName { get; set; } = "AuthService";
        public int VerificationCodeExpirationMinutes { get; set; } = 15;
        public int PasswordResetExpirationMinutes { get; set; } = 30;

        /// <summary>Frontend page that consumes the reset token (?token= is appended).</summary>
        public string PasswordResetUrlBase { get; set; } = "https://auth.shenxianovo.com/reset-password";
    }
}
