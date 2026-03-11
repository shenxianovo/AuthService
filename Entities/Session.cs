namespace AuthService.Entities
{
    public class Session
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Device { get; set; } = null!;
        public string IpAddress { get; set; } = null!;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset ExpiredAt { get; set; }
        public bool Revoked { get; set; }

        public User User { get; set; } = null!;
        public ICollection<RefreshToken> RefreshTokens { get; } = [];
    }
}
