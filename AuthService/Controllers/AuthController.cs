using System.Security.Claims;
using AuthService.Data;
using AuthService.DTOs.Auth;
using AuthService.Entities;
using AuthService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
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
        IOAuthSecurityService oauthSecurity,
        IOptions<GithubOAuthOptions> githubOptions,
        IOptions<GoogleOAuthOptions> googleOptions,
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

        // ===================== GitHub OAuth =====================

        [HttpGet("github/login")]
        public IActionResult GithubLogin([FromQuery] string? redirectUrl, [FromQuery] string? token)
        {
            // Validate redirect URL against whitelist
            oauthSecurity.ValidateRedirectUrl(redirectUrl);

            // Parse binding userId from JWT (if provided)
            Guid? userId = null;
            if (!string.IsNullOrEmpty(token))
            {
                var userIdClaim = GetUserIdFromAuthHeader();
                userId = userIdClaim;
            }

            // Generate signed state (tamper-proof, with CSRF nonce and expiry)
            var state = oauthSecurity.GenerateState(redirectUrl, userId);

            var url = "https://github.com/login/oauth/authorize" +
                $"?client_id={_githubOptions.ClientId}" +
                $"&redirect_uri={Uri.EscapeDataString(_githubOptions.CallbackUrl)}" +
                "&scope=user:email" +
                $"&state={Uri.EscapeDataString(state)}";

            return Redirect(url);
        }

        [HttpGet("github/callback")]
        public async Task<IActionResult> GithubCallback([FromQuery] string code, [FromQuery] string? state)
        {
            return await HandleOAuthCallback(
                state,
                (ipAddress, device, currentUserId) => githubAuthService.LoginAsync(code, ipAddress, device, currentUserId));
        }

        // ===================== Google OAuth =====================

        [HttpGet("google/login")]
        public IActionResult GoogleLogin([FromQuery] string? redirectUrl, [FromQuery] string? token)
        {
            oauthSecurity.ValidateRedirectUrl(redirectUrl);

            Guid? userId = null;
            if (!string.IsNullOrEmpty(token))
            {
                userId = GetUserIdFromAuthHeader();
            }

            var state = oauthSecurity.GenerateState(redirectUrl, userId);

            var url = "https://accounts.google.com/o/oauth2/v2/auth" +
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
            return await HandleOAuthCallback(
                state,
                (ipAddress, device, currentUserId) => googleAuthService.LoginAsync(code, ipAddress, device, currentUserId));
        }

        // ===================== Auth Code Exchange =====================

        /// <summary>
        /// Exchange a one-time authorization code for tokens (POST, no tokens in URL).
        /// </summary>
        [HttpPost("exchange")]
        public IActionResult ExchangeCode([FromBody] ExchangeCodeRequest request)
        {
            var payload = oauthSecurity.ExchangeAuthCode(request.Code);
            if (payload == null)
                return BadRequest(new { message = "Invalid or expired authorization code." });

            return Ok(new AuthResponse
            {
                UserId = payload.UserId,
                AccessToken = payload.AccessToken,
                RefreshToken = payload.RefreshToken,
                ExpiresAt = payload.ExpiresAt
            });
        }

        // ===================== Account Management =====================

        [Authorize]
        [HttpPost("add-password")]
        public async Task<IActionResult> AddPassword([FromBody] AddPasswordRequest request)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!Guid.TryParse(userIdClaim, out var userId))
                    return Unauthorized();

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

        // ===================== Helpers =====================

        /// <summary>
        /// Unified OAuth callback handler. Validates signed state, performs login,
        /// generates a one-time auth code, and redirects with code (not token).
        /// </summary>
        private async Task<IActionResult> HandleOAuthCallback(
            string? state,
            Func<string, string, Guid?, Task<AuthResponse>> loginAction)
        {
            // Validate signed state
            OAuthStatePayload? statePayload = null;
            if (!string.IsNullOrEmpty(state))
            {
                statePayload = oauthSecurity.ValidateState(state);
                if (statePayload == null)
                    return BadRequest(new { message = "Invalid or expired OAuth state." });
            }

            string? redirectUrl = statePayload?.RedirectUrl;
            Guid? currentUserId = statePayload?.UserId;

            try
            {
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var device = Request.Headers.UserAgent.ToString();

                var response = await loginAction(ipAddress, device, currentUserId);

                if (!string.IsNullOrEmpty(redirectUrl))
                {
                    // Generate one-time auth code instead of passing tokens in URL
                    var authCode = oauthSecurity.GenerateAuthCode(
                        response.UserId, response.AccessToken, response.RefreshToken, response.ExpiresAt);

                    var finalUrl = QueryHelpers.AddQueryString(redirectUrl, "code", authCode);
                    return Redirect(finalUrl);
                }

                return Ok(response);
            }
            catch (InvalidOperationException ex)
            {
                if (!string.IsNullOrEmpty(redirectUrl))
                {
                    var finalUrl = QueryHelpers.AddQueryString(redirectUrl, "error", ex.Message);
                    return Redirect(finalUrl);
                }
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Extract user ID from the Authorization header JWT (for binding flows).
        /// </summary>
        private Guid? GetUserIdFromAuthHeader()
        {
            var authHeader = Request.Headers.Authorization.ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                return null;

            var token = authHeader["Bearer ".Length..];
            var jwtSvc = HttpContext.RequestServices.GetRequiredService<IJwtService>();
            return jwtSvc.ValidateTokenAndGetUserId(token);
        }
    }
}
