namespace AuthService.Entities
{
    /// <summary>
    /// Internal authorization tier governing only AuthService's own admin surface.
    /// Deliberately never emitted in any token or OIDC claim — see the Role entry
    /// in CONTEXT.md and ADR-017.
    /// </summary>
    public enum UserRole
    {
        User = 0,
        Admin = 1,
    }

    public class User
    {
        public Guid Id { get; set; } = Guid.CreateVersion7();
        public string Username { get; set; } = "user-" + Guid.NewGuid().ToString("N")[..8];
        public string DisplayName { get; set; } = null!;
        public UserRole Role { get; set; } = UserRole.User;
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }

        public ICollection<UserEmail> Emails { get; } = [];
        public ICollection<AuthProvider> Providers { get; } = [];
        public ICollection<Session> Sessions { get; } = [];
        public ICollection<ApiKey> ApiKeys { get; } = [];
        public PasswordCredential? PasswordCredential { get; set; }
    }
}