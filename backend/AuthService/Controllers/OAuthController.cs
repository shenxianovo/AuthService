using System.Net.Http.Headers;
using AuthService.Common;
using AuthService.Data;
using AuthService.DTOs.Auth;
using AuthService.DTOs.Auth.Github;
using AuthService.Entities;
using AuthService.Extensions;
using AuthService.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
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
    /// Binding a provider to the logged-in account is the interactive flow
    /// POST /connect/bind/{provider} (ADR-019).
    /// </summary>
    [ApiController]
    [Route("api/v1/auth")]
    public class OAuthController(
        IOAuthService oauthService,
        ISessionService sessionService,
        IOAuthSecurityService oauthSecurity,
        AppDbContext db,
        IHttpClientFactory httpClientFactory) : ControllerBase
    {
        private const string RedirectUrlKey = "redirectUrl";
        private const string BindUserIdKey = "bindUserId";

        // ===================== Login entry points =====================

        [HttpGet("github/login")]
        public IActionResult GithubLogin([FromQuery] string? redirectUrl)
            => StartOAuthLogin(Providers.GitHub, redirectUrl);

        [HttpGet("google/login")]
        public IActionResult GoogleLogin([FromQuery] string? redirectUrl)
            => StartOAuthLogin(Providers.Google, redirectUrl);

        /// <summary>
        /// Validate the redirect URL and challenge the OpenIddict client scheme.
        /// The properties round-trip inside the protected OAuth state parameter.
        /// </summary>
        private IActionResult StartOAuthLogin(string provider, string? redirectUrl)
        {
            var redirectValidation = oauthSecurity.ValidateRedirectUrl(redirectUrl);
            if (!redirectValidation.IsSuccess)
                return this.ToErrorResponse(redirectValidation.Error, redirectValidation.ErrorMessage);

            return Challenge(BuildChallengeProperties(provider, redirectUrl, bindUserId: null),
                OpenIddictClientAspNetCoreDefaults.AuthenticationScheme);
        }

        // ===================== Bind entry point (ADR-019) =====================

        /// <summary>
        /// Attach a provider to the logged-in account: a top-level form POST
        /// authenticated by the interactive cookie, with the same DB session
        /// liveness backstop as /connect/authorize. POST-only — cross-site POSTs
        /// don't carry the SameSite=Lax cookie, which closes forced-binding CSRF
        /// without anti-forgery tokens.
        /// </summary>
        [HttpPost("~/connect/bind/{provider}")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Bind(string provider, [FromForm] string? redirectUrl)
        {
            var providerName = provider.ToLowerInvariant() switch
            {
                "github" => Providers.GitHub,
                "google" => Providers.Google,
                _ => null,
            };
            if (providerName is null)
                return NotFound();

            var redirectValidation = oauthSecurity.ValidateRedirectUrl(redirectUrl);
            if (!redirectValidation.IsSuccess)
                return this.ToErrorResponse(redirectValidation.Error, redirectValidation.ErrorMessage);

            var auth = await HttpContext.AuthenticateAsync(AuthConstants.InteractiveScheme);
            if (!auth.Succeeded
                || !Guid.TryParse(auth.Principal.FindFirst("sub")?.Value, out var userId)
                || !Guid.TryParse(auth.Principal.FindFirst("sid")?.Value, out var sessionId))
            {
                return ChallengeInteractive(redirectUrl);
            }

            // The cookie is only a pointer; the database decides (same rule as
            // the authorize endpoint). Existence == liveness under the
            // soft-delete query filter.
            var sessionAlive = await db.Sessions.AnyAsync(s =>
                s.Id == sessionId && !s.Revoked && s.ExpiresAt > DateTimeOffset.UtcNow);
            var userAlive = sessionAlive && await db.Users.AnyAsync(u => u.Id == userId);
            if (!userAlive)
            {
                await HttpContext.SignOutAsync(AuthConstants.InteractiveScheme);
                return ChallengeInteractive(redirectUrl);
            }

            return Challenge(BuildChallengeProperties(providerName, redirectUrl, userId),
                OpenIddictClientAspNetCoreDefaults.AuthenticationScheme);
        }

        /// <summary>
        /// 302 to the SPA login page with returnUrl back to the settings page.
        /// No POST-resume machinery: after re-login the user lands on settings and
        /// clicks bind again (ADR-019). Explicit redirect rather than a cookie
        /// challenge — under [ApiController] a bare challenge resolves to the
        /// bearer scheme (401), not the interactive cookie.
        /// </summary>
        private IActionResult ChallengeInteractive(string? redirectUrl)
        {
            var returnUrl = string.IsNullOrEmpty(redirectUrl) ? "/" : redirectUrl;
            return Redirect(QueryHelpers.AddQueryString("/login", "returnUrl", returnUrl));
        }

        private static AuthenticationProperties BuildChallengeProperties(
            string provider, string? redirectUrl, Guid? bindUserId)
            => new()
            {
                Items =
                {
                    [OpenIddictClientAspNetCoreConstants.Properties.ProviderName] = provider,
                    [RedirectUrlKey] = redirectUrl,
                    [BindUserIdKey] = bindUserId?.ToString(),
                },
            };

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
        /// Login: upsert/link/merge the account, create a session, then hand off
        /// to the SPA with a one-time auth code (tokens never appear in URLs).
        /// Bind: attach/merge only — the user is already logged in, so no session
        /// and no auth code are minted; just a 302 back to the settings page
        /// with a success or error indicator (ADR-019).
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

            var userResult = await oauthService.ProcessOAuthLoginAsync(
                provider, providerUserId, email, displayName, bindUserId, providerLogin, emailVerified);

            if (bindUserId is not null)
            {
                if (string.IsNullOrEmpty(redirectUrl))
                    return userResult.IsSuccess
                        ? Ok()
                        : this.ToErrorResponse(userResult.Error, userResult.ErrorMessage);

                // Error codes only — internal messages don't belong in URLs.
                return userResult.IsSuccess
                    ? Redirect(QueryHelpers.AddQueryString(redirectUrl, "bound", provider.ToString().ToLowerInvariant()))
                    : Redirect(QueryHelpers.AddQueryString(redirectUrl, "error", userResult.Error.ToString()));
            }

            var (ipAddress, device) = this.GetClientContext();

            var result = userResult.IsSuccess
                ? await sessionService.CreateSessionAsync(userResult.Value, ipAddress, device)
                : Result<AuthResponse>.Fail(userResult.Error, userResult.ErrorMessage);

            if (!result.IsSuccess)
            {
                // Error codes only — internal messages don't belong in URLs.
                if (!string.IsNullOrEmpty(redirectUrl))
                    return Redirect(QueryHelpers.AddQueryString(redirectUrl, "error", result.Error.ToString()));
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
