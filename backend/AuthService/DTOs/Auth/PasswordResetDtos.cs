using System.ComponentModel.DataAnnotations;

namespace AuthService.DTOs.Auth
{
    public class ForgotPasswordRequest
    {
        [Required]
        [EmailAddress]
        [MaxLength(255)]
        public string Email { get; set; } = null!;
    }

    public class ResetPasswordRequest
    {
        [Required]
        [MaxLength(128)]
        public string Token { get; set; } = null!;

        [Required]
        [MinLength(8)]
        [MaxLength(128)]
        public string NewPassword { get; set; } = null!;
    }

    public class ChangePasswordRequest
    {
        [Required]
        [MaxLength(128)]
        public string CurrentPassword { get; set; } = null!;

        [Required]
        [MinLength(8)]
        [MaxLength(128)]
        public string NewPassword { get; set; } = null!;
    }
}
