using AuthService.Common;

namespace AuthService.DTOs
{
    /// <summary>
    /// Body of every non-2xx response produced via ToErrorResponse. <see cref="Code"/>
    /// is the stable machine-readable AuthError name (part of the API contract, used by
    /// the SPA to localize error text); <see cref="Message"/> is the English default.
    /// </summary>
    public record ErrorResponse(AuthError Code, string Message);
}
