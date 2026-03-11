namespace AuthService.Entities
{
    public class User
    {
        public Guid Id { get; set; }
        public string DisplayName { get; set; } = null!;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }

        public ICollection<UserEmail> Emails { get; } = [];
        public ICollection<AuthProvider> Providers { get; } = [];
        public PasswordCredential? PasswordCredential { get; set; }
    }
}
