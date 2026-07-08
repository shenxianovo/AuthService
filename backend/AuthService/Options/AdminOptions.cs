namespace AuthService.Configuration
{
    public class AdminOptions
    {
        public const string Section = "Admin";

        /// <summary>
        /// Username promoted to Admin at startup (idempotent). Registration is
        /// public, so the bootstrap admin must be designated explicitly — never
        /// first-user-auto. Empty = no bootstrap promotion.
        /// </summary>
        public string BootstrapUsername { get; set; } = string.Empty;
    }
}
