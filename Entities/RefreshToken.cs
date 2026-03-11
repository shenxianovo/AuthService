namespace AuthService.Entities
{
    public class RefreshToken
    {
        public Guid Id { get; set; } = Guid.CreateVersion7();
        
        public string TokenHash { get; set; } = null!;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
        public bool Revoked { get; set; }

        public Guid SessionId { get; set; }
        public Session Session { get; set; } = null!;
    }
}
