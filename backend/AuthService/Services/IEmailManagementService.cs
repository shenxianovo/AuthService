using AuthService.Common;

namespace AuthService.Services
{
    public interface IEmailManagementService
    {
        Task<Result> AddEmailAsync(Guid userId, string email);
        Task<Result> RemoveEmailAsync(Guid userId, string email);
        Task<Result> SetPrimaryEmailAsync(Guid userId, string email);
    }
}
