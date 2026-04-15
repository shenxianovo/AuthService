namespace AuthService.Exceptions
{
    /// <summary>
    /// Represents a generic business rule violation. Maps to HTTP 400 Bad Request.
    /// </summary>
    public class BusinessException(string message) : Exception(message);

    /// <summary>
    /// Represents a conflict with existing state (e.g. duplicate resource).
    /// Maps to HTTP 409 Conflict.
    /// </summary>
    public class ConflictException(string message) : Exception(message);

    /// <summary>
    /// Represents an authentication failure or missing/invalid credentials.
    /// Maps to HTTP 401 Unauthorized.
    /// </summary>
    public class UnauthorizedException(string message) : Exception(message);
}
