using System.ComponentModel.DataAnnotations;

namespace AuthService.DTOs.Auth
{
    public class ExchangeCodeRequest
    {
        [Required]
        public string Code { get; set; } = null!;
    }
}