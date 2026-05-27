using System.Text.Json;

namespace AuthService.Middleware
{
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
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled exception");
                var message = context.RequestServices
                    .GetRequiredService<IHostEnvironment>()
                    .IsDevelopment()
                    ? ex.Message
                    : "An unexpected error occurred.";
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/json";
                var body = JsonSerializer.Serialize(new { message }, _jsonOptions);
                await context.Response.WriteAsync(body);
            }
        }
    }
}
