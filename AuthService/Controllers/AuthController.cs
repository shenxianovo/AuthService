using System.Security.Claims;
using AuthService.Data;
using AuthService.DTOs.Auth;
using AuthService.Entities;
using AuthService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AuthService.Controllers
{
    [ApiController]
    [Route("api/v1/auth")]
    public class AuthController(
        IPasswordAuthService passwordAuthService,
        IGithubAuthService githubAuthService,
        IGoogleAuthService googleAuthService,
        IOptions<GithubOAuthOptions> githubOptions,
        IOptions<GoogleOAuthOptions> googleOptions,
        IJwtService jwtService,
        AppDbContext db) : ControllerBase
    {
        private readonly GithubOAuthOptions _githubOptions = githubOptions.Value;
        private readonly GoogleOAuthOptions _googleOptions = googleOptions.Value;

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
        [HttpGet("google/login")]
        public async Task<IActionResult> GoogleLogin([FromQuery] string? redirectUrl, [FromQuery] string? token)
        {
            var stateObj = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(redirectUrl)) stateObj["redirectUrl"] = redirectUrl;
            if (!string.IsNullOrEmpty(token)) stateObj["token"] = token;

            var state = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(stateObj)));

            var url =
                "https://accounts.google.com/o/oauth2/v2/auth" +
                $"?client_id={_googleOptions.ClientId}" +
                $"&redirect_uri={Uri.EscapeDataString(_googleOptions.CallbackUrl)}" +
                "&response_type=code" +
                "&scope=openid%20email%20profile" +
                $"&state={Uri.EscapeDataString(state)}";

            return Redirect(url);
        }

        [HttpGet("google/callback")]
        public async Task<IActionResult> GoogleCallback([FromQuery] string code, [FromQuery] string? state)
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

                var response = await googleAuthService.LoginAsync(code, ipAddress, device, currentUserId);

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

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> GetMe()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized();

            var user = await db.Users
                .Include(u => u.Emails)
                .Include(u => u.Providers)
                .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);

            if (user == null)
                return NotFound();

            var hasPassword = await db.PasswordCredentials.AnyAsync(p => p.UserId == userId);

            return Ok(new UserInfoResponse
            {
                UserId = user.Id,
                DisplayName = user.DisplayName,
                CreatedAt = user.CreatedAt,
                HasPassword = hasPassword,
                Emails = user.Emails.Select(e => new EmailInfo
                {
                    Email = e.Email,
                    IsPrimary = e.IsPrimary,
                    IsVerified = e.VerifiedAt.HasValue
                }).ToList(),
                Providers = user.Providers
                    .Where(p => p.Provider != AuthProviderType.Password)
                    .Select(p => new ProviderInfo
                    {
                        Provider = p.Provider.ToString(),
                        LinkedAt = p.CreatedAt
                    }).ToList()
            });
        }
    }
}
