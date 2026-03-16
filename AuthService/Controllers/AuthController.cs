using System.Security.Claims;
using AuthService.DTOs.Auth;
using AuthService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AuthService.Controllers
{
    [ApiController]
    [Route("api/v1/auth")]
    public class AuthController(
        IPasswordAuthService passwordAuthService,
        IGithubAuthService githubAuthService,
        IOptions<GithubOAuthOptions> githubOptions,
        IJwtService jwtService) : ControllerBase
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
        public async Task<IActionResult> GithubLogin([FromQuery] string? redirectUrl, [FromQuery] string? token)
        {
            var stateObj = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(redirectUrl)) stateObj["redirectUrl"] = redirectUrl;
            if (!string.IsNullOrEmpty(token)) stateObj["token"] = token; // MVP approach: pass token in state to bind
            
            var state = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(stateObj)));

            var url =
                "https://github.com/login/oauth/authorize" +
                $"?client_id={_githubOptions.ClientId}" +
                $"&redirect_uri={Uri.EscapeDataString(_githubOptions.CallbackUrl)}" +
                "&scope=user:email" +
                $"&state={Uri.EscapeDataString(state)}";

            return Redirect(url);
        }

        [HttpGet("github/callback")]
        public async Task<IActionResult> GithubCallback([FromQuery] string code, [FromQuery] string? state)
        {
            try
            {
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var device = Request.Headers.UserAgent.ToString();

                string? redirectUrl = null;
                Guid? currentUserId = null;

                if (!string.IsNullOrEmpty(state))
                {
                    try
                    {
                        var stateJson = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(state));
                        var stateObj = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(stateJson);
                        if (stateObj != null)
                        {
                            if (stateObj.TryGetValue("redirectUrl", out var r)) redirectUrl = r;
                            if (stateObj.TryGetValue("token", out var t)) currentUserId = jwtService.ValidateTokenAndGetUserId(t);
                        }
                    }
                    catch
                    {
                        // ignore state parsing error
                    }
                }

                var response = await githubAuthService.LoginAsync(code, ipAddress, device, currentUserId);

                if (!string.IsNullOrEmpty(redirectUrl))
                {
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
                string? redirectUrl = null;
                if (!string.IsNullOrEmpty(state))
                {
                    try
                    {
                        var stateJson = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(state));
                        var stateObj = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(stateJson);
                        if (stateObj != null && stateObj.TryGetValue("redirectUrl", out var r)) redirectUrl = r;
                    }
                    catch { }
                }

                if (!string.IsNullOrEmpty(redirectUrl))
                {
                    var finalUrl = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(redirectUrl, "error", ex.Message);
                    return Redirect(finalUrl);
                }
                return BadRequest(new { message = ex.Message });
            }
        }
        [Authorize]
        [HttpPost("add-password")]
        public async Task<IActionResult> AddPassword([FromBody] AddPasswordRequest request)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!Guid.TryParse(userIdClaim, out var userId))
                {
                    return Unauthorized();
                }

                await passwordAuthService.AddPasswordAsync(userId, request.Password);
                return Ok(new { message = "Password added successfully." });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
