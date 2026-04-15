namespace AuthService.Services
{
    public interface IEmailVerificationService
    {
        Task SendVerificationCodeAsync(Guid userId, EmailTarget? target = null);
        Task VerifyCodeAsync(Guid userId, string code, EmailTarget? target = null);
    }
}
