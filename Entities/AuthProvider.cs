namespace AuthService.Entities
{
    public class AuthProvider
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public AuthProviderType Provider { get; set; }
        public string ProviderUserId { get; set; } = null!;
        public DateTimeOffset CreatedAt { get; set; }

        public User User { get; set; } = null!;
    }

    public enum AuthProviderType
    {
        Password = 0,
    }
}
