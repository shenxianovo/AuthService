namespace AuthService.Configuration
{
    public class OidcOptions
    {
        public const string Section = "Oidc";

        /// <summary>
        /// Base64-encoded 32-byte symmetric key encrypting OpenIddict authorization
        /// codes and refresh tokens. Must stay stable across restarts.
        /// Generate with: openssl rand -base64 32
        /// </summary>
        public string EncryptionKey { get; set; } = string.Empty;
    }
}
