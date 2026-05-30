using System.Security.Cryptography;
using AuthService.Configuration;
using Microsoft.Extensions.Options;

namespace AuthService.Services
{
    /// <summary>
    /// Supplies the RSA key pair used to sign and verify JWTs.
    /// Separating key acquisition from token signing keeps JwtService free of
    /// filesystem concerns and lets tests inject an in-memory key pair.
    /// </summary>
    public interface IRsaKeyProvider
    {
        RSA PrivateKey { get; }
        RSA PublicKey { get; }
    }

    /// <summary>
    /// Loads the RSA key pair from PEM files on disk (paths from JwtOptions).
    /// Registered as a singleton: keys are read once at construction.
    /// </summary>
    public sealed class PemFileRsaKeyProvider : IRsaKeyProvider
    {
        public RSA PrivateKey { get; }
        public RSA PublicKey { get; }

        public PemFileRsaKeyProvider(IOptions<JwtOptions> options)
        {
            var opts = options.Value;

            PrivateKey = RSA.Create();
            PrivateKey.ImportFromPem(File.ReadAllText(opts.PrivateKeyPath));

            PublicKey = RSA.Create();
            PublicKey.ImportFromPem(File.ReadAllText(opts.PublicKeyPath));
        }
    }
}
