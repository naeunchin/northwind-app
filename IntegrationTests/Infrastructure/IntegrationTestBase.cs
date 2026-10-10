using Microsoft.EntityFrameworkCore;
using OLTPSystem.DAL;

namespace IntegrationTests.Infrastructure
{
    /// <summary>
    /// Base class for tests against the SQL Server container. Resets the database before each test.
    /// Derived classes must be marked [Collection(nameof(DatabaseCollection))].
    /// </summary>
    public abstract class IntegrationTestBase : IAsyncLifetime
    {
        private readonly DatabaseFixture _database;

        protected IntegrationTestBase(DatabaseFixture database)
        {
            _database = database;
        }

        public Task InitializeAsync() => _database.ResetAsync();

        public Task DisposeAsync() => Task.CompletedTask;

        /// <summary>
        /// Factory to pass to services under test; each service call gets its own context, as in the app.
        /// </summary>
        protected IDbContextFactory<NorthwindContext> ContextFactory => _database;

        /// <summary>
        /// Creates a fresh DbContext so reads hit the database rather than the EF Core change tracker.
        /// </summary>
        protected NorthwindContext CreateContext() => _database.CreateDbContext();

        /// <summary>
        /// Saves the given entities in their own context. SQL Server generates identity keys,
        /// so the entities' ID properties are populated once this returns.
        /// </summary>
        protected async Task SeedAsync(params object[] entities)
        {
            await using var context = CreateContext();
            context.AddRange(entities);
            await context.SaveChangesAsync();
        }
    }
}
