using AuthService.Data;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Tests.Fixtures
{
    /// <summary>
    /// Base class for unit tests that need an AppDbContext.
    /// Centralises DB creation so switching from InMemory to a real database
    /// (e.g. Testcontainers + PostgreSQL) only requires changing this one place.
    /// </summary>
    public abstract class DbTestBase : IDisposable
    {
        protected AppDbContext Db { get; }

        protected DbTestBase()
        {
            Db = CreateDbContext();
        }

        /// <summary>
        /// Creates a fresh, isolated AppDbContext for each test class.
        /// Override this if you want to use a different database provider.
        /// </summary>
        private static AppDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new AppDbContext(options);
        }

        public void Dispose()
        {
            Db.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
