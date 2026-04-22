using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using AuthService.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AuthService.Services
{
    public interface IJwtService
    {
        string GenerateAccessToken(Guid userId, params Claim[] extraClaims);
        RsaSecurityKey GetPublicKey();
        Guid? ValidateTokenAndGetUserId(string token);
        Guid? GetSessionIdFromToken(string token);
    }

    public class JwtService : IJwtService
    {
        private readonly JwtOptions _options;
        private readonly RSA _privateKey;
        private readonly RSA _publicKey;

        public JwtService(IOptions<JwtOptions> options)
        {
            _options = options.Value;

            var privateKeyPem = File.ReadAllText(_options.PrivateKeyPath);
            var publicKeyPem = File.ReadAllText(_options.PublicKeyPath);

            _privateKey = RSA.Create();
            _privateKey.ImportFromPem(privateKeyPem);

            _publicKey = RSA.Create();
            _publicKey.ImportFromPem(publicKeyPem);
        }

        public string GenerateAccessToken(Guid userId, params Claim[] extraClaims)
        {
            var signingKey = new RsaSecurityKey(_privateKey);
            var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256);

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
            };
            claims.AddRange(extraClaims);

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

        public Guid? ValidateTokenAndGetUserId(string token)
        {
            var principal = ValidateToken(token);
            if (principal == null) return null;
            var userIdStr = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdStr, out var userId) ? userId : null;
        }

        public Guid? GetSessionIdFromToken(string token)
        {
            var principal = ValidateToken(token);
            if (principal == null) return null;
            var sidStr = principal.FindFirst("sid")?.Value;
            return Guid.TryParse(sidStr, out var sessionId) ? sessionId : null;
        }

        private ClaimsPrincipal? ValidateToken(string token)
        {
            var handler = new JwtSecurityTokenHandler();
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _options.Issuer,
                ValidAudience = _options.Audience,
                IssuerSigningKey = GetPublicKey()
            };

            try
            {
                return handler.ValidateToken(token, validationParameters, out _);
            }
            catch
            {
                return null;
            }
        }
    }
}
