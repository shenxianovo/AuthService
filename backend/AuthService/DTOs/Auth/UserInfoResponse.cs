namespace AuthService.DTOs.Auth
{
    public class UserInfoResponse
    {
        public Guid UserId { get; set; }
        public string DisplayName { get; set; } = null!;

        /// <summary>"User" or "Admin". Drives admin UI visibility only — the
        /// server re-checks the database on every admin request.</summary>
        public string Role { get; set; } = null!;

        public DateTimeOffset CreatedAt { get; set; }
        public bool HasPassword { get; set; }
        public List<EmailInfo> Emails { get; set; } = [];
        public List<ProviderInfo> Providers { get; set; } = [];
    }

    public class EmailInfo
    {
        public string Email { get; set; } = null!;
        public bool IsPrimary { get; set; }
        public bool IsVerified { get; set; }
    }

    public class ProviderInfo
    {
        public string Provider { get; set; } = null!;
        public DateTimeOffset LinkedAt { get; set; }
    }
}
