using System.ComponentModel.DataAnnotations;

namespace AuthService.DTOs.Auth
{
    public class AddPasswordRequest
    {
        [Required]
        [MinLength(8)]
        [MaxLength(128)]
        public string Password { get; set; } = null!;
    }
}