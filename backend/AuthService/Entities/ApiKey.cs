namespace AuthService.Entities
{
    public class ApiKey
    {
        public Guid Id { get; set; } = Guid.CreateVersion7();
        public string Name { get; set; } = null!;
        public string Prefix { get; set; } = null!;
        public string SecretHash { get; set; } = null!;
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? LastUsedAt { get; set; }
        public bool IsRevoked { get; set; }

        public Guid UserId { get; set; }
        public User User { get; set; } = null!;
    }
}