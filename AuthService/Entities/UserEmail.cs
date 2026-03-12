namespace AuthService.Entities
{
    public class UserEmail
    {
        public Guid Id { get; set; } = Guid.CreateVersion7();
        
        public string Email { get; set; } = null!;
        public bool IsPrimary { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? VerifiedAt { get; set; }

        public Guid UserId { get; set; }
        public User User { get; set; } = null!;
    }
}
