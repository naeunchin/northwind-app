using AngleSharp.Dom;
using BlazorWebApp.Components.Pages.AppPages;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OLTPSystem.Entities;

namespace UnitTests.Components
{
    /// <summary>
    /// Renders the product editor against a real ProductService backed by an in-memory SQLite database.
    /// </summary>
    public class InventoryEditPageTests : DatabaseComponentTestBase
    {
        private void SeedProduct()
        {
            using var context = CreateContext();
            context.Categories.Add(new Category { CategoryID = 1, CategoryName = "Beverages" });
            context.Suppliers.Add(new Supplier { SupplierID = 1, CompanyName = "Exotic Liquids" });
            context.Products.AddRange(
                new Product { ProductID = 1, ProductName = "Chai", CategoryID = 1, SupplierID = 1, UnitPrice = 18.00m, UnitsInStock = 39 },
                new Product { ProductID = 2, ProductName = "Chang", CategoryID = 1, SupplierID = 1, UnitPrice = 19.00m, UnitsInStock = 17 }
            );
            context.SaveChanges();
        }

        private IRenderedComponent<InventoryEdit> RenderEditor(int productID)
        {
            return Render<InventoryEdit>(parameters => parameters.Add(p => p.ProductID, productID));
        }

        /// <summary>
        /// Finds the input element of the MudBlazor field whose label contains the given text.
        /// </summary>
        private static IElement FindInput(IRenderedComponent<InventoryEdit> cut, string label)
        {
            return cut.FindAll(".mud-input-control")
                      .First(c => c.QuerySelector("label")?.TextContent.Contains(label) == true)
                      .QuerySelector("input")!;
        }

        private static IElement FindButton(IRenderedComponent<InventoryEdit> cut, string text)
        {
            return cut.FindAll("button").Single(b => b.TextContent.Contains(text));
        }

        #region Loading

        [Fact]
        public void Render_NewProduct_ShowsAddTitleAndDisabledSave()
        {
            // Act
            var cut = RenderEditor(0);

            // Assert
            cut.Markup.Should().Contain("Add New Product");
            FindButton(cut, "Save Record").HasAttribute("disabled").Should().BeTrue();
            FindButton(cut, "Close").Should().NotBeNull();
        }

        [Fact]
        public void Render_ExistingProduct_LoadsProductIntoForm()
        {
            // Arrange
            SeedProduct();

            // Act
            var cut = RenderEditor(1);

            // Assert
            cut.WaitForAssertion(() =>
            {
                cut.Markup.Should().Contain("Edit Product:Chai");
                FindInput(cut, "Product Name").GetAttribute("value").Should().Be("Chai");
                FindInput(cut, "Physical Units In Stock").GetAttribute("value").Should().Be("39");
            });
        }

        [Fact]
        public void Render_NonExistentProduct_ShowsNotFoundError()
        {
            // Act
            var cut = RenderEditor(999);

            // Assert
            cut.WaitForAssertion(() =>
                cut.Find(".mud-alert").TextContent.Should().Contain("No product was found with ID: 999"));
        }

        #endregion

        #region Saving

        [Fact]
        public void Save_EditedName_PersistsChangeAndShowsFeedback()
        {
            // Arrange
            SeedProduct();
            var cut = RenderEditor(1);
            cut.WaitForAssertion(() => FindInput(cut, "Product Name").GetAttribute("value").Should().Be("Chai"));

            // Act
            FindInput(cut, "Product Name").Input("Chai Tea");
            cut.WaitForAssertion(() => FindButton(cut, "Save Record").HasAttribute("disabled").Should().BeFalse());
            FindButton(cut, "Save Record").Click();

            // Assert
            cut.WaitForAssertion(() =>
                cut.Find(".mud-alert").TextContent.Should().Contain("Product Chai Tea was successfully saved."));

            using var verifyContext = CreateContext();
            verifyContext.Products.Single(p => p.ProductID == 1).ProductName.Should().Be("Chai Tea");
        }

