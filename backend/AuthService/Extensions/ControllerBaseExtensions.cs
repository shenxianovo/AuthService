using System.Collections.Frozen;
using AuthService.Common;
using AuthService.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.Extensions
{
    public static class ControllerBaseExtensions
    {
        /// <summary>
        /// Convert a Result failure to the appropriate IActionResult, using the
        /// central AuthError → HTTP status/message map in <see cref="AuthErrorHttp"/>.
        /// The body carries the stable error code alongside the English message so
        /// clients can localize (see ADR-021).
        /// </summary>
        public static IActionResult ToErrorResponse(this ControllerBase controller, AuthError error, string? message = null)
        {
            var (status, defaultMessage) = AuthErrorHttp.Resolve(error);
            return new ObjectResult(new ErrorResponse(error, message ?? defaultMessage)) { StatusCode = status };
        }
    }

    /// <summary>
    /// Single source of truth mapping each <see cref="AuthError"/> to its HTTP status
    /// and default message. Adding an AuthError without an entry here is caught by
    /// AuthErrorHttpMappingTests, so the mapping cannot silently drift.
    /// </summary>
    public static class AuthErrorHttp
    {
        private static readonly FrozenDictionary<AuthError, (int Status, string Message)> Map =
            new Dictionary<AuthError, (int, string)>
            {
                [AuthError.EmailAlreadyExists] = (StatusCodes.Status409Conflict, "Email already registered."),
                [AuthError.UsernameAlreadyExists] = (StatusCodes.Status409Conflict, "Username already taken."),

                [AuthError.UserNotFound] = (StatusCodes.Status400BadRequest, "User not found."),
                [AuthError.PasswordAlreadySet] = (StatusCodes.Status400BadRequest, "User already has a password."),
                [AuthError.InvalidAuthCode] = (StatusCodes.Status400BadRequest, "Invalid or expired authorization code."),
                [AuthError.InvalidOAuthState] = (StatusCodes.Status400BadRequest, "Invalid or expired OAuth state."),
                [AuthError.InvalidRedirectUrl] = (StatusCodes.Status400BadRequest, "Redirect URL is not allowed."),
                [AuthError.InvalidUsername] = (StatusCodes.Status400BadRequest, "Username format is invalid or reserved."),

                [AuthError.InvalidCredentials] = (StatusCodes.Status401Unauthorized, "Invalid credentials."),
                [AuthError.InvalidRefreshToken] = (StatusCodes.Status401Unauthorized, "Invalid or expired refresh token."),
                [AuthError.UserNotFoundForMerge] = (StatusCodes.Status401Unauthorized, "User not found."),
                [AuthError.InvalidResetToken] = (StatusCodes.Status400BadRequest, "Invalid or expired reset token."),
                [AuthError.PasswordNotSet] = (StatusCodes.Status400BadRequest, "No password set on this account. Add one first."),

                [AuthError.CannotUnlinkLastLoginMethod] = (StatusCodes.Status400BadRequest, "Cannot unlink the last login method."),
                [AuthError.ProviderNotLinked] = (StatusCodes.Status400BadRequest, "Provider is not linked to this account."),

                [AuthError.ApiKeyNotFound] = (StatusCodes.Status404NotFound, "API key not found."),
                [AuthError.InvalidApiKey] = (StatusCodes.Status401Unauthorized, "Invalid API key."),

                [AuthError.EmailNotFound] = (StatusCodes.Status400BadRequest, "Email not found."),
                [AuthError.CannotRemovePrimaryEmail] = (StatusCodes.Status400BadRequest, "Cannot remove primary email. Set another email as primary first."),
                [AuthError.EmailNotVerified] = (StatusCodes.Status400BadRequest, "Email is not verified."),
                [AuthError.EmailAlreadyVerified] = (StatusCodes.Status400BadRequest, "Email is already verified."),
                [AuthError.InvalidVerificationCode] = (StatusCodes.Status400BadRequest, "Invalid or expired verification code."),

                [AuthError.VerificationRateLimited] = (StatusCodes.Status429TooManyRequests, "Please try again later."),

                [AuthError.CannotDemoteLastAdmin] = (StatusCodes.Status400BadRequest, "Cannot demote the last admin."),
                [AuthError.OidcClientAlreadyExists] = (StatusCodes.Status409Conflict, "An OIDC client with this id already exists."),
                [AuthError.OidcClientNotFound] = (StatusCodes.Status404NotFound, "OIDC client not found."),
                [AuthError.InvalidOidcClient] = (StatusCodes.Status400BadRequest, "Invalid OIDC client definition."),
            }.ToFrozenDictionary();

        /// <summary>All mapped errors — used by the guard test to assert completeness.</summary>
        public static IReadOnlyDictionary<AuthError, (int Status, string Message)> Entries => Map;

        public static (int Status, string Message) Resolve(AuthError error) =>
            Map.TryGetValue(error, out var entry)
                ? entry
                : (StatusCodes.Status400BadRequest, "An error occurred.");
    }
}
