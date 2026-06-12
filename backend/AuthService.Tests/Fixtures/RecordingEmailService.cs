using AuthService.Services;

namespace AuthService.Tests.Fixtures
{
    /// <summary>
    /// No-op email service that records what would have been sent, so integration
    /// tests can pull the reset link "out of the mailbox".
    /// </summary>
    public sealed class RecordingEmailService : IEmailService
    {
        private readonly Lock _lock = new();
        private readonly List<(string Email, string Code)> _verificationCodes = [];
        private readonly List<(string Email, string Url)> _resetLinks = [];

        public Task SendVerificationCodeAsync(string toEmail, string displayName, string code)
        {
            lock (_lock) _verificationCodes.Add((toEmail, code));
            return Task.CompletedTask;
        }

        public Task SendPasswordResetLinkAsync(string toEmail, string displayName, string resetUrl)
        {
            lock (_lock) _resetLinks.Add((toEmail, resetUrl));
            return Task.CompletedTask;
        }

        public string? LastResetUrlFor(string email)
        {
            lock (_lock)
                return _resetLinks.LastOrDefault(l => l.Email == email).Url;
        }
    }
}
