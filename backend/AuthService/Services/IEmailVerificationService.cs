using AuthService.Common;

namespace AuthService.Services
{
    public interface IEmailVerificationService
    {
        Task<Result> SendVerificationCodeAsync(Guid userId, EmailTarget? target = null);
        Task<Result> VerifyCodeAsync(Guid userId, string code, EmailTarget? target = null);
    }
}
