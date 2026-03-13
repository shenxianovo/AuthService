namespace AuthService.Entities
{
    public class PasswordReset
    {
        public Guid Id { get; set; } = Guid.CreateVersion7();
        
        public string TokenHash { get; set; } = null!;
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset ExpiresAt { get; set; }
        public bool Used { get; set; }

        public Guid UserId { get; set; }
        public User User { get; set; } = null!;
    }
}
