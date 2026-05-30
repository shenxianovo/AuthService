using System.Security.Cryptography;
using AuthService.Services;

namespace AuthService.Tests.Fixtures
{
    /// <summary>
    /// In-memory RSA key provider for tests. Generates a fresh key pair in memory,
    /// no filesystem, no PEM serialization.
    /// </summary>
    public sealed class InMemoryRsaKeyProvider : IRsaKeyProvider
    {
        public RSA PrivateKey { get; }
        public RSA PublicKey { get; }

        public InMemoryRsaKeyProvider()
        {
            var rsa = RSA.Create(2048);

            // Private holds the full pair; public holds only the public parameters,
            // mirroring how the production provider loads separate PEM files.
            PrivateKey = rsa;

            PublicKey = RSA.Create();
            PublicKey.ImportParameters(rsa.ExportParameters(includePrivateParameters: false));
        }
    }
}
