namespace AuthService.Services
{
    public interface IEmailService
    {
        Task SendVerificationCodeAsync(string toEmail, string displayName, string code);
    }
}
