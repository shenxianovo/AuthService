namespace AuthService.Entities
{
    public class EmailVerification
    {
        public Guid Id { get; set; } = Guid.CreateVersion7();
        public Guid UserEmailId { get; set; }
        public string TokenHash { get; set; } = null!;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
        public bool Used { get; set; }

        public UserEmail UserEmail { get; set; } = null!;
    }
}
