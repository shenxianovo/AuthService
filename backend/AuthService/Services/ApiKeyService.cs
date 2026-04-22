using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AuthService.Common;
using AuthService.Configuration;
using AuthService.Data;
using AuthService.DTOs.ApiKeys;
using AuthService.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AuthService.Services
{
    public interface IApiKeyService
    {
        Task<CreateApiKeyResponse> CreateAsync(Guid userId, string name);
        Task<List<ApiKeyListItem>> ListAsync(Guid userId);
        Task<Result> RevokeAsync(Guid userId, Guid apiKeyId);
        Task<Result<ExchangeApiKeyResponse>> ExchangeAsync(string rawKey);
    }

    public class ApiKeyService(AppDbContext db, IJwtService jwtService, IOptions<JwtOptions> jwtOptions) : IApiKeyService
    {
        private const int SecretBytes = 32;
        private const int PrefixLength = 8;

        public async Task<CreateApiKeyResponse> CreateAsync(Guid userId, string name)
        {
            // Generate random secret
            var secretBytes = RandomNumberGenerator.GetBytes(SecretBytes);
            var secretBase64 = Convert.ToBase64String(secretBytes)
                .Replace("+", "-").Replace("/", "_").TrimEnd('='); // URL-safe

            // Build prefix from first 8 chars of a separate random string (not derived from secret)
            var prefixBytes = RandomNumberGenerator.GetBytes(6);
            var prefix = Convert.ToBase64String(prefixBytes)
                .Replace("+", "-").Replace("/", "_").TrimEnd('=')[..PrefixLength];

            var rawKey = $"ak_{prefix}_{secretBase64}";
            var secretHash = ComputeSha256(secretBase64);

            var entity = new ApiKey
            {
                Name = name,
                Prefix = prefix,
                SecretHash = secretHash,
                UserId = userId,
            };

            db.ApiKeys.Add(entity);
            await db.SaveChangesAsync();

            return new CreateApiKeyResponse
            {
                Id = entity.Id,
                Name = entity.Name,
                Key = rawKey,
                CreatedAt = entity.CreatedAt,
            };
        }

        public async Task<List<ApiKeyListItem>> ListAsync(Guid userId)
        {
            return await db.ApiKeys
                .Where(k => k.UserId == userId)
                .OrderByDescending(k => k.CreatedAt)
                .Select(k => new ApiKeyListItem
                {
                    Id = k.Id,
                    Name = k.Name,
                    Prefix = $"ak_{k.Prefix}_***",
                    CreatedAt = k.CreatedAt,
                    LastUsedAt = k.LastUsedAt,
                    IsRevoked = k.IsRevoked,
                })
                .ToListAsync();
        }

        public async Task<Result> RevokeAsync(Guid userId, Guid apiKeyId)
        {
            var key = await db.ApiKeys
                .FirstOrDefaultAsync(k => k.Id == apiKeyId && k.UserId == userId);

            if (key is null)
                return Result.Fail(AuthError.ApiKeyNotFound);

            key.IsRevoked = true;
            await db.SaveChangesAsync();
            return Result.Ok();
        }

        public async Task<Result<ExchangeApiKeyResponse>> ExchangeAsync(string rawKey)
        {
            // Parse: ak_<prefix>_<secret>
            if (!TryParseKey(rawKey, out var prefix, out var secret))
                return Result<ExchangeApiKeyResponse>.Fail(AuthError.InvalidApiKey);

            var secretHash = ComputeSha256(secret);

            var key = await db.ApiKeys
                .Include(k => k.User)
                .FirstOrDefaultAsync(k => k.Prefix == prefix && !k.IsRevoked);

            if (key is null || key.SecretHash != secretHash)
                return Result<ExchangeApiKeyResponse>.Fail(AuthError.InvalidApiKey);

            if (key.User.IsDeleted)
                return Result<ExchangeApiKeyResponse>.Fail(AuthError.UserDeleted);

            // Update last used
            key.LastUsedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();

            var accessToken = jwtService.GenerateAccessToken(key.UserId, new Claim("akid", key.Id.ToString()));
            var expiresIn = jwtOptions.Value.AccessTokenExpirationMinutes * 60;

            return Result<ExchangeApiKeyResponse>.Ok(new ExchangeApiKeyResponse
            {
                AccessToken = accessToken,
                ExpiresIn = expiresIn,
            });
        }

        private static bool TryParseKey(string rawKey, out string prefix, out string secret)
        {
            prefix = secret = string.Empty;

            if (string.IsNullOrWhiteSpace(rawKey))
                return false;

            // Format: ak_<prefix(8)>_<secret>
            // Use fixed-position parsing because prefix/secret may contain '_'
            const string header = "ak_";
            if (!rawKey.StartsWith(header))
                return false;

            // prefix starts at index 3, length 8
            if (rawKey.Length < header.Length + PrefixLength + 1) // +1 for separator '_'
                return false;

            prefix = rawKey.Substring(header.Length, PrefixLength);

            // Expect '_' separator after prefix
            if (rawKey[header.Length + PrefixLength] != '_')
                return false;

            secret = rawKey[(header.Length + PrefixLength + 1)..];
            return secret.Length > 0;
        }

        private static string ComputeSha256(string input)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexStringLower(bytes);
        }
    }
}