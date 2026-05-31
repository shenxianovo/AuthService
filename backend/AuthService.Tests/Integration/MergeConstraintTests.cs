using AuthService.Entities;
using AuthService.Services;
using AuthService.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Tests.Integration
{
    /// <summary>
    /// Merge-path tests against real PostgreSQL. These exist because the merge logic
    /// is shaped by constraints the InMemory provider ignores: UserEmail.Email and
    /// AuthProvider(Provider,ProviderUserId) unique indexes, PasswordCredential's
    /// UserId primary key, and FK cascade on user delete. AccountServiceTests cover
    /// the same logic on InMemory for speed; these prove it survives the real schema.
    /// </summary>
    public class MergeConstraintTests(PostgresContainerFixture fixture) : PostgresTestBase(fixture)
    {
        private AccountService Sut => new(Db);

        [Fact]
        public async Task Merge_MigratesApiKeys_WithoutViolatingConstraints()
        {
            var target = new User { Username = "target", DisplayName = "Target" };
            var source = new User { Username = "source", DisplayName = "Source" };
            Db.Users.AddRange(target, source);
            Db.ApiKeys.Add(new ApiKey { UserId = source.Id, Name = "k", Prefix = "abc12345", SecretHash = "h" });
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            await Sut.MergeAsync(source.Id, target.Id);
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var key = await Db.ApiKeys.SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal(target.Id, key.UserId);
        }

        [Fact]
        public async Task Merge_DeduplicatesSharedEmail_UnderUniqueConstraint()
        {
            // The email unique index is the whole reason dedup exists. On Postgres a
            // dedup bug surfaces as a unique violation here; on InMemory it slips by.
            var target = new User { Username = "target", DisplayName = "Target" };
            Db.Users.Add(target);
            Db.UserEmails.Add(new UserEmail { UserId = target.Id, Email = "shared@example.com", IsPrimary = true });

            var source = new User { Username = "source", DisplayName = "Source" };
            Db.Users.Add(source);
            Db.UserEmails.Add(new UserEmail { UserId = source.Id, Email = "unique@example.com", IsPrimary = true });
            // NOTE: source cannot also hold shared@example.com — the unique index forbids
            // two rows with the same address, which is itself the InMemory blind spot.
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            await Sut.MergeAsync(source.Id, target.Id);
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var emails = await Db.UserEmails.Where(e => e.UserId == target.Id)
                .Select(e => e.Email).ToListAsync(TestContext.Current.CancellationToken);
            Assert.Contains("shared@example.com", emails);
            Assert.Contains("unique@example.com", emails);
        }
        // APPEND_MARKER

        [Fact]
        public async Task Merge_MovesPasswordCredential_AcrossPrimaryKey()
        {
            // PasswordCredential.UserId IS the primary key, so merge deletes the source
            // row and inserts one for the target. This delete+recreate is invisible to
            // InMemory's key handling; here it must satisfy the real PK.
            var target = new User { Username = "target", DisplayName = "Target" };
            var source = new User { Username = "source", DisplayName = "Source" };
            Db.Users.AddRange(target, source);
            Db.PasswordCredentials.Add(new PasswordCredential { UserId = source.Id, PasswordHash = "salt.hash" });
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            await Sut.MergeAsync(source.Id, target.Id);
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var pwd = await Db.PasswordCredentials.SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal(target.Id, pwd.UserId);
            Assert.Equal("salt.hash", pwd.PasswordHash);
        }

        [Fact]
        public async Task Merge_MovesAuthProvider_WithoutUniqueViolation()
        {
            // AuthProvider(Provider, ProviderUserId) is uniquely indexed. Reassigning the
            // source's provider rows to the target must not collide with the target's.
            var target = new User { Username = "target", DisplayName = "Target" };
            Db.Users.Add(target);
            Db.AuthProviders.Add(new AuthProvider { UserId = target.Id, Provider = AuthProviderType.Google, ProviderUserId = "g-1" });

            var source = new User { Username = "source", DisplayName = "Source" };
            Db.Users.Add(source);
            Db.AuthProviders.Add(new AuthProvider { UserId = source.Id, Provider = AuthProviderType.Github, ProviderUserId = "gh-1" });
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            await Sut.MergeAsync(source.Id, target.Id);
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var providers = await Db.AuthProviders.Where(p => p.UserId == target.Id)
                .Select(p => p.Provider).ToListAsync(TestContext.Current.CancellationToken);
            Assert.Equal(2, providers.Count);
            Assert.Contains(AuthProviderType.Github, providers);
            Assert.Contains(AuthProviderType.Google, providers);
        }

        [Fact]
        public async Task Merge_SoftDeletesSource_WithoutCascadingAwayMovedRows()
        {
            // FKs cascade on user delete (DeleteBehavior.Cascade). Merge must SOFT-delete
            // the source (IsDeleted = true), never hard-delete it — otherwise the rows it
            // just reassigned to the target would cascade away. This guards that contract.
            var target = new User { Username = "target", DisplayName = "Target" };
            var source = new User { Username = "source", DisplayName = "Source" };
            Db.Users.AddRange(target, source);
            Db.AuthProviders.Add(new AuthProvider { UserId = source.Id, Provider = AuthProviderType.Github, ProviderUserId = "gh-1" });
            Db.ApiKeys.Add(new ApiKey { UserId = source.Id, Name = "k", Prefix = "pfx12345", SecretHash = "h" });
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            await Sut.MergeAsync(source.Id, target.Id);
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var sourceAfter = await Db.Users.FindAsync([source.Id], TestContext.Current.CancellationToken);
            Assert.NotNull(sourceAfter);
            Assert.True(sourceAfter.IsDeleted);

            // The reassigned rows survived (not cascade-deleted with the source).
            Assert.Equal(1, await Db.AuthProviders.CountAsync(p => p.UserId == target.Id, TestContext.Current.CancellationToken));
            Assert.Equal(1, await Db.ApiKeys.CountAsync(k => k.UserId == target.Id, TestContext.Current.CancellationToken));
        }
    }
}
