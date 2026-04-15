namespace AuthService.Services
{
    public interface IEmailManagementService
    {
        Task AddEmailAsync(Guid userId, string email);
        Task RemoveEmailAsync(Guid userId, string email);
        Task SetPrimaryEmailAsync(Guid userId, string email);
    }
}
