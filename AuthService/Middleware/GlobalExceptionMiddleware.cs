using AuthService.Exceptions;
using System.Text.Json;

namespace AuthService.Middleware
{
    /// <summary>
    /// Catches unhandled exceptions and converts them to consistent JSON error responses.
    /// 
    /// Mapping:
    ///   ConflictException           → 409 Conflict
    ///   BusinessException           → 400 Bad Request
    ///   UnauthorizedAccessException → 401 Unauthorized
    ///   Everything else             → 500 Internal Server Error (message hidden in production)
    /// </summary>
    public class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await next(context);
            }
            catch (ConflictException ex)
            {
                logger.LogInformation(ex, "Conflict: {Message}", ex.Message);
                await WriteErrorAsync(context, StatusCodes.Status409Conflict, ex.Message);
            }
            catch (BusinessException ex)
            {
                logger.LogInformation(ex, "Business rule violation: {Message}", ex.Message);
                await WriteErrorAsync(context, StatusCodes.Status400BadRequest, ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                logger.LogInformation(ex, "Unauthorized: {Message}", ex.Message);
                await WriteErrorAsync(context, StatusCodes.Status401Unauthorized, ex.Message);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled exception");
                var message = context.RequestServices
                    .GetRequiredService<IHostEnvironment>()
                    .IsDevelopment()
                    ? ex.Message
                    : "An unexpected error occurred.";
                await WriteErrorAsync(context, StatusCodes.Status500InternalServerError, message);
            }
        }

        private static async Task WriteErrorAsync(HttpContext context, int statusCode, string message)
        {
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";
            var body = JsonSerializer.Serialize(new { message }, _jsonOptions);
            await context.Response.WriteAsync(body);
        }
    }
}
