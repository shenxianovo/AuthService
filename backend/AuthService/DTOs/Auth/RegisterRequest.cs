using System.ComponentModel.DataAnnotations;

namespace AuthService.DTOs.Auth
{
    public class RegisterRequest
    {
        [Required]
        [MinLength(3)]
        [MaxLength(39)]
        public string Username { get; set; } = null!;

        [Required]
        [MaxLength(128)]
        public string DisplayName { get; set; } = null!;

        [Required]
        [EmailAddress]
        [MaxLength(255)]
        public string Email { get; set; } = null!;

        [Required]
        [MinLength(8)]
        [MaxLength(128)]
        public string Password { get; set; } = null!;
    }
}
