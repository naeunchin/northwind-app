using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OLTPSystem.BLL;
using OLTPSystem.DAL;
using UnitTests.Services;

namespace UnitTests.Components
{
    /// <summary>
    /// Base class for page tests that render against the real OrderService and ProductService, backed by an in-memory SQLite database.
    /// Registers the services the same way the app does, so pages resolve them through dependency injection.
    /// </summary>
    public abstract class DatabaseComponentTestBase : ComponentTestBase
    {
        private readonly SqliteConnection _connection;
        private readonly TestDbContextFactory _contextFactory;

        protected DatabaseComponentTestBase()
        {
            // The in-memory database lives as long as this connection stays open
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            _contextFactory = new TestDbContextFactory(_connection);

            using (var context = CreateContext())
            {
                context.Database.EnsureCreated();
            }

            Services.AddSingleton<IDbContextFactory<NorthwindContext>>(_contextFactory);
            Services.AddScoped<OrderService>();
            Services.AddScoped<ProductService>();

            RenderMudProviders();
        }

        protected override async ValueTask DisposeAsyncCore()
        {
            await base.DisposeAsyncCore();
            await _connection.DisposeAsync();
        }

        /// <summary>
        /// Creates an independent DbContext for seeding data and verifying what pages saved.
        /// </summary>
        protected NorthwindContext CreateContext() => _contextFactory.CreateDbContext();
    }
}
