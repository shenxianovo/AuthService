using System.Text.RegularExpressions;

namespace AuthService.Common
{
    /// <summary>
    /// Generates unique usernames for OAuth users by sanitizing provider hints
    /// (login, email local part) and resolving collisions with numeric suffixes.
    /// </summary>
    public static class UsernameGenerator
    {
        private const int MinLength = 3;
        private const int MaxLength = 39;

        /// <summary>
        /// Produce a unique username for a new user. Tries (in order):
        /// 1. Sanitized providerLogin (e.g. GitHub login)
        /// 2. Sanitized email local part
        /// 3. Random "user-{shortid}" fallback
        /// Appends "2", "3", ... if the candidate is taken or reserved.
        /// </summary>
        public static async Task<string> GenerateUniqueAsync(
            string? providerLogin,
            string? email,
            Func<string, Task<bool>> existsAsync)
        {
            var baseCandidate = Sanitize(providerLogin) ?? Sanitize(EmailLocalPart(email));

            if (baseCandidate is null || UsernameValidator.IsReserved(baseCandidate))
                return RandomFallback();

            var candidate = baseCandidate;
            for (int suffix = 2; suffix <= 9999; suffix++)
            {
                if (!await existsAsync(candidate))
                    return candidate;

                var suffixStr = suffix.ToString();
                if (baseCandidate.Length + suffixStr.Length > MaxLength)
                    return RandomFallback();

                candidate = $"{baseCandidate}{suffixStr}";
            }

            return RandomFallback();
        }

        private static string? Sanitize(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;

            var lower = input.ToLowerInvariant();
            var sanitized = Regex.Replace(lower, "[^a-z0-9-]", "-");
            sanitized = Regex.Replace(sanitized, "-+", "-");
            sanitized = sanitized.Trim('-');

            if (sanitized.Length > MaxLength)
                sanitized = sanitized[..MaxLength].TrimEnd('-');

            return sanitized.Length >= MinLength ? sanitized : null;
        }

        private static string? EmailLocalPart(string? email)
        {
            if (string.IsNullOrWhiteSpace(email)) return null;
            var atIdx = email.IndexOf('@');
            return atIdx > 0 ? email[..atIdx] : null;
        }

        private static string RandomFallback() =>
            "user-" + Guid.NewGuid().ToString("N")[..8];
    }
}