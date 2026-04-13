using AuthService.DTOs.Auth;
using AuthService.Configuration;
using AuthService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace AuthService.Controllers
{
    [ApiController]
    [Route("api/v1/auth")]
    public class OAuthController(
        IGithubAuthService githubAuthService,
        IGoogleAuthService googleAuthService,
        IOAuthSecurityService oauthSecurity,
        IOptions<GithubOAuthOptions> githubOptions,
        IOptions<GoogleOAuthOptions> googleOptions) : ControllerBase
    {
        private readonly GithubOAuthOptions _githubOptions = githubOptions.Value;
        private readonly GoogleOAuthOptions _googleOptions = googleOptions.Value;

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
                userId = GetUserIdFromAuthHeader();
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
