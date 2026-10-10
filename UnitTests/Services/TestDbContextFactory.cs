using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OLTPSystem.DAL;

namespace UnitTests.Services
{
    /// <summary>
    /// Hands out a new TestNorthwindContext over a shared in-memory SQLite connection each time a service asks for one,
    /// mirroring how the app's IDbContextFactory gives every operation its own context.
    /// </summary>
    internal class TestDbContextFactory : IDbContextFactory<NorthwindContext>
    {
        private readonly SqliteConnection _connection;

        public TestDbContextFactory(SqliteConnection connection)
        {
            _connection = connection;
        }

        public NorthwindContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<NorthwindContext>().UseSqlite(_connection).Options;

            return new TestNorthwindContext(options);
        }
    }
}
