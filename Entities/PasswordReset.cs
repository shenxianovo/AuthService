namespace AuthService.Entities
{
    public class PasswordReset
    {
        public Guid Id { get; set; } = Guid.CreateVersion7();
        public Guid UserId { get; set; }
        public string TokenHash { get; set; } = null!;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
        public bool Used { get; set; }

        public User User { get; set; } = null!;
    }
}
