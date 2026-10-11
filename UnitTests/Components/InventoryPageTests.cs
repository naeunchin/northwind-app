using BlazorWebApp.Components.Pages.AppPages;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using OLTPSystem.Entities;

namespace UnitTests.Components
{
    /// <summary>
    /// Renders the Inventory page against a real ProductService backed by an in-memory SQLite database.
    /// </summary>
    public class InventoryPageTests : DatabaseComponentTestBase
    {
        private void SeedProducts()
        {
            using var context = CreateContext();
            context.Products.AddRange(
                new Product { ProductID = 1, ProductName = "Chai" },
                new Product { ProductID = 2, ProductName = "Chang" },
                new Product { ProductID = 3, ProductName = "Ikura", Discontinued = true }
            );
            context.SaveChanges();
        }

        private static void Search(IRenderedComponent<Inventory> cut, string searchTerm)
        {
            cut.Find("input").Change(searchTerm);
            cut.FindAll("button").Single(b => b.TextContent.Contains("Get")).Click();
        }

        [Fact]
        public void Render_ProductsExist_LoadsAllProductsIntoGrid()
        {
            // Arrange
            SeedProducts();

            // Act
            var cut = Render<Inventory>();

            // Assert
            cut.WaitForAssertion(() => cut.FindAll(".mud-table-body tr").Should().HaveCount(3));
            cut.Markup.Should().NotContain("No Inventory Data to Display.");
        }

        [Fact]
        public void Render_DiscontinuedProduct_ShowsDiscontinuedStatus()
        {
            // Arrange
            SeedProducts();

            // Act
            var cut = Render<Inventory>();

            // Assert
            cut.WaitForAssertion(() =>
            {
                var ikuraRow = cut.FindAll(".mud-table-body tr").Single(r => r.TextContent.Contains("Ikura"));
                ikuraRow.TextContent.Should().Contain("Discontinued");

                var chaiRow = cut.FindAll(".mud-table-body tr").Single(r => r.TextContent.Contains("Chai"));
                chaiRow.TextContent.Should().Contain("Active");
            });
        }

        [Fact]
        public void Render_NoProductsExist_ShowsLoadErrorAndEmptyState()
        {
            // Act
            var cut = Render<Inventory>();

            // Assert
            cut.WaitForAssertion(() =>
            {
                cut.Find(".mud-alert").TextContent.Should().Contain("Failed to load the inventory")
                    .And.Contain("No product records were found in the database.");
                cut.Markup.Should().Contain("No Inventory Data to Display.");
            });
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Get_EmptySearchTerm_ShowsValidationError(string searchTerm)
        {
            // Arrange
            SeedProducts();
            var cut = Render<Inventory>();

            // Act
            Search(cut, searchTerm);

            // Assert
            cut.WaitForAssertion(() =>
            {
                cut.Find(".mud-alert").TextContent.Should().Contain("Search Validation Failed")
                    .And.Contain("Please enter a product name or a product ID to search.");
                cut.FindAll(".mud-table-body tr").Should().BeEmpty();
            });
        }

        [Fact]
        public void Get_PartialName_FiltersGridAndShowsFeedback()
        {
            // Arrange
            SeedProducts();
            var cut = Render<Inventory>();

            // Act
            Search(cut, "cha");

            // Assert
            cut.WaitForAssertion(() =>
            {
                var rows = cut.FindAll(".mud-table-body tr");
                rows.Should().HaveCount(2);
                rows.Should().NotContain(r => r.TextContent.Contains("Ikura"));
                cut.Find(".mud-alert").TextContent.Should().Contain("Successfully loaded 2 inventory records.");
            });
        }

        [Fact]
        public void Get_NumericSearchTerm_LooksUpProductByID()
        {
            // Arrange
            SeedProducts();
            var cut = Render<Inventory>();

            // Act
            Search(cut, "2");

            // Assert
            cut.WaitForAssertion(() =>
            {
                var rows = cut.FindAll(".mud-table-body tr");
                rows.Should().ContainSingle();
                rows[0].TextContent.Should().Contain("Chang");
            });
        }

        [Fact]
        public void Get_NoMatches_ShowsErrorDetails()
        {
            // Arrange
            SeedProducts();
            var cut = Render<Inventory>();

            // Act
            Search(cut, "ZZZZZ");

            // Assert
            cut.WaitForAssertion(() =>
            {
                cut.Find(".mud-alert").TextContent.Should().Contain("No products were located containing the term: 'ZZZZZ'.");
                cut.Markup.Should().Contain("No Inventory Data to Display.");
            });
        }

        [Fact]
        public void EditButton_Click_NavigatesToThatProductsEditor()
        {
            // Arrange
            SeedProducts();
            var cut = Render<Inventory>();
            var navigationManager = Services.GetRequiredService<NavigationManager>();

            // Act
            cut.WaitForAssertion(() => cut.FindAll(".mud-table-body tr").Should().HaveCount(3));
            var changRow = cut.FindAll(".mud-table-body tr").Single(r => r.TextContent.Contains("Chang"));
            changRow.QuerySelector("button")!.Click();

            // Assert
            navigationManager.Uri.Should().EndWith("/ProductEdit/2");
        }

        [Fact]
        public void NewButton_Click_NavigatesToNewProductEditor()
        {
            // Arrange
            var cut = Render<Inventory>();
            var navigationManager = Services.GetRequiredService<NavigationManager>();

            // Act
            cut.FindAll("button").Single(b => b.TextContent.Contains("New")).Click();

            // Assert
            navigationManager.Uri.Should().EndWith("/ProductEdit/0");
        }
    }
}
