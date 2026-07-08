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
    }
}
