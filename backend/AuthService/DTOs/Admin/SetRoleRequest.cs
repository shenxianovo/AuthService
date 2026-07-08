namespace AuthService.DTOs.Admin
{
    public class SetRoleRequest
    {
        /// <summary>"User" or "Admin" (case-insensitive).</summary>
        public string Role { get; set; } = null!;
    }
}
