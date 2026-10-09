using BlazorWebApp.Components.Pages.AppPages;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OLTPSystem.BLL;
using OLTPSystem.DAL;
using OLTPSystem.Entities;
using UnitTests.Services;

namespace UnitTests.Components
{
    /// <summary>
    /// Renders the Orders page against a real OrderService backed by an in-memory SQLite database.
    /// </summary>
    public class OrdersPageTests : ComponentTestBase
    {
        private readonly SqliteConnection _connection;

        public OrdersPageTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            using (var context = CreateContext())
            {
                context.Database.EnsureCreated();
            }

            Services.AddScoped(_ => new OrderService(CreateContext()));

            RenderMudProviders();
        }

        protected override async ValueTask DisposeAsyncCore()
        {
            await base.DisposeAsyncCore();
            await _connection.DisposeAsync();
        }

        private NorthwindContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<NorthwindContext>().UseSqlite(_connection).Options;

            return new TestNorthwindContext(options);
        }

        private void SeedOrders()
        {
            using var context = CreateContext();
            context.Customers.AddRange(
                new Customer { CustomerID = "ALFKI", CompanyName = "Alfreds Futterkiste" },
                new Customer { CustomerID = "BOLID", CompanyName = "Bólido Comidas" }
            );
            context.Orders.AddRange(
                new Order { OrderID = 1, CustomerID = "ALFKI", OrderDate = new DateTime(2026, 1, 10) },
                new Order { OrderID = 2, CustomerID = "BOLID", OrderDate = new DateTime(2026, 5, 20) }
            );
            context.SaveChanges();
        }

        private static void Search(IRenderedComponent<Orders> cut, string searchTerm)
        {
            cut.Find("input").Change(searchTerm);
            cut.FindAll("button").Single(b => b.TextContent.Contains("Fetch")).Click();
        }

        [Fact]
        public void Render_NoOrdersExist_ShowsEmptyState()
        {
            // Act
            var cut = Render<Orders>();

            // Assert
            cut.Markup.Should().Contain("No Orders Found");
            cut.FindAll(".mud-table-body tr").Should().BeEmpty();
        }

        [Fact]
        public void Render_OrdersExist_ShowsOrdersInGrid()
        {
            // Arrange
            SeedOrders();

            // Act
            var cut = Render<Orders>();

            // Assert
            cut.WaitForAssertion(() => cut.FindAll(".mud-table-body tr").Should().HaveCount(2));
            cut.Markup.Should().NotContain("No Orders Found");
        }

        [Fact]
        public void Fetch_MatchingSearchTerm_FiltersGridAndShowsFeedback()
        {
            // Arrange
            SeedOrders();
            var cut = Render<Orders>();

            // Act
            Search(cut, "alf");

            // Assert
            cut.WaitForAssertion(() =>
            {
                var rows = cut.FindAll(".mud-table-body tr");
                rows.Should().ContainSingle();
                rows[0].TextContent.Should().Contain("ALFKI");
                cut.Find(".mud-alert").TextContent.Should().Contain("Found 1 orders matching the search term.");
            });
        }

        [Fact]
        public void Fetch_NoMatches_ShowsErrorAlert()
        {
            // Arrange
            SeedOrders();
            var cut = Render<Orders>();

            // Act
            Search(cut, "ZZZZZ");

            // Assert
            cut.WaitForAssertion(() =>
            {
                var alert = cut.Find(".mud-alert");
                alert.TextContent.Should().Contain("No results found.")
                    .And.Contain("No orders matched your search criteria.");
            });
        }

        [Fact]
        public void NewButton_Click_NavigatesToNewOrderEditor()
        {
            // Arrange
            var cut = Render<Orders>();
            var navigationManager = Services.GetRequiredService<NavigationManager>();

            // Act
            cut.FindAll("button").Single(b => b.TextContent.Contains("New")).Click();

            // Assert
            navigationManager.Uri.Should().EndWith("/OrderEdit/0");
        }
    }
}
