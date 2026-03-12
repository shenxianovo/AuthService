namespace AuthService.Entities
{
    public class PasswordCredential
    {
        public Guid UserId { get; set; }
        
        public string PasswordHash { get; set; } = null!;
        public DateTimeOffset CreatedAt { get; set;}
        public DateTimeOffset? UpdatedAt { get; set;}

        public User User { get; set; } = null!;
    }
}
