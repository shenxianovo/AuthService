using AuthService.Data;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace AuthService.Tests.Fixtures
{
    /// <summary>
    /// Spins up a single PostgreSQL container for the whole test assembly and applies
    /// EF migrations once. Shared via [Collection] so constraint-sensitive tests run
    /// against a real database (unique indexes, FK cascade, PK semantics) that the
    /// InMemory provider silently ignores.
    /// </summary>
    public sealed class PostgresContainerFixture : IAsyncLifetime
    {
        private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18-alpine")
            .WithDatabase("authservice_test")
            .WithUsername("test")
            .WithPassword("test")
            .Build();

        public string ConnectionString => _container.GetConnectionString();

        public async ValueTask InitializeAsync()
        {
            await _container.StartAsync();

            await using var db = CreateDbContext();
            await db.Database.MigrateAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await _container.DisposeAsync();
        }

        /// <summary>A fresh context pointed at the container. Caller owns disposal.</summary>
        public AppDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(ConnectionString)
                .Options;
            return new AppDbContext(options);
        }
    }

    /// <summary>
    /// Collection definition so every Postgres-backed test class shares one container.
    /// </summary>
    [CollectionDefinition(Name)]
    public sealed class PostgresCollection : ICollectionFixture<PostgresContainerFixture>
    {
        public const string Name = "postgres";
    }
}
