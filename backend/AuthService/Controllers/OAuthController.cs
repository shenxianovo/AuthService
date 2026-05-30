using AuthService.Common;
using AuthService.Configuration;
using AuthService.DTOs.Auth;
using AuthService.Extensions;
using AuthService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace AuthService.Controllers
{
    [ApiController]
    [Route("api/v1/auth")]
    [Produces("application/json")]
    public class OAuthController(
        IGithubAuthService githubAuthService,
        IGoogleAuthService googleAuthService,
        IOAuthSecurityService oauthSecurity,
        IJwtService jwtService,
        IOptions<GithubOAuthOptions> githubOptions,
        IOptions<GoogleOAuthOptions> googleOptions) : ControllerBase
    {
        private readonly GithubOAuthOptions _githubOptions = githubOptions.Value;
        private readonly GoogleOAuthOptions _googleOptions = googleOptions.Value;

        // ===================== GitHub OAuth =====================

        [HttpGet("github/login")]
        public IActionResult GithubLogin([FromQuery] string? redirectUrl, [FromQuery] string? token)
            => StartOAuthLogin(redirectUrl, token, state =>
                "https://github.com/login/oauth/authorize" +
                $"?client_id={_githubOptions.ClientId}" +
                $"&redirect_uri={Uri.EscapeDataString(_githubOptions.CallbackUrl)}" +
                "&scope=user:email" +
                $"&state={Uri.EscapeDataString(state)}");

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
            => StartOAuthLogin(redirectUrl, token, state =>
                "https://accounts.google.com/o/oauth2/v2/auth" +
                $"?client_id={_googleOptions.ClientId}" +
                $"&redirect_uri={Uri.EscapeDataString(_googleOptions.CallbackUrl)}" +
                "&response_type=code" +
                "&scope=openid%20email%20profile" +
                $"&state={Uri.EscapeDataString(state)}");

        [HttpGet("google/callback")]
        public async Task<IActionResult> GoogleCallback([FromQuery] string code, [FromQuery] string? state)
        {
            return await HandleOAuthCallback(
                state,
                (ipAddress, device, currentUserId) => googleAuthService.LoginAsync(code, ipAddress, device, currentUserId));
        }

        // ===================== Helpers =====================

        /// <summary>
        /// Shared login entry: validate the redirect URL, resolve an optional binding
        /// userId from the JWT query param, mint signed state, and redirect to the
        /// provider's authorize URL (built by <paramref name="buildAuthorizeUrl"/>).
        /// </summary>
        private IActionResult StartOAuthLogin(string? redirectUrl, string? token, Func<string, string> buildAuthorizeUrl)
        {
            var redirectValidation = oauthSecurity.ValidateRedirectUrl(redirectUrl);
            if (!redirectValidation.IsSuccess)
                return this.ToErrorResponse(redirectValidation.Error, redirectValidation.ErrorMessage);

            Guid? userId = null;
            if (!string.IsNullOrEmpty(token))
                userId = jwtService.ValidateTokenAndGetUserId(token);

            var state = oauthSecurity.GenerateState(redirectUrl, userId);
            return Redirect(buildAuthorizeUrl(state));
        }

        /// <summary>
        /// Unified OAuth callback handler. Validates signed state, performs login,
        /// generates a one-time auth code, and redirects with code (not token).
        /// </summary>
        private async Task<IActionResult> HandleOAuthCallback(
            string? state,
            Func<string, string, Guid?, Task<Result<AuthResponse>>> loginAction)
        {
            // Validate signed state
            OAuthStatePayload? statePayload = null;
            if (!string.IsNullOrEmpty(state))
            {
                statePayload = oauthSecurity.ValidateState(state);
                if (statePayload == null)
                    return this.ToErrorResponse(AuthError.InvalidOAuthState);
            }

            string? redirectUrl = statePayload?.RedirectUrl;
            Guid? currentUserId = statePayload?.UserId;

            var (ipAddress, device) = this.GetClientContext();

            var result = await loginAction(ipAddress, device, currentUserId);

            if (!result.IsSuccess)
            {
                if (!string.IsNullOrEmpty(redirectUrl))
                {
                    var errorMsg = result.ErrorMessage ?? result.Error.ToString();
                    var finalUrl = QueryHelpers.AddQueryString(redirectUrl, "error", errorMsg);
                    return Redirect(finalUrl);
                }
                return this.ToErrorResponse(result.Error, result.ErrorMessage);
            }

            if (!string.IsNullOrEmpty(redirectUrl))
            {
                var authCode = oauthSecurity.GenerateAuthCode(
                    result.Value.UserId, result.Value.AccessToken,
                    result.Value.RefreshToken, result.Value.ExpiresAt);

                var finalUrl = QueryHelpers.AddQueryString(redirectUrl, "code", authCode);
                return Redirect(finalUrl);
            }

            return Ok(result.Value);
        }

    }
}
