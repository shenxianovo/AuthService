using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AuthService.Services
{
    public class JwtOptions
    {
        public const string Section = "Jwt";

        public string PrivateKeyPem { get; set; } = null!;
        public string PublicKeyPem { get; set; } = null!;
        public string Issuer { get; set; } = null!;
        public string Audience { get; set; } = null!;
        public int AccessTokenExpirationMinutes { get; set; } = 15;
        public int RefreshTokenExpirationDays { get; set; } = 30;
        public int SessionExpirationDays { get; set; } = 30;
    }

    public interface IJwtService
    {
        string GenerateAccessToken(Guid userId, Guid sessionId);
        RsaSecurityKey GetPublicKey();
    }

    public class JwtService : IJwtService
    {
        private readonly JwtOptions _options;
        private readonly RSA _privateKey;
        private readonly RSA _publicKey;

        public JwtService(IOptions<JwtOptions> options)
        {
            _options = options.Value;

            _privateKey = RSA.Create();
            _privateKey.ImportFromPem(_options.PrivateKeyPem);

            _publicKey = RSA.Create();
            _publicKey.ImportFromPem(_options.PublicKeyPem);
        }

        public string GenerateAccessToken(Guid userId, Guid sessionId)
        {
            var signingKey = new RsaSecurityKey(_privateKey);
            var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
                new Claim("sid", sessionId.ToString()),
            };

            var token = new JwtSecurityToken(
                issuer: _options.Issuer,
                audience: _options.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_options.AccessTokenExpirationMinutes),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public RsaSecurityKey GetPublicKey()
        {
            return new RsaSecurityKey(_publicKey);
        }
    }
}
