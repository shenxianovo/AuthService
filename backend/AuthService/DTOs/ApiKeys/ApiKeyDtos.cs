using System.ComponentModel.DataAnnotations;

namespace AuthService.DTOs.ApiKeys
{
    public class CreateApiKeyRequest
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = null!;
    }

    /// <summary>
    /// Returned only once at creation time; contains the full raw key.
    /// </summary>
    public class CreateApiKeyResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public string Key { get; set; } = null!;
        public DateTimeOffset CreatedAt { get; set; }
    }

    /// <summary>
    /// List item — the raw key is never shown again.
    /// </summary>
    public class ApiKeyListItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public string Prefix { get; set; } = null!;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? LastUsedAt { get; set; }
        public bool IsRevoked { get; set; }
    }

    public class ExchangeApiKeyRequest
    {
        [Required]
        public string ApiKey { get; set; } = null!;
    }

    public class ExchangeApiKeyResponse
    {
        public string AccessToken { get; set; } = null!;
        public long ExpiresIn { get; set; }
    }
}