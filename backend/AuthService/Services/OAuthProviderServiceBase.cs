using AuthService.Common;
using AuthService.DTOs.Auth;
using AuthService.Entities;
using System.Net.Http.Headers;

namespace AuthService.Services
{
    /// <summary>
    /// Normalized user info returned by any OAuth provider after token exchange.
    /// </summary>
    public record OAuthUserInfo(
        string ProviderUserId,
        string? Email,
        string DisplayName,
        string? ProviderLogin = null
    );

    /// <summary>
    /// Base class that implements the common OAuth login flow:
    /// ExchangeCode → GetUserInfo → ProcessOAuthLogin → CreateSession.
    /// Subclasses only need to implement the provider-specific steps.
    /// </summary>
    public abstract class OAuthProviderServiceBase(
        HttpClient http,
        IOAuthService oauthService,
        ISessionService sessionService)
    {
        protected readonly HttpClient Http = http;

        /// <summary>The provider type used when persisting the OAuth link.</summary>
        protected abstract AuthProviderType ProviderType { get; }

        /// <summary>Exchange the authorization code for a provider access token.</summary>
        protected abstract Task<string> ExchangeCodeAsync(string code);

        /// <summary>Fetch normalized user info from the provider using the access token.</summary>
        protected abstract Task<OAuthUserInfo> GetUserInfoAsync(string accessToken);

        /// <summary>
        /// Full login flow: exchange code, get user info, upsert user, create session.
        /// </summary>
        public async Task<Result<AuthResponse>> LoginAsync(
            string code,
            string ipAddress,
            string device,
            Guid? currentUserId = null)
        {
            var accessToken = await ExchangeCodeAsync(code);
            var userInfo = await GetUserInfoAsync(accessToken);

            var userResult = await oauthService.ProcessOAuthLoginAsync(
                ProviderType,
                userInfo.ProviderUserId,
                userInfo.Email,
                userInfo.DisplayName,
                currentUserId,
                userInfo.ProviderLogin);

            if (!userResult.IsSuccess)
                return Result<AuthResponse>.Fail(userResult.Error, userResult.ErrorMessage);

            return await sessionService.CreateSessionAsync(userResult.Value, ipAddress, device);
        }

        /// <summary>
        /// Helper: set Authorization Bearer header on the shared HttpClient.
        /// </summary>
        protected void SetBearerToken(string token)
        {
            Http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }
    }
}
