using System.Net.Http.Headers;
using AuthService.Common;
using AuthService.DTOs.Auth;
using AuthService.DTOs.Auth.Github;
using AuthService.Entities;
using AuthService.Extensions;
using AuthService.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using OpenIddict.Abstractions;
using OpenIddict.Client.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Client.WebIntegration.OpenIddictClientWebIntegrationConstants;

namespace AuthService.Controllers
{
    /// <summary>
    /// Upstream GitHub/Google login via OpenIddict Client (ADR-018). The client
    /// stack owns state protection, correlation and code exchange; these actions
    /// only translate between it and the domain pipeline
    /// (ProcessOAuthLoginAsync → session → one-time auth code → SPA).
    /// </summary>
    [ApiController]
    [Route("api/v1/auth")]
    public class OAuthController(
        IOAuthService oauthService,
        ISessionService sessionService,
        IOAuthSecurityService oauthSecurity,
        IJwtService jwtService,
        IHttpClientFactory httpClientFactory) : ControllerBase
    {
        private const string RedirectUrlKey = "redirectUrl";
        private const string BindUserIdKey = "bindUserId";

        // ===================== Login entry points =====================

        [HttpGet("github/login")]
        public IActionResult GithubLogin([FromQuery] string? redirectUrl, [FromQuery] string? token)
            => StartOAuthLogin(Providers.GitHub, redirectUrl, token);

        [HttpGet("google/login")]
        public IActionResult GoogleLogin([FromQuery] string? redirectUrl, [FromQuery] string? token)
            => StartOAuthLogin(Providers.Google, redirectUrl, token);

        /// <summary>
        /// Validate the redirect URL, resolve an optional binding userId from the
        /// JWT query param, and challenge the OpenIddict client scheme. The
        /// properties round-trip inside the protected OAuth state parameter.
        /// </summary>
        private IActionResult StartOAuthLogin(string provider, string? redirectUrl, string? token)
        {
            var redirectValidation = oauthSecurity.ValidateRedirectUrl(redirectUrl);
            if (!redirectValidation.IsSuccess)
                return this.ToErrorResponse(redirectValidation.Error, redirectValidation.ErrorMessage);

            Guid? userId = null;
            if (!string.IsNullOrEmpty(token))
                userId = jwtService.ValidateTokenAndGetUserId(token);

            var properties = new AuthenticationProperties
            {
                Items =
                {
                    [OpenIddictClientAspNetCoreConstants.Properties.ProviderName] = provider,
                    [RedirectUrlKey] = redirectUrl,
                    [BindUserIdKey] = userId?.ToString(),
                },
            };

            return Challenge(properties, OpenIddictClientAspNetCoreDefaults.AuthenticationScheme);
        }

        // ===================== Provider callbacks =====================

        [HttpGet("github/callback"), HttpPost("github/callback")]
        public async Task<IActionResult> GithubCallback()
        {
            var result = await HttpContext.AuthenticateAsync(OpenIddictClientAspNetCoreDefaults.AuthenticationScheme);
            if (!result.Succeeded)
                return this.ToErrorResponse(AuthError.InvalidOAuthState);

            var principal = result.Principal;
            var providerUserId = principal.FindFirst(Claims.Subject)?.Value
                ?? principal.FindFirst("id")?.Value;
            if (providerUserId is null)
                return this.ToErrorResponse(AuthError.InvalidOAuthState);

            var login = principal.FindFirst("login")?.Value;

            // GET /user only carries the public profile email with no verification
            // status. The primary, verified address lives in /user/emails, which
            // is the authoritative source for both (ADR-012).
            var providerToken = result.Properties?.GetTokenValue(
                OpenIddictClientAspNetCoreConstants.Tokens.BackchannelAccessToken);
            var (email, emailVerified) = await FetchGithubPrimaryEmailAsync(providerToken);
            email ??= principal.FindFirst(Claims.Email)?.Value;

            return await CompleteAsync(
                AuthProviderType.Github,
                providerUserId,
                email,
                displayName: login ?? principal.FindFirst(Claims.Name)?.Value ?? providerUserId,
                providerLogin: login,
                emailVerified,
                result.Properties);
        }

        [HttpGet("google/callback"), HttpPost("google/callback")]
        public async Task<IActionResult> GoogleCallback()
        {
            var result = await HttpContext.AuthenticateAsync(OpenIddictClientAspNetCoreDefaults.AuthenticationScheme);
            if (!result.Succeeded)
                return this.ToErrorResponse(AuthError.InvalidOAuthState);

            // Google is a full OIDC provider: sub/name/email/email_verified come
            // from the validated id_token + userinfo, no extra calls needed.
            var principal = result.Principal;
            var providerUserId = principal.FindFirst(Claims.Subject)?.Value;
            if (providerUserId is null)
                return this.ToErrorResponse(AuthError.InvalidOAuthState);

            var email = principal.FindFirst(Claims.Email)?.Value;
            var emailVerified = string.Equals(
                principal.FindFirst(Claims.EmailVerified)?.Value, "true", StringComparison.OrdinalIgnoreCase);

            return await CompleteAsync(
                AuthProviderType.Google,
                providerUserId,
                email,
                displayName: principal.FindFirst(Claims.Name)?.Value ?? email ?? providerUserId,
                providerLogin: null,
                emailVerified,
                result.Properties);
        }

        // ===================== Shared completion =====================

        /// <summary>
        /// Unchanged domain pipeline: upsert/link/merge the account, create a
        /// session, then hand off to the SPA with a one-time auth code (tokens
        /// never appear in URLs).
        /// </summary>
        private async Task<IActionResult> CompleteAsync(
            AuthProviderType provider,
            string providerUserId,
            string? email,
            string displayName,
            string? providerLogin,
            bool emailVerified,
            AuthenticationProperties? properties)
        {
            string? redirectUrl = null;
            Guid? bindUserId = null;
            if (properties is not null)
            {
                properties.Items.TryGetValue(RedirectUrlKey, out redirectUrl);
                if (properties.Items.TryGetValue(BindUserIdKey, out var rawUserId)
                    && Guid.TryParse(rawUserId, out var parsed))
                    bindUserId = parsed;
            }

            var (ipAddress, device) = this.GetClientContext();

            var userResult = await oauthService.ProcessOAuthLoginAsync(
                provider, providerUserId, email, displayName, bindUserId, providerLogin, emailVerified);

            var result = userResult.IsSuccess
                ? await sessionService.CreateSessionAsync(userResult.Value, ipAddress, device)
                : Result<AuthResponse>.Fail(userResult.Error, userResult.ErrorMessage);

            if (!result.IsSuccess)
            {
                if (!string.IsNullOrEmpty(redirectUrl))
                {
                    var errorMsg = result.ErrorMessage ?? result.Error.ToString();
                    return Redirect(QueryHelpers.AddQueryString(redirectUrl, "error", errorMsg));
                }
                return this.ToErrorResponse(result.Error, result.ErrorMessage);
            }

            if (!string.IsNullOrEmpty(redirectUrl))
            {
                var authCode = oauthSecurity.GenerateAuthCode(
                    result.Value.UserId, result.Value.AccessToken,
                    result.Value.RefreshToken, result.Value.ExpiresAt);
                return Redirect(QueryHelpers.AddQueryString(redirectUrl, "code", authCode));
            }

            return Ok(result.Value);
        }

        private async Task<(string? Email, bool Verified)> FetchGithubPrimaryEmailAsync(string? accessToken)
        {
            if (string.IsNullOrEmpty(accessToken))
                return (null, false);

            var http = httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user/emails");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("AuthService", "1.0"));

            using var response = await http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return (null, false);

            var emails = await response.Content.ReadFromJsonAsync<List<GithubEmail>>();
            var primary = emails?.FirstOrDefault(e => e.Primary);
            return (primary?.Email, primary?.Verified ?? false);
        }
    }
}
