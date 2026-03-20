namespace AuthService.Entities
{
    public class AuthProvider
    {
        public Guid Id { get; set; } = Guid.CreateVersion7();
        
        public AuthProviderType Provider { get; set; }
        public string ProviderUserId { get; set; } = null!;
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        public Guid UserId { get; set; }
        public User User { get; set; } = null!;
    }

    public enum AuthProviderType
    {
        Password = 0,
        Github = 1,
        Google = 2
    }
}
