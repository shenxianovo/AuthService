namespace AuthService.Services
{
    public interface IEmailVerificationService
    {
        Task SendVerificationCodeAsync(Guid userId);
        Task VerifyCodeAsync(Guid userId, string code);
    }
}
