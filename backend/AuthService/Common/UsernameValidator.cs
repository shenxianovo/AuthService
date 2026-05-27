using System.Text.RegularExpressions;

namespace AuthService.Common
{
    /// <summary>
    /// Validates usernames following GitHub-style rules.
    /// </summary>
    public static class UsernameValidator
    {
        private static readonly Regex Pattern = new(
            @"^[a-z0-9](?:[a-z0-9]|-(?=[a-z0-9])){1,38}$",
            RegexOptions.Compiled);

        private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
        {
            "settings", "callback", "login", "logout", "register", "signup", "signin",
            "api", "admin", "oauth", "auth", "users", "user", "me",
            "public", "static", "assets", "well-known",
            "about", "help", "docs", "terms", "privacy", "contact", "status",
            "root", "system", "support", "official", "anonymous",
            "new", "edit", "delete", "create", "update",
            "null", "undefined", "true", "false",
        };

        /// <summary>
        /// True if the username conforms to format rules and is not reserved.
        /// Length: 3-39. Characters: lowercase letters, digits, hyphens.
        /// No leading/trailing hyphen, no consecutive hyphens.
        /// </summary>
        public static bool IsValid(string? username)
        {
            if (string.IsNullOrWhiteSpace(username)) return false;
            if (username.Length is < 3 or > 39) return false;
            if (!Pattern.IsMatch(username)) return false;
            if (Reserved.Contains(username)) return false;
            return true;
        }

        public static bool IsReserved(string username) => Reserved.Contains(username);
    }
}