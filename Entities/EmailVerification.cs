namespace AuthService.Entities
{
    public class EmailVerification
    {
        public Guid Id { get; set; }
        public Guid UserEmailId { get; set; }
        public string TokenHash { get; set; } = null!;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
        public bool Used { get; set; }
    }
}
