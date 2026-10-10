using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using OLTPSystem.DAL;
using Respawn;
using Testcontainers.MsSql;

namespace IntegrationTests.Infrastructure
{
    /// <summary>
    /// Starts one SQL Server container for the whole test run, builds the Northwind schema from the EF model,
    /// and resets the data between tests with Respawn.
    /// </summary>
    public class DatabaseFixture : IAsyncLifetime
    {
        private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

        private Respawner _respawner = null!;

        public string ConnectionString => _container.GetConnectionString();

        public NorthwindContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<NorthwindContext>().UseSqlServer(ConnectionString).Options;

            return new NorthwindContext(options);
        }

        public async Task InitializeAsync()
        {
            await _container.StartAsync();

            // The project has no migrations, so the schema is created straight from the scaffolded model
            await using (var context = CreateContext())
            {
                await context.Database.EnsureCreatedAsync();
            }

            // Reseed identity columns so every test starts from the same generated IDs
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
            {
                DbAdapter = DbAdapter.SqlServer,
                WithReseed = true
            });
        }

        /// <summary>
        /// Deletes all rows from every table, leaving the schema in place.
        /// </summary>
        public async Task ResetAsync()
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            await _respawner.ResetAsync(connection);
        }

        public Task DisposeAsync() => _container.DisposeAsync().AsTask();
    }

    /// <summary>
    /// Shares a single DatabaseFixture across every test class in the collection. Tests in one collection run serially,
    /// so they never reset the database underneath each other.
    /// </summary>
    [CollectionDefinition(nameof(DatabaseCollection))]
    public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
    {
    }
}
