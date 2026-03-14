using AuthService.Data;
using AuthService.DTOs.Auth;
using AuthService.DTOs.Auth.Github;
using AuthService.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace AuthService.Services
{
    public class GithubOAuthOptions
    {
        public const string Section = "GithubOAuth";

        public string ClientId { get; set; } = null!;
        public string ClientSecret { get; set; } = null!;
        public string CallbackUrl { get; set; } = null!;
    }

    public interface IGithubAuthService
    {
        Task<AuthResponse> LoginAsync(string code, string ipAddress, string device);
    }

    public class GithubAuthService(
        HttpClient http, 
        IOptions<GithubOAuthOptions> options, 
        AppDbContext db,
        IJwtService jwtService,
        IOptions<JwtOptions> jwtOptions) : IGithubAuthService
    {
        private readonly GithubOAuthOptions _options = options.Value;
        private readonly JwtOptions _jwtOptions = jwtOptions.Value;

        public async Task<AuthResponse> LoginAsync(string code, string ipAddress, string device)
        {
            var token = await ExchangeCode(code);
            var githubUser = await GetGithubUser(token);

            var authProvider = await db.AuthProviders
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.Provider == AuthProviderType.Github && a.ProviderUserId == githubUser.Id.ToString());

            User user;

            if (authProvider is not null)
            {
                user = authProvider.User;
                if (user.IsDeleted)
                    throw new UnauthorizedAccessException("User is deleted.");
            }
            else
            {
                UserEmail? userEmail = null;
                if (!string.IsNullOrEmpty(githubUser.Email))
                {
                    userEmail = await db.UserEmails
                        .Include(e => e.User)
                        .FirstOrDefaultAsync(e => e.Email == githubUser.Email.ToLowerInvariant());
                }

                if (userEmail is not null)
                {
                    user = userEmail.User;
                    if (user.IsDeleted)
                        throw new UnauthorizedAccessException("User is deleted.");
                    
                    authProvider = new AuthProvider
                    {
                        UserId = user.Id,
                        Provider = AuthProviderType.Github,
                        ProviderUserId = githubUser.Id.ToString()
                    };
                    db.AuthProviders.Add(authProvider);
                }
                else
                {
                    user = new User
                    {
                        DisplayName = githubUser.Login,
                    };
                    db.Users.Add(user);

                    if (!string.IsNullOrEmpty(githubUser.Email))
                    {
                        db.UserEmails.Add(new UserEmail
                        {
                            UserId = user.Id,
                            Email = githubUser.Email.ToLowerInvariant(),
                            IsPrimary = true
                        });
                    }

                    authProvider = new AuthProvider
                    {
                        UserId = user.Id,
                        Provider = AuthProviderType.Github,
                        ProviderUserId = githubUser.Id.ToString()
                    };
                    db.AuthProviders.Add(authProvider);
                }
            }

            var session = new Session
            {
                UserId = user.Id,
                Device = device,
                IpAddress = ipAddress,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(_jwtOptions.SessionExpirationDays),
            };

            var refreshTokenRaw = GenerateRefreshToken();
            var refreshToken = new RefreshToken
            {
                SessionId = session.Id,
                TokenHash = HashToken(refreshTokenRaw),
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(_jwtOptions.RefreshTokenExpirationDays),
            };

            db.Sessions.Add(session);
            db.RefreshTokens.Add(refreshToken);

            await db.SaveChangesAsync();

            var accessToken = jwtService.GenerateAccessToken(user.Id, session.Id);

            return new AuthResponse
            {
                UserId = user.Id,
                AccessToken = accessToken,
                RefreshToken = refreshTokenRaw,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(_jwtOptions.AccessTokenExpirationMinutes),
            };
        }

        private async Task<string> ExchangeCode(string code)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/oauth/access_token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = _options.ClientId,
                    ["client_secret"] = _options.ClientSecret,
                    ["code"] = code,
                    ["redirect_uri"] = _options.CallbackUrl
                })
            };

            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await http.SendAsync(request);
            
            response.EnsureSuccessStatusCode();

            var jsonContent = await response.Content.ReadAsStringAsync();
            var result = System.Text.Json.JsonSerializer.Deserialize<GithubTokenResponse>(jsonContent);

            if (result == null || string.IsNullOrEmpty(result.AccessToken))
            {
                throw new InvalidOperationException($"Failed to exchange GitHub code. GitHub response: {jsonContent}");
            }

            return result.AccessToken;
        }

        private async Task<GithubUser> GetGithubUser(string accessToken)
        {
            http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            http.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("AuthService", "1.0"));

            var user = await http.GetFromJsonAsync<GithubUser>("https://api.github.com/user");

            return user!;
        }


        private static string GenerateRefreshToken()
        {
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        }

        private static string HashToken(string token)
        {
            var bytes = Convert.FromBase64String(token);
            var hash = SHA256.HashData(bytes);
            return Convert.ToBase64String(hash);
        }
    }

}