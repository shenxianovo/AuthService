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
            var user = await _sut.ProcessOAuthLoginAsync(
                AuthProviderType.Github, "github-123", "user@example.com", "TestUser");

            Assert.NotNull(user);
            Assert.Equal("TestUser", user.DisplayName);

            var provider = await _db.AuthProviders.FirstOrDefaultAsync();
            Assert.NotNull(provider);
            Assert.Equal(AuthProviderType.Github, provider.Provider);
            Assert.Equal("github-123", provider.ProviderUserId);
            Assert.Equal(user.Id, provider.UserId);

            var email = await _db.UserEmails.FirstOrDefaultAsync();
            Assert.NotNull(email);
            Assert.Equal("user@example.com", email.Email);
            Assert.True(email.IsPrimary);
            Assert.Equal(user.Id, email.UserId);
        }

        [Fact]
        public async Task ProcessOAuth_NewProvider_NullEmail_CreatesUserWithoutEmail()
        {
            var user = await _sut.ProcessOAuthLoginAsync(
                AuthProviderType.Github, "github-456", null, "NoEmailUser");

            Assert.NotNull(user);
            Assert.Equal("NoEmailUser", user.DisplayName);

            var emails = await _db.UserEmails.CountAsync();
            Assert.Equal(0, emails);

            var provider = await _db.AuthProviders.FirstOrDefaultAsync();
            Assert.NotNull(provider);
            Assert.Equal(user.Id, provider.UserId);
        }

        // ===================== Existing Provider Login =====================

        [Fact]
        public async Task ProcessOAuth_ExistingProvider_ReturnsExistingUser()
        {
            // First login creates user
            var firstUser = await _sut.ProcessOAuthLoginAsync(
                AuthProviderType.Github, "github-123", "user@example.com", "TestUser");

            // Second login returns same user
            var secondUser = await _sut.ProcessOAuthLoginAsync(
                AuthProviderType.Github, "github-123", "user@example.com", "TestUser");

            Assert.Equal(firstUser.Id, secondUser.Id);

            var userCount = await _db.Users.CountAsync();
            Assert.Equal(1, userCount);
        }

        [Fact]
        public async Task ProcessOAuth_ExistingProvider_DeletedUser_ThrowsUnauthorized()
        {
            var user = await _sut.ProcessOAuthLoginAsync(
                AuthProviderType.Github, "github-123", "user@example.com", "TestUser");

            // Soft delete the user
            user.IsDeleted = true;
            await _db.SaveChangesAsync();

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => _sut.ProcessOAuthLoginAsync(
                    AuthProviderType.Github, "github-123", "user@example.com", "TestUser"));
        }

        // ===================== Email-based User Matching =====================

        [Fact]
        public async Task ProcessOAuth_NewProvider_ExistingEmail_LinksToExistingUser()
        {
            // Create a user with password (has email)
            var existingUser = new User { DisplayName = "Existing" };
            _db.Users.Add(existingUser);
            _db.UserEmails.Add(new UserEmail
            {
                UserId = existingUser.Id,
                Email = "shared@example.com",
                IsPrimary = true,
            });
            await _db.SaveChangesAsync();

            // OAuth login with same email — should link to existing user
            var oauthUser = await _sut.ProcessOAuthLoginAsync(
                AuthProviderType.Github, "github-789", "shared@example.com", "GithubUser");

            Assert.Equal(existingUser.Id, oauthUser.Id);

            var providers = await _db.AuthProviders.Where(p => p.UserId == existingUser.Id).ToListAsync();
            Assert.Single(providers);
            Assert.Equal(AuthProviderType.Github, providers[0].Provider);
        }

        [Fact]
        public async Task ProcessOAuth_NewProvider_ExistingEmail_DeletedUser_ThrowsUnauthorized()
        {
            var existingUser = new User { DisplayName = "Deleted", IsDeleted = true };
            _db.Users.Add(existingUser);
            _db.UserEmails.Add(new UserEmail
            {
                UserId = existingUser.Id,
                Email = "deleted@example.com",
                IsPrimary = true,
            });
            await _db.SaveChangesAsync();

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => _sut.ProcessOAuthLoginAsync(
                    AuthProviderType.Github, "github-000", "deleted@example.com", "GithubUser"));
        }

        // ===================== Binding (currentUserId provided) =====================

        [Fact]
        public async Task ProcessOAuth_Binding_NewProvider_LinksProviderToCurrentUser()
        {
            // Create existing user
            var currentUser = new User { DisplayName = "CurrentUser" };
            _db.Users.Add(currentUser);
            await _db.SaveChangesAsync();

            var resultUser = await _sut.ProcessOAuthLoginAsync(
                AuthProviderType.Google, "google-123", "google@example.com", "GoogleUser",
                currentUserId: currentUser.Id);

            Assert.Equal(currentUser.Id, resultUser.Id);

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
            // Create current user
            var currentUser = new User { DisplayName = "Current" };
            _db.Users.Add(currentUser);
            _db.UserEmails.Add(new UserEmail { UserId = currentUser.Id, Email = "current@example.com", IsPrimary = true });

            // Create other user who owns the email
            var otherUser = new User { DisplayName = "Other" };
            _db.Users.Add(otherUser);
            _db.UserEmails.Add(new UserEmail { UserId = otherUser.Id, Email = "shared@example.com", IsPrimary = true });
            _db.AuthProviders.Add(new AuthProvider { UserId = otherUser.Id, Provider = AuthProviderType.Password, ProviderUserId = otherUser.Id.ToString() });

            await _db.SaveChangesAsync();

            // Bind Google (which has shared@example.com) to current user
            var resultUser = await _sut.ProcessOAuthLoginAsync(
                AuthProviderType.Google, "google-456", "shared@example.com", "GoogleUser",
                currentUserId: currentUser.Id);

            Assert.Equal(currentUser.Id, resultUser.Id);

            // Other user should be soft-deleted
            var otherUserAfter = await _db.Users.FindAsync(otherUser.Id);
            Assert.True(otherUserAfter!.IsDeleted);

            // Providers from other user should be moved to current user
            var movedProviders = await _db.AuthProviders
                .Where(p => p.UserId == currentUser.Id && p.Provider == AuthProviderType.Password)
                .CountAsync();
            Assert.Equal(1, movedProviders);
        }

        [Fact]
        public async Task ProcessOAuth_Binding_ExistingProvider_BelongsToOtherUser_MergesUsers()
        {
            // Create current user
            var currentUser = new User { DisplayName = "Current" };
            _db.Users.Add(currentUser);

            // Create other user who already has this GitHub account
            var otherUser = new User { DisplayName = "Other" };
            _db.Users.Add(otherUser);
            _db.AuthProviders.Add(new AuthProvider
            {
                UserId = otherUser.Id,
                Provider = AuthProviderType.Github,
                ProviderUserId = "github-999"
            });

            await _db.SaveChangesAsync();

            // Bind this GitHub to current user — should merge
            var resultUser = await _sut.ProcessOAuthLoginAsync(
                AuthProviderType.Github, "github-999", null, "GithubUser",
                currentUserId: currentUser.Id);

            Assert.Equal(currentUser.Id, resultUser.Id);

            // Other user should be soft-deleted
            var otherUserAfter = await _db.Users.FindAsync(otherUser.Id);
            Assert.True(otherUserAfter!.IsDeleted);
        }

        [Fact]
        public async Task ProcessOAuth_Binding_ExistingProvider_SameUser_ReturnsSameUser()
        {
            // Create current user with existing GitHub provider
            var currentUser = new User { DisplayName = "Current" };
            _db.Users.Add(currentUser);
            _db.AuthProviders.Add(new AuthProvider
            {
                UserId = currentUser.Id,
                Provider = AuthProviderType.Github,
                ProviderUserId = "github-111"
            });
            await _db.SaveChangesAsync();

            // Re-binding same provider — should just return same user
            var resultUser = await _sut.ProcessOAuthLoginAsync(
                AuthProviderType.Github, "github-111", null, "GithubUser",
                currentUserId: currentUser.Id);

            Assert.Equal(currentUser.Id, resultUser.Id);
            Assert.False(resultUser.IsDeleted);
        }

        [Fact]
        public async Task ProcessOAuth_Binding_InvalidCurrentUserId_ThrowsUnauthorized()
        {
            var nonExistentUserId = Guid.NewGuid();

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => _sut.ProcessOAuthLoginAsync(
                    AuthProviderType.Github, "github-new", null, "GithubUser",
                    currentUserId: nonExistentUserId));
        }

        // ===================== Merge Details =====================

        [Fact]
        public async Task ProcessOAuth_Merge_MovesPasswordCredential()
        {
            // Create current user (no password)
            var currentUser = new User { DisplayName = "Current" };
            _db.Users.Add(currentUser);

            // Create source user with password
            var sourceUser = new User { DisplayName = "Source" };
            _db.Users.Add(sourceUser);
            _db.PasswordCredentials.Add(new PasswordCredential
            {
                UserId = sourceUser.Id,
                PasswordHash = "salt.hash"
            });
            _db.AuthProviders.Add(new AuthProvider
            {
                UserId = sourceUser.Id,
                Provider = AuthProviderType.Github,
                ProviderUserId = "github-merge"
            });

            await _db.SaveChangesAsync();

            await _sut.ProcessOAuthLoginAsync(
                AuthProviderType.Github, "github-merge", null, "GithubUser",
                currentUserId: currentUser.Id);

            // Password credential should have moved to current user
            var pwd = await _db.PasswordCredentials.FirstOrDefaultAsync(p => p.UserId == currentUser.Id);
            Assert.NotNull(pwd);
            Assert.Equal("salt.hash", pwd.PasswordHash);
        }

        [Fact]
        public async Task ProcessOAuth_Merge_RevokesSourceSessions()
        {
            // Create current user
            var currentUser = new User { DisplayName = "Current" };
            _db.Users.Add(currentUser);

            // Create source user with an active session
            var sourceUser = new User { DisplayName = "Source" };
            _db.Users.Add(sourceUser);
            _db.Sessions.Add(new Session
            {
                UserId = sourceUser.Id,
                IpAddress = "1.2.3.4",
                Device = "OldDevice",
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            });
            _db.AuthProviders.Add(new AuthProvider
            {
                UserId = sourceUser.Id,
                Provider = AuthProviderType.Github,
                ProviderUserId = "github-session-merge"
            });

            await _db.SaveChangesAsync();

            await _sut.ProcessOAuthLoginAsync(
                AuthProviderType.Github, "github-session-merge", null, "GithubUser",
                currentUserId: currentUser.Id);

            // Source sessions should be revoked
            var sessions = await _db.Sessions.Where(s => s.Device == "OldDevice").ToListAsync();
            Assert.Single(sessions);
            Assert.True(sessions[0].Revoked);

            // Session should be moved to current user
            Assert.Equal(currentUser.Id, sessions[0].UserId);
        }

        [Fact]
        public async Task ProcessOAuth_Merge_HandlesEmailDeduplication()
        {
            // Current user has email "shared@example.com"
            var currentUser = new User { DisplayName = "Current" };
            _db.Users.Add(currentUser);
            _db.UserEmails.Add(new UserEmail { UserId = currentUser.Id, Email = "shared@example.com", IsPrimary = true });

            // Source user also has "shared@example.com" + "unique@example.com"
            var sourceUser = new User { DisplayName = "Source" };
            _db.Users.Add(sourceUser);
            _db.UserEmails.Add(new UserEmail { UserId = sourceUser.Id, Email = "shared@example.com", IsPrimary = true });
            _db.UserEmails.Add(new UserEmail { UserId = sourceUser.Id, Email = "unique@example.com", IsPrimary = false });
            _db.AuthProviders.Add(new AuthProvider
            {
                UserId = sourceUser.Id,
                Provider = AuthProviderType.Github,
                ProviderUserId = "github-email-merge"
            });

            await _db.SaveChangesAsync();

            await _sut.ProcessOAuthLoginAsync(
                AuthProviderType.Github, "github-email-merge", null, "GithubUser",
                currentUserId: currentUser.Id);

            // Current user should have 2 emails (shared kept, unique moved)
            var currentEmails = await _db.UserEmails.Where(e => e.UserId == currentUser.Id).ToListAsync();
            Assert.Equal(2, currentEmails.Count);
            Assert.Contains(currentEmails, e => e.Email == "shared@example.com" && e.IsPrimary);
            Assert.Contains(currentEmails, e => e.Email == "unique@example.com" && !e.IsPrimary);

            // Source user should have no emails (duplicate was removed)
            var sourceEmails = await _db.UserEmails.Where(e => e.UserId == sourceUser.Id).CountAsync();
            Assert.Equal(0, sourceEmails);
        }

        // ===================== Email Normalization =====================

        [Fact]
        public async Task ProcessOAuth_NormalizesEmailToLowerCase()
        {
            var user = await _sut.ProcessOAuthLoginAsync(
                AuthProviderType.Github, "github-case", "USER@EXAMPLE.COM", "TestUser");

            var email = await _db.UserEmails.FirstOrDefaultAsync();
            Assert.NotNull(email);
            Assert.Equal("user@example.com", email.Email);
        }
    }
}
