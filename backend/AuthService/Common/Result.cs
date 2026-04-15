namespace AuthService.Common
{
    /// <summary>
    /// Non-generic Result for void operations (no return value on success).
    /// </summary>
    public sealed class Result
    {
        public bool IsSuccess { get; }
        public AuthError Error { get; }
        public string? ErrorMessage { get; }

        private Result() { IsSuccess = true; Error = AuthError.None; }
        private Result(AuthError error, string? message = null)
        {
            IsSuccess = false;
            Error = error;
            ErrorMessage = message;
        }

        public static Result Ok() => new();
        public static Result Fail(AuthError error, string? message = null) => new(error, message);
    }

    /// <summary>
    /// Discriminated union representing either a successful value or a typed error.
    /// Use Result.Ok(value) / Result.Fail(error) factory methods.
    /// </summary>
    public sealed class Result<T>
    {
        public bool IsSuccess { get; }
        public T Value { get; }
        public AuthError Error { get; }
        public string? ErrorMessage { get; }

        private Result(T value)
        {
            IsSuccess = true;
            Value = value;
            Error = AuthError.None;
        }

        private Result(AuthError error, string? message = null)
        {
            IsSuccess = false;
            Value = default!;
            Error = error;
            ErrorMessage = message;
        }

        public static Result<T> Ok(T value) => new(value);
        public static Result<T> Fail(AuthError error, string? message = null) => new(error, message);

        /// <summary>Map a successful value; propagate failure unchanged.</summary>
        public Result<TOut> Map<TOut>(Func<T, TOut> mapper) =>
            IsSuccess ? Result<TOut>.Ok(mapper(Value)) : Result<TOut>.Fail(Error, ErrorMessage);
    }

    /// <summary>
    /// Typed error codes for all business-level failures in AuthService.
    /// Each code maps to a specific HTTP status code in the Controller layer.
    /// </summary>
    public enum AuthError
    {
        None = 0,

        // 409 Conflict
        EmailAlreadyExists,

        // 400 Bad Request
        UserNotFound,
        PasswordAlreadySet,
        InvalidAuthCode,
        InvalidOAuthState,
        InvalidRedirectUrl,

        // 401 Unauthorized
        InvalidCredentials,
        InvalidRefreshToken,
        UserDeleted,
        UserNotFoundForMerge,
    }
}
