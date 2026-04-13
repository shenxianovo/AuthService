using System.ComponentModel.DataAnnotations;

namespace AuthService.DTOs.Auth
{
    public class RefreshRequest
    {
        [Required]
        public string RefreshToken { get; set; } = null!;
    }
}
