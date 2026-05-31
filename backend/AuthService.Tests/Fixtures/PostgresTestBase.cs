using AuthService.Data;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Tests.Fixtures
{
    /// <summary>
    /// Base for tests that need a real PostgreSQL database. Shares the assembly-wide
    /// container (via <see cref="PostgresCollection"/>) and truncates all tables before
    /// each test so cases stay isolated without paying for schema recreation.
    /// </summary>
    [Collection(PostgresCollection.Name)]
    public abstract class PostgresTestBase : IAsyncLifetime
    {
        private readonly PostgresContainerFixture _fixture;
        protected AppDbContext Db { get; private set; } = null!;

        protected PostgresTestBase(PostgresContainerFixture fixture)
        {
            _fixture = fixture;
        }

        public async ValueTask InitializeAsync()
        {
            await using (var cleanup = _fixture.CreateDbContext())
            {
                // Order-independent: one statement, cascades through FKs, resets identities.
                await cleanup.Database.ExecuteSqlRawAsync(
                    """
                    TRUNCATE TABLE
                        "ApiKeys", "RefreshTokens", "Sessions", "AuthProviders",
                        "PasswordCredentials", "UserEmails", "EmailVerifications",
                        "PasswordResets", "Users"
                    RESTART IDENTITY CASCADE;
                    """);
            }

            Db = _fixture.CreateDbContext();
        }

        public ValueTask DisposeAsync()
        {
            Db.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
