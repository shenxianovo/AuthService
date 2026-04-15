using System.ComponentModel.DataAnnotations;

namespace AuthService.DTOs.Auth
{
    public class LoginRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = null!;

        [Required]
        [MinLength(8)]
        [MaxLength(128)]
        public string Password { get; set; } = null!;
    }
}
