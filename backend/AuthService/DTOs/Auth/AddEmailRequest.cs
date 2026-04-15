using System.ComponentModel.DataAnnotations;

namespace AuthService.DTOs.Auth
{
    public class AddEmailRequest
    {
        [Required]
        [EmailAddress]
        [MaxLength(255)]
        public string Email { get; set; } = null!;
    }
}