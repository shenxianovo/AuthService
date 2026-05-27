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

                AuthError.UsernameAlreadyExists =>
                    controller.Conflict(new { message = message ?? "Username already taken." }),

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

                AuthError.InvalidUsername =>
                    controller.BadRequest(new { message = message ?? "Username format is invalid or reserved." }),

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

                AuthError.ApiKeyNotFound =>
                    controller.NotFound(new { message = message ?? "API key not found." }),

                AuthError.InvalidApiKey =>
                    controller.Unauthorized(new { message = message ?? "Invalid API key." }),

                AuthError.EmailNotFound =>
                    controller.BadRequest(new { message = message ?? "Email not found." }),

                AuthError.CannotRemovePrimaryEmail =>
                    controller.BadRequest(new { message = message ?? "Cannot remove primary email. Set another email as primary first." }),

                AuthError.EmailNotVerified =>
                    controller.BadRequest(new { message = message ?? "Email is not verified." }),

                AuthError.EmailAlreadyVerified =>
                    controller.BadRequest(new { message = message ?? "Email is already verified." }),

                AuthError.InvalidVerificationCode =>
                    controller.BadRequest(new { message = message ?? "Invalid or expired verification code." }),

                AuthError.VerificationRateLimited =>
                    new ObjectResult(new { message = message ?? "Please try again later." }) { StatusCode = StatusCodes.Status429TooManyRequests },

                _ => controller.BadRequest(new { message = message ?? "An error occurred." })
            };
        }
    }
}
