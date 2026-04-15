namespace AuthService.Entities
{
    public class PasswordCredential
    {
        public Guid UserId { get; set; }
        
        public string PasswordHash { get; set; } = null!;
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? UpdatedAt { get; set;}

        public User User { get; set; } = null!;
    }
}
