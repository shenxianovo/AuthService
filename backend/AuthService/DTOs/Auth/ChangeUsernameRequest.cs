using System.ComponentModel.DataAnnotations;

namespace AuthService.DTOs.Auth
{
    public class ChangeUsernameRequest
    {
        [Required]
        [MinLength(3)]
        [MaxLength(39)]
        public string Username { get; set; } = null!;
    }
}
