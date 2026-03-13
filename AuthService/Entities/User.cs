namespace AuthService.Entities
{
    public class User
    {
        public Guid Id { get; set; } = Guid.CreateVersion7();
        public string DisplayName { get; set; } = null!;
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }

        public ICollection<UserEmail> Emails { get; } = [];
        public ICollection<AuthProvider> Providers { get; } = [];
        public ICollection<Session> Sessions { get; } = [];
        public PasswordCredential? PasswordCredential { get; set; }
    }
}
