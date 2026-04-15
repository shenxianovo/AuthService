using AuthService.Common;
using AuthService.Data;
using AuthService.Entities;
using AuthService.Services;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Tests.Unit.Services
{
    public class OAuthServiceTests : IDisposable
    {
        private readonly AppDbContext _db;
        private readonly OAuthService _sut;

        public OAuthServiceTests()
        {
            var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            _db = new AppDbContext(dbOptions);
            _sut = new OAuthService(_db);
        }

        public void Dispose() => _db.Dispose();

        // ===================== New User Creation =====================

        [Fact]
        public async Task ProcessOAuth_NewProvider_NewEmail_CreatesNewUser()
        {
            var result = await _sut.ProcessOAuthLoginAsync(
                AuthProviderType.Github, "github-123", "user@example.com", "TestUser");

            Assert.True(result.IsSuccess);
            Assert.Equal("TestUser", result.Value.DisplayName);

            var provider = await _db.AuthProviders.FirstOrDefaultAsync();
            Assert.NotNull(provider);
            Assert.Equal(AuthProviderType.Github, provider.Provider);
            Assert.Equal("github-123", provider.ProviderUserId);
            Assert.Equal(result.Value.Id, provider.UserId);

            var email = await _db.UserEmails.FirstOrDefaultAsync();
            Assert.NotNull(email);
            Assert.Equal("user@example.com", email.Email);
            Assert.True(email.IsPrimary);
            Assert.Equal(result.Value.Id, email.UserId);
        }

        [Fact]
        public async Task ProcessOAuth_NewProvider_NullEmail_CreatesUserWithoutEmail()
        {
            var result = await _sut.ProcessOAuthLoginAsync(
                AuthProviderType.Github, "github-456", null, "NoEmailUser");

            Assert.True(result.IsSuccess);
            Assert.Equal("NoEmailUser", result.Value.DisplayName);

            var emails = await _db.UserEmails.CountAsync();
            Assert.Equal(0, emails);

            var provider = await _db.AuthProviders.FirstOrDefaultAsync();
            Assert.NotNull(provider);
            Assert.Equal(result.Value.Id, provider.UserId);
        }

        // ===================== Existing Provider Login =====================

        [Fact]
        public async Task ProcessOAuth_ExistingProvider_ReturnsExistingUser()
        {
            var first = await _sut.ProcessOAuthLoginAsync(
                AuthProviderType.Github, "github-123", "user@example.com", "TestUser");

            var second = await _sut.ProcessOAuthLoginAsync(
                AuthProviderType.Github, "github-123", "user@example.com", "TestUser");

            Assert.True(first.IsSuccess);
            Assert.True(second.IsSuccess);
            Assert.Equal(first.Value.Id, second.Value.Id);

            var userCount = await _db.Users.CountAsync();
            Assert.Equal(1, userCount);
        }

        [Fact]
        public async Task ProcessOAuth_ExistingProvider_DeletedUser_ReturnsUserDeleted()
        {
            var result = await _sut.ProcessOAuthLoginAsync(
                AuthProviderType.Github, "github-123", "user@example.com", "TestUser");

            Assert.True(result.IsSuccess);
            result.Value.IsDeleted = true;
            await _db.SaveChangesAsync();

            var second = await _sut.ProcessOAuthLoginAsync(
                AuthProviderType.Github, "github-123", "user@example.com", "TestUser");

            Assert.False(second.IsSuccess);
            Assert.Equal(AuthError.UserDeleted, second.Error);
        }

        // ===================== Email-based User Matching =====================

        [Fact]
        public async Task ProcessOAuth_NewProvider_ExistingEmail_LinksToExistingUser()
        {
            var existingUser = new User { DisplayName = "Existing" };
            _db.Users.Add(existingUser);
            _db.UserEmails.Add(new UserEmail { UserId = existingUser.Id, Email = "shared@example.com", IsPrimary = true });
            await _db.SaveChangesAsync();

            var result = await _sut.ProcessOAuthLoginAsync(
                AuthProviderType.Github, "github-789", "shared@example.com", "GithubUser");

            Assert.True(result.IsSuccess);
            Assert.Equal(existingUser.Id, result.Value.Id);

            var providers = await _db.AuthProviders.Where(p => p.UserId == existingUser.Id).ToListAsync();
            Assert.Single(providers);
            Assert.Equal(AuthProviderType.Github, providers[0].Provider);
        }

        [Fact]
        public async Task ProcessOAuth_NewProvider_ExistingEmail_DeletedUser_ReturnsUserDeleted()
        {
            var existingUser = new User { DisplayName = "Deleted", IsDeleted = true };
            _db.Users.Add(existingUser);
            _db.UserEmails.Add(new UserEmail { UserId = existingUser.Id, Email = "deleted@example.com", IsPrimary = true });
            await _db.SaveChangesAsync();

            var result = await _sut.ProcessOAuthLoginAsync(
                AuthProviderType.Github, "github-000", "deleted@example.com", "GithubUser");

            Assert.False(result.IsSuccess);
            Assert.Equal(AuthError.UserDeleted, result.Error);
        }

        // ===================== Binding (currentUserId provided) =====================

        [Fact]
        public async Task ProcessOAuth_Binding_NewProvider_LinksProviderToCurrentUser()
        {
            var currentUser = new User { DisplayName = "CurrentUser" };
            _db.Users.Add(currentUser);
            await _db.SaveChangesAsync();

            var result = await _sut.ProcessOAuthLoginAsync(
                AuthProviderType.Google, "google-123", "google@example.com", "GoogleUser",
                currentUserId: currentUser.Id);

            Assert.True(result.IsSuccess);
            Assert.Equal(currentUser.Id, result.Value.Id);

            var provider = await _db.AuthProviders.FirstOrDefaultAsync(p => p.UserId == currentUser.Id);
            Assert.NotNull(provider);
            Assert.Equal(AuthProviderType.Google, provider.Provider);

            var email = await _db.UserEmails.FirstOrDefaultAsync(e => e.UserId == currentUser.Id);
            Assert.NotNull(email);
            Assert.Equal("google@example.com", email.Email);
        }

        [Fact]
        public async Task ProcessOAuth_Binding_NewProvider_ExistingEmailBelongsToOtherUser_MergesUsers()
        {
            var currentUser = new User { DisplayName = "Current" };
            _db.Users.Add(currentUser);
            _db.UserEmails.Add(new UserEmail { UserId = currentUser.Id, Email = "current@example.com", IsPrimary = true });

            var otherUser = new User { DisplayName = "Other" };
            _db.Users.Add(otherUser);
            _db.UserEmails.Add(new UserEmail { UserId = otherUser.Id, Email = "shared@example.com", IsPrimary = true });
            _db.AuthProviders.Add(new AuthProvider { UserId = otherUser.Id, Provider = AuthProviderType.Password, ProviderUserId = otherUser.Id.ToString() });

            await _db.SaveChangesAsync();

            var result = await _sut.ProcessOAuthLoginAsync(
                AuthProviderType.Google, "google-456", "shared@example.com", "GoogleUser",
                currentUserId: currentUser.Id);

            Assert.True(result.IsSuccess);
            Assert.Equal(currentUser.Id, result.Value.Id);

            var otherUserAfter = await _db.Users.FindAsync(otherUser.Id);
            Assert.True(otherUserAfter!.IsDeleted);

            var movedProviders = await _db.AuthProviders
                .Where(p => p.UserId == currentUser.Id && p.Provider == AuthProviderType.Password)
                .CountAsync();
            Assert.Equal(1, movedProviders);
        }

        [Fact]
        public async Task ProcessOAuth_Binding_ExistingProvider_BelongsToOtherUser_MergesUsers()
        {
            var currentUser = new User { DisplayName = "Current" };
            _db.Users.Add(currentUser);

            var otherUser = new User { DisplayName = "Other" };
            _db.Users.Add(otherUser);
            _db.AuthProviders.Add(new AuthProvider { UserId = otherUser.Id, Provider = AuthProviderType.Github, ProviderUserId = "github-999" });

            await _db.SaveChangesAsync();

            var result = await _sut.ProcessOAuthLoginAsync(
                AuthProviderType.Github, "github-999", null, "GithubUser",
                currentUserId: currentUser.Id);

            Assert.True(result.IsSuccess);
            Assert.Equal(currentUser.Id, result.Value.Id);

            var otherUserAfter = await _db.Users.FindAsync(otherUser.Id);
            Assert.True(otherUserAfter!.IsDeleted);
        }

        [Fact]
        public async Task ProcessOAuth_Binding_ExistingProvider_SameUser_ReturnsSameUser()
        {
            var currentUser = new User { DisplayName = "Current" };
            _db.Users.Add(currentUser);
            _db.AuthProviders.Add(new AuthProvider { UserId = currentUser.Id, Provider = AuthProviderType.Github, ProviderUserId = "github-111" });
            await _db.SaveChangesAsync();

            var result = await _sut.ProcessOAuthLoginAsync(
                AuthProviderType.Github, "github-111", null, "GithubUser",
                currentUserId: currentUser.Id);

            Assert.True(result.IsSuccess);
            Assert.Equal(currentUser.Id, result.Value.Id);
            Assert.False(result.Value.IsDeleted);
        }

        [Fact]
        public async Task ProcessOAuth_Binding_InvalidCurrentUserId_ReturnsUserNotFoundForMerge()
        {
            var result = await _sut.ProcessOAuthLoginAsync(
                AuthProviderType.Github, "github-new", null, "GithubUser",
                currentUserId: Guid.NewGuid());

            Assert.False(result.IsSuccess);
            Assert.Equal(AuthError.UserNotFoundForMerge, result.Error);
        }

        // ===================== Merge Details =====================

        [Fact]
        public async Task ProcessOAuth_Merge_MovesPasswordCredential()
        {
            var currentUser = new User { DisplayName = "Current" };
            _db.Users.Add(currentUser);

            var sourceUser = new User { DisplayName = "Source" };
            _db.Users.Add(sourceUser);
            _db.PasswordCredentials.Add(new PasswordCredential { UserId = sourceUser.Id, PasswordHash = "salt.hash" });
            _db.AuthProviders.Add(new AuthProvider { UserId = sourceUser.Id, Provider = AuthProviderType.Github, ProviderUserId = "github-merge" });

            await _db.SaveChangesAsync();

            await _sut.ProcessOAuthLoginAsync(
                AuthProviderType.Github, "github-merge", null, "GithubUser",
                currentUserId: currentUser.Id);

            var pwd = await _db.PasswordCredentials.FirstOrDefaultAsync(p => p.UserId == currentUser.Id);
            Assert.NotNull(pwd);
            Assert.Equal("salt.hash", pwd.PasswordHash);
        }

        [Fact]
        public async Task ProcessOAuth_Merge_RevokesSourceSessions()
        {
            var currentUser = new User { DisplayName = "Current" };
            _db.Users.Add(currentUser);

            var sourceUser = new User { DisplayName = "Source" };
            _db.Users.Add(sourceUser);
            _db.Sessions.Add(new Session { UserId = sourceUser.Id, IpAddress = "1.2.3.4", Device = "OldDevice", ExpiresAt = DateTimeOffset.UtcNow.AddDays(30) });
            _db.AuthProviders.Add(new AuthProvider { UserId = sourceUser.Id, Provider = AuthProviderType.Github, ProviderUserId = "github-session-merge" });

            await _db.SaveChangesAsync();

            await _sut.ProcessOAuthLoginAsync(
                AuthProviderType.Github, "github-session-merge", null, "GithubUser",
                currentUserId: currentUser.Id);

            var sessions = await _db.Sessions.Where(s => s.Device == "OldDevice").ToListAsync();
            Assert.Single(sessions);
            Assert.True(sessions[0].Revoked);
            Assert.Equal(currentUser.Id, sessions[0].UserId);
        }

        [Fact]
        public async Task ProcessOAuth_Merge_HandlesEmailDeduplication()
        {
            var currentUser = new User { DisplayName = "Current" };
            _db.Users.Add(currentUser);
            _db.UserEmails.Add(new UserEmail { UserId = currentUser.Id, Email = "shared@example.com", IsPrimary = true });

            var sourceUser = new User { DisplayName = "Source" };
            _db.Users.Add(sourceUser);
            _db.UserEmails.Add(new UserEmail { UserId = sourceUser.Id, Email = "shared@example.com", IsPrimary = true });
            _db.UserEmails.Add(new UserEmail { UserId = sourceUser.Id, Email = "unique@example.com", IsPrimary = false });
            _db.AuthProviders.Add(new AuthProvider { UserId = sourceUser.Id, Provider = AuthProviderType.Github, ProviderUserId = "github-email-merge" });

            await _db.SaveChangesAsync();

            await _sut.ProcessOAuthLoginAsync(
                AuthProviderType.Github, "github-email-merge", null, "GithubUser",
                currentUserId: currentUser.Id);

            var currentEmails = await _db.UserEmails.Where(e => e.UserId == currentUser.Id).ToListAsync();
            Assert.Equal(2, currentEmails.Count);
            Assert.Contains(currentEmails, e => e.Email == "shared@example.com" && e.IsPrimary);
            Assert.Contains(currentEmails, e => e.Email == "unique@example.com" && !e.IsPrimary);

            var sourceEmails = await _db.UserEmails.Where(e => e.UserId == sourceUser.Id).CountAsync();
            Assert.Equal(0, sourceEmails);
        }

        // ===================== Email Normalization =====================

        [Fact]
        public async Task ProcessOAuth_NormalizesEmailToLowerCase()
        {
            await _sut.ProcessOAuthLoginAsync(
                AuthProviderType.Github, "github-case", "USER@EXAMPLE.COM", "TestUser");

            var email = await _db.UserEmails.FirstOrDefaultAsync();
            Assert.NotNull(email);
            Assert.Equal("user@example.com", email.Email);
        }
    }
}
