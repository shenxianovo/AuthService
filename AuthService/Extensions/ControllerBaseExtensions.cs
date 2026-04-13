using AuthService.Common;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.Extensions
{
    public static class ControllerBaseExtensions
    {
        /// <summary>
        /// Convert a Result failure to the appropriate IActionResult.
        /// Centralises the AuthError → HTTP status code mapping.
        /// </summary>
        public static IActionResult ToErrorResponse(this ControllerBase controller, AuthError error, string? message = null)
        {
            return error switch
            {
                AuthError.EmailAlreadyExists =>
                    controller.Conflict(new { message = message ?? "Email already registered." }),

                AuthError.UserNotFound =>
                    controller.BadRequest(new { message = message ?? "User not found." }),

                AuthError.PasswordAlreadySet =>
                    controller.BadRequest(new { message = message ?? "User already has a password." }),

                AuthError.InvalidAuthCode =>
                    controller.BadRequest(new { message = message ?? "Invalid or expired authorization code." }),

                AuthError.InvalidOAuthState =>
                    controller.BadRequest(new { message = message ?? "Invalid or expired OAuth state." }),

                AuthError.InvalidCredentials =>
                    controller.Unauthorized(new { message = message ?? "Invalid credentials." }),

                AuthError.InvalidRefreshToken =>
                    controller.Unauthorized(new { message = message ?? "Invalid or expired refresh token." }),

                AuthError.UserDeleted =>
                    controller.Unauthorized(new { message = message ?? "User account has been deleted." }),

                AuthError.UserNotFoundForMerge =>
                    controller.Unauthorized(new { message = message ?? "User not found." }),

                _ => controller.BadRequest(new { message = message ?? "An error occurred." })
            };
        }
    }
}