        [Fact]
        public void Save_DuplicateName_ShowsServiceErrorAndDoesNotSave()
        {
            // Arrange
            SeedProduct();
            var cut = RenderEditor(1);
            cut.WaitForAssertion(() => FindInput(cut, "Product Name").GetAttribute("value").Should().Be("Chai"));

            // Act - rename Chai to the name of the other seeded product
            FindInput(cut, "Product Name").Input("chang");
            cut.WaitForAssertion(() => FindButton(cut, "Save Record").HasAttribute("disabled").Should().BeFalse());
            FindButton(cut, "Save Record").Click();

            // Assert
            cut.WaitForAssertion(() =>
                cut.Find(".mud-alert").TextContent.Should().Contain("An inventory item named chang already exists"));

            using var verifyContext = CreateContext();
            verifyContext.Products.Single(p => p.ProductID == 1).ProductName.Should().Be("Chai");
        }

        #endregion

        #region Cancelling

        [Fact]
        public void Close_NoChanges_NavigatesToInventoryWithoutPrompt()
        {
            // Arrange
            SeedProduct();
            var dialogProvider = RenderDialogProvider();
            var cut = RenderEditor(1);
            var navigationManager = Services.GetRequiredService<NavigationManager>();

            // Act
            FindButton(cut, "Close").Click();

            // Assert
            navigationManager.Uri.Should().EndWith("/inventory");
            dialogProvider.FindAll(".mud-dialog").Should().BeEmpty();
        }

        [Fact]
        public void Cancel_WithChanges_StayOnPage_KeepsEditorOpen()
        {
            // Arrange
            SeedProduct();
            var dialogProvider = RenderDialogProvider();
            var cut = RenderEditor(1);
            var navigationManager = Services.GetRequiredService<NavigationManager>();
            var startUri = navigationManager.Uri;

            cut.WaitForAssertion(() => FindInput(cut, "Product Name").GetAttribute("value").Should().Be("Chai"));
            FindInput(cut, "Product Name").Input("Chai Tea");

            // Act
            cut.WaitForAssertion(() => FindButton(cut, "Cancel").Should().NotBeNull());
            FindButton(cut, "Cancel").Click();
            dialogProvider.WaitForAssertion(() => dialogProvider.Markup.Should().Contain("Confirm Cancel"));
            dialogProvider.FindAll("button").Single(b => b.TextContent.Contains("Stay on Page")).Click();

            // Assert
            dialogProvider.WaitForAssertion(() => dialogProvider.FindAll(".mud-dialog").Should().BeEmpty());
            navigationManager.Uri.Should().Be(startUri);
        }

        [Fact]
        public void Cancel_WithChanges_DiscardChanges_NavigatesToInventory()
        {
            // Arrange
            SeedProduct();
            var dialogProvider = RenderDialogProvider();
            var cut = RenderEditor(1);
            var navigationManager = Services.GetRequiredService<NavigationManager>();

            cut.WaitForAssertion(() => FindInput(cut, "Product Name").GetAttribute("value").Should().Be("Chai"));
            FindInput(cut, "Product Name").Input("Chai Tea");

            // Act
            cut.WaitForAssertion(() => FindButton(cut, "Cancel").Should().NotBeNull());
            FindButton(cut, "Cancel").Click();
            dialogProvider.WaitForAssertion(() => dialogProvider.Markup.Should().Contain("Confirm Cancel"));
            dialogProvider.FindAll("button").Single(b => b.TextContent.Contains("Discard Changes")).Click();

            // Assert
            cut.WaitForAssertion(() => navigationManager.Uri.Should().EndWith("/inventory"));

            using var verifyContext = CreateContext();
            verifyContext.Products.Single(p => p.ProductID == 1).ProductName.Should().Be("Chai");
        }

        #endregion
    }
}
