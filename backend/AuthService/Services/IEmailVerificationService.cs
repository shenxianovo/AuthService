namespace AuthService.Services
{
    public interface IEmailVerificationService
    {
        Task SendVerificationCodeAsync(Guid userId);
        Task SendVerificationCodeByEmailIdAsync(Guid userId, Guid emailId);
        Task VerifyCodeAsync(Guid userId, string code);
    }
}
