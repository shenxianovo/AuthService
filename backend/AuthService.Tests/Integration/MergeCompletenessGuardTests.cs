using AuthService.Data;
using AuthService.Entities;
using AuthService.Services;
using AuthService.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Tests.Integration
{
    /// <summary>
    /// Completeness guard for account merge (ADR-010). "Account composition" is the
    /// set of relations owned by a User, and MergeAsync must define semantics for
    /// every one of them. The manifest below IS that contract: when an entity gains
    /// a foreign key to User, the structural test fails until the entity is handled
    /// in AccountService.MergeAsync and registered here — which the behavioral test
    /// then exercises against the real PostgreSQL schema.
    /// </summary>
    public class MergeCompletenessGuardTests(PostgresContainerFixture fixture) : PostgresTestBase(fixture)
    {
        private enum MergeBehavior
        {
            /// <summary>Rows are reassigned to the target user.</summary>
            Moved,
            /// <summary>Row is deleted on the source and recreated for the target (PK = UserId).</summary>
            Recreated,
            /// <summary>Rows are deleted outright — they must not survive the merge.</summary>
            Deleted,
        }

        private sealed record RelationSpec(
            MergeBehavior Behavior,
            Func<AppDbContext, Guid, CancellationToken, Task<int>> CountOwnedBy,
            Action<AppDbContext, User> SeedFor);

        // IgnoreQueryFilters everywhere: the guard must see rows even if soft-delete
        // query filters would hide the merged-away source user's data.
        private static readonly Dictionary<Type, RelationSpec> Manifest = new()
        {
            [typeof(UserEmail)] = new(
                MergeBehavior.Moved,
                (db, uid, ct) => db.UserEmails.IgnoreQueryFilters().CountAsync(e => e.UserId == uid, ct),
                (db, u) => db.UserEmails.Add(new UserEmail
                {
                    UserId = u.Id,
                    Email = $"{u.Username}@example.com",
                    IsPrimary = true,
                    VerifiedAt = DateTimeOffset.UtcNow
                })),
            [typeof(AuthProvider)] = new(
                MergeBehavior.Moved,
                (db, uid, ct) => db.AuthProviders.IgnoreQueryFilters().CountAsync(p => p.UserId == uid, ct),
                (db, u) => db.AuthProviders.Add(new AuthProvider
                {
                    UserId = u.Id,
                    Provider = AuthProviderType.Github,
                    ProviderUserId = $"gh-{u.Username}"
                })),
            [typeof(Session)] = new(
                MergeBehavior.Moved,
                (db, uid, ct) => db.Sessions.IgnoreQueryFilters().CountAsync(s => s.UserId == uid, ct),
                (db, u) => db.Sessions.Add(new Session
                {
                    UserId = u.Id,
                    Device = "guard-test",
                    IpAddress = "127.0.0.1",
                    ExpiresAt = DateTimeOffset.UtcNow.AddDays(7)
                })),
            [typeof(ApiKey)] = new(
                MergeBehavior.Moved,
                (db, uid, ct) => db.ApiKeys.IgnoreQueryFilters().CountAsync(k => k.UserId == uid, ct),
                (db, u) => db.ApiKeys.Add(new ApiKey
                {
                    UserId = u.Id,
                    Name = "guard-key",
                    Prefix = u.Username[..8],
                    SecretHash = $"hash-{u.Username}"
                })),
            [typeof(PasswordCredential)] = new(
                MergeBehavior.Recreated,
                (db, uid, ct) => db.PasswordCredentials.IgnoreQueryFilters().CountAsync(p => p.UserId == uid, ct),
                (db, u) => db.PasswordCredentials.Add(new PasswordCredential
                {
                    UserId = u.Id,
                    PasswordHash = $"hash-{u.Username}"
                })),
            [typeof(PasswordReset)] = new(
                MergeBehavior.Deleted,
                (db, uid, ct) => db.PasswordResets.IgnoreQueryFilters().CountAsync(r => r.UserId == uid, ct),
                (db, u) => db.PasswordResets.Add(new PasswordReset
                {
                    UserId = u.Id,
                    TokenHash = $"reset-{u.Username}",
                    ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30)
                })),
        };

        [Fact]
        public void EveryUserOwnedRelation_HasMergeSemantics()
        {
            var userOwned = Db.Model.GetEntityTypes()
                .Where(et => et.GetForeignKeys().Any(fk => fk.PrincipalEntityType.ClrType == typeof(User)))
                .Select(et => et.ClrType)
                .ToList();

            var missing = userOwned.Except(Manifest.Keys).Select(t => t.Name).ToList();
            var stale = Manifest.Keys.Except(userOwned).Select(t => t.Name).ToList();

            Assert.True(missing.Count == 0,
                $"Entities own User data but have no merge semantics: {string.Join(", ", missing)}. " +
                "Handle them in AccountService.MergeAsync, then register them in the manifest of this test.");
            Assert.True(stale.Count == 0,
                $"Manifest entries no longer in the EF model: {string.Join(", ", stale)}. Remove them from the manifest.");
        }

        [Fact]
        public async Task Merge_HandlesEveryRelation_PerManifest()
        {
            var ct = TestContext.Current.CancellationToken;

            var target = new User { Username = "mergetgt", DisplayName = "Target" };
            var source = new User { Username = "mergesrc", DisplayName = "Source" };
            Db.Users.AddRange(target, source);
            foreach (var spec in Manifest.Values)
            {
                spec.SeedFor(Db, source);
                spec.SeedFor(Db, target);
            }
            await Db.SaveChangesAsync(ct);
            var sourceSessionIds = await Db.Sessions
                .Where(s => s.UserId == source.Id).Select(s => s.Id).ToListAsync(ct);

            await new AccountService(Db).MergeAsync(source.Id, target.Id);
            await Db.SaveChangesAsync(ct);

            foreach (var (type, spec) in Manifest)
            {
                var sourceCount = await spec.CountOwnedBy(Db, source.Id, ct);
                Assert.True(sourceCount == 0,
                    $"{type.Name}: source still owns {sourceCount} row(s) after merge.");

                // Moved: target's own row + the source's. Recreated/Deleted: target keeps its own only.
                var expectedTarget = spec.Behavior == MergeBehavior.Moved ? 2 : 1;
                var targetCount = await spec.CountOwnedBy(Db, target.Id, ct);
                Assert.True(targetCount == expectedTarget,
                    $"{type.Name}: target owns {targetCount} row(s) after merge, expected {expectedTarget}.");
            }

            // Semantics beyond row ownership:
            var inheritedSessions = await Db.Sessions.IgnoreQueryFilters()
                .Where(s => sourceSessionIds.Contains(s.Id)).ToListAsync(ct);
            Assert.All(inheritedSessions, s => Assert.True(s.Revoked,
                "sessions inherited from the source must arrive revoked"));

            Assert.Equal(1, await Db.UserEmails.IgnoreQueryFilters()
                .CountAsync(e => e.UserId == target.Id && e.IsPrimary, ct));

            var sourceAfter = await Db.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == source.Id, ct);
            Assert.True(sourceAfter.IsDeleted);
        }

        [Fact]
        public async Task Merge_OfAlreadyMergedSource_IsIdempotent()
        {
            var ct = TestContext.Current.CancellationToken;

            var target = new User { Username = "mergetgt", DisplayName = "Target" };
            var source = new User { Username = "mergesrc", DisplayName = "Source" };
            Db.Users.AddRange(target, source);
            Db.AuthProviders.Add(new AuthProvider { UserId = source.Id, Provider = AuthProviderType.Github, ProviderUserId = "gh-1" });
            await Db.SaveChangesAsync(ct);

            var sut = new AccountService(Db);
            await sut.MergeAsync(source.Id, target.Id);
            await Db.SaveChangesAsync(ct);

            await sut.MergeAsync(source.Id, target.Id);
            await Db.SaveChangesAsync(ct);

            Assert.Equal(1, await Db.AuthProviders.IgnoreQueryFilters()
                .CountAsync(p => p.UserId == target.Id, ct));
        }
    }
}
