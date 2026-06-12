using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AuthService.Data;
using AuthService.DTOs.Auth;
using AuthService.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AuthService.Tests.Integration.Controllers
{
    [Collection("Api Tests")]
    public class PasswordAuthController_ResetAndChangeTests(ApiTestFixture fixture)
    {
        private readonly HttpClient _client = fixture.Client;
        private readonly ApiTestFixture _fixture = fixture;

        private RecordingEmailService Mailbox =>
            _fixture.Factory.Services.GetRequiredService<RecordingEmailService>();

        private async Task<(AuthResponse Auth, string Email)> RegisterUserAsync(bool verifyEmail = true)
        {
            var email = $"reset-{Guid.NewGuid():N}@example.com";
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest
            {
                Username = $"u{Guid.NewGuid():N}"[..15],
                DisplayName = "ResetTestUser",
                Email = email,
                Password = "OldPassword123",
            }, TestContext.Current.CancellationToken);
            response.EnsureSuccessStatusCode();
            var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(TestContext.Current.CancellationToken))!;

            if (verifyEmail)
            {
                using var scope = _fixture.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var userEmail = await db.UserEmails.SingleAsync(e => e.Email == email, TestContext.Current.CancellationToken);
                userEmail.VerifiedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            return (auth, email);
        }

        private static string ExtractToken(string resetUrl) =>
            Uri.UnescapeDataString(resetUrl.Split("?token=")[1]);

        // ==================== Forgot password ====================

        [Fact]
        public async Task ForgotPassword_UnknownEmail_Returns204()
        {
            var response = await _client.PostAsJsonAsync("/api/v1/auth/forgot-password",
                new ForgotPasswordRequest { Email = "nobody@example.com" }, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        [Fact]
        public async Task ForgotPassword_UnverifiedEmail_Returns204ButSendsNothing()
        {
            var (_, email) = await RegisterUserAsync(verifyEmail: false);

            var response = await _client.PostAsJsonAsync("/api/v1/auth/forgot-password",
                new ForgotPasswordRequest { Email = email }, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Null(Mailbox.LastResetUrlFor(email));
        }

        // ==================== Reset password (full flow) ====================

        [Fact]
        public async Task ResetPassword_FullFlow_ChangesPasswordAndRevokesSessions()
        {
            var (auth, email) = await RegisterUserAsync();

            var forgot = await _client.PostAsJsonAsync("/api/v1/auth/forgot-password",
                new ForgotPasswordRequest { Email = email }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NoContent, forgot.StatusCode);

            var resetUrl = Mailbox.LastResetUrlFor(email);
            Assert.NotNull(resetUrl);

            var reset = await _client.PostAsJsonAsync("/api/v1/auth/reset-password",
                new ResetPasswordRequest { Token = ExtractToken(resetUrl), NewPassword = "NewPassword456" },
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

            // Every pre-reset session is signed out.
            var refresh = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
                new RefreshRequest { RefreshToken = auth.RefreshToken }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);

            // Old password no longer works; the new one does.
            var oldLogin = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new LoginRequest { Email = email, Password = "OldPassword123" }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);

            var newLogin = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new LoginRequest { Email = email, Password = "NewPassword456" }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
        }

        [Fact]
        public async Task ResetPassword_WithGarbageToken_Returns400()
        {
            var response = await _client.PostAsJsonAsync("/api/v1/auth/reset-password",
                new ResetPasswordRequest { Token = "garbage", NewPassword = "NewPassword456" },
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        // ==================== Change password ====================

        [Fact]
        public async Task ChangePassword_WithoutToken_Returns401()
        {
            var response = await _client.PostAsJsonAsync("/api/v1/auth/change-password",
                new ChangePasswordRequest { CurrentPassword = "x12345678", NewPassword = "NewPassword456" },
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task ChangePassword_WrongCurrentPassword_Returns401()
        {
            var (auth, _) = await RegisterUserAsync();

            var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/change-password")
            {
                Content = JsonContent.Create(new ChangePasswordRequest
                {
                    CurrentPassword = "WrongPassword1",
                    NewPassword = "NewPassword456",
                })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
            var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task ChangePassword_FullFlow_KeepsCurrentSession_RevokesOthers()
        {
            var (current, email) = await RegisterUserAsync();

            // A second session that should be signed out by the change.
            var otherLogin = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new LoginRequest { Email = email, Password = "OldPassword123" }, TestContext.Current.CancellationToken);
            var other = (await otherLogin.Content.ReadFromJsonAsync<AuthResponse>(TestContext.Current.CancellationToken))!;

            var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/change-password")
            {
                Content = JsonContent.Create(new ChangePasswordRequest
                {
                    CurrentPassword = "OldPassword123",
                    NewPassword = "NewPassword456",
                })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", current.AccessToken);
            var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

            // The other session is gone; the caller's own survives.
            var otherRefresh = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
                new RefreshRequest { RefreshToken = other.RefreshToken }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, otherRefresh.StatusCode);

            var ownRefresh = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
                new RefreshRequest { RefreshToken = current.RefreshToken }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, ownRefresh.StatusCode);

            var newLogin = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new LoginRequest { Email = email, Password = "NewPassword456" }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
        }
    }
}
