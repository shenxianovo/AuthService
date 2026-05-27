namespace AuthService.DTOs.User
{
    public class PublicUserResponse
    {
        public Guid Id { get; set; }
        public string Username { get; set; } = null!;
        public string DisplayName { get; set; } = null!;
    }
}