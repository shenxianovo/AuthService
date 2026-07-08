namespace AuthService.Common
{
    /// <summary>Authentication scheme names beyond the JwtBearer default.</summary>
    public static class AuthConstants
    {
        /// <summary>
        /// Cookie scheme backing the interactive OIDC authorize flow. Issued when a
        /// login completes in the browser; consumed only by /connect/* endpoints.
        /// </summary>
        public const string InteractiveScheme = "Interactive";

        /// <summary>
        /// Authorization policy for the admin surface. The handler checks
        /// User.Role in the database per request; Role never rides in a token
        /// (CONTEXT.md "Role", ADR-017).
        /// </summary>
        public const string AdminPolicy = "RequireAdmin";
    }
}
