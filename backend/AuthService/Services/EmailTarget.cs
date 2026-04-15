namespace AuthService.Services
{
    public abstract class EmailTarget
    {
        private EmailTarget() { }

        public static EmailTarget Primary { get; } = new PrimaryTarget();
        public static EmailTarget ById(Guid emailId) => new ByIdTarget(emailId);
        public static EmailTarget ByAddress(string email) => new ByAddressTarget(email);

        public sealed class PrimaryTarget : EmailTarget { }
        public sealed class ByIdTarget(Guid emailId) : EmailTarget { public Guid EmailId => emailId; }
        public sealed class ByAddressTarget(string email) : EmailTarget { public string Email => email; }
    }
}
