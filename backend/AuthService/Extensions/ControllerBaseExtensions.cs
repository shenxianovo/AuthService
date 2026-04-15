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

                AuthError.InvalidRedirectUrl =>
                    controller.BadRequest(new { message = message ?? "Redirect URL is not allowed." }),

                AuthError.InvalidCredentials =>
                    controller.Unauthorized(new { message = message ?? "Invalid credentials." }),

                AuthError.InvalidRefreshToken =>
                    controller.Unauthorized(new { message = message ?? "Invalid or expired refresh token." }),

                AuthError.UserDeleted =>
                    controller.Unauthorized(new { message = message ?? "User account has been deleted." }),

                AuthError.UserNotFoundForMerge =>
                    controller.Unauthorized(new { message = message ?? "User not found." }),

                AuthError.CannotUnlinkLastLoginMethod =>
                    controller.BadRequest(new { message = message ?? "Cannot unlink the last login method." }),

                AuthError.ProviderNotLinked =>
                    controller.BadRequest(new { message = message ?? "Provider is not linked to this account." }),

                _ => controller.BadRequest(new { message = message ?? "An error occurred." })
            };
        }
    }
}
