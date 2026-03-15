using AuthService.DTOs.Auth;
using AuthService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AuthService.Controllers
{
    [ApiController]
    [Route("api/v1/auth")]
    public class AuthController(
        IPasswordAuthService passwordAuthService,
        IGithubAuthService githubAuthService,
        IOptions<GithubOAuthOptions> githubOptions) : ControllerBase
    {
        private readonly GithubOAuthOptions _githubOptions = githubOptions.Value;

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            try
            {
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var device = Request.Headers.UserAgent.ToString();

                var response = await passwordAuthService.RegisterAsync(request, ipAddress, device);
                return Ok(response);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var device = Request.Headers.UserAgent.ToString();

                var response = await passwordAuthService.LoginAsync(request, ipAddress, device);
                return Ok(response);
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new { message = "Invalid email or password." });
            }
        }

        [HttpGet("github/login")]
        public async Task<IActionResult> GithubLogin([FromQuery] string? redirectUrl)
        {
            var state = string.IsNullOrEmpty(redirectUrl) ? "" : Uri.EscapeDataString(redirectUrl);

            var url =
                "https://github.com/login/oauth/authorize" +
                $"?client_id={_githubOptions.ClientId}" +
                $"&redirect_uri={Uri.EscapeDataString(_githubOptions.CallbackUrl)}" +
                "&scope=user:email" +
                (string.IsNullOrEmpty(state) ? "" : $"&state={state}");

            return Redirect(url);
        }

        [HttpGet("github/callback")]
        public async Task<IActionResult> GithubCallback([FromQuery] string code, [FromQuery] string? state)
        {
            try
            {
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var device = Request.Headers.UserAgent.ToString();

                var response = await githubAuthService.LoginAsync(code, ipAddress, device);

                if (!string.IsNullOrEmpty(state))
                {
                    var redirectUrl = Uri.UnescapeDataString(state);
                    var queryParameters = new Dictionary<string, string?>
                    {
                        { "token", response.AccessToken },
                        { "userId", response.UserId.ToString() }
                    };
                    var finalUrl = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(redirectUrl, queryParameters);
                    return Redirect(finalUrl);
                }

                return Ok(response);
            }
            catch (InvalidOperationException ex)
            {
                if (!string.IsNullOrEmpty(state))
                {
                    var redirectUrl = Uri.UnescapeDataString(state);
                    var finalUrl = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(redirectUrl, "error", ex.Message);
                    return Redirect(finalUrl);
                }
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
