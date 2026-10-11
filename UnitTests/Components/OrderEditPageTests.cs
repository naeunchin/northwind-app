using AngleSharp.Dom;
using BlazorWebApp.Components.Pages.AppPages;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using OLTPSystem.Entities;
using OLTPSystem.ViewModels;

namespace UnitTests.Components
{
    /// <summary>
    /// Renders the order editor against a real OrderService backed by an in-memory SQLite database.
    /// </summary>
    public class OrderEditPageTests : DatabaseComponentTestBase
    {
        private const int SeededOrderID = 10248;

        /// <summary>
        /// Seeds lookup data plus order 10248, shipped by Speedy Express (ID 1), containing Chai (ID 1, qty 2) and Chang (ID 2, qty 5 at 10% off).
        /// </summary>
        private void SeedOrder()
        {
            using var context = CreateContext();
            context.Customers.Add(new Customer { CustomerID = "ALFKI", CompanyName = "Alfreds Futterkiste" });
            context.Employees.Add(new Employee { EmployeeID = 1, FirstName = "Nancy", LastName = "Davolio" });
            context.Shippers.AddRange(
                new Shipper { ShipperID = 1, CompanyName = "Speedy Express" },
                new Shipper { ShipperID = 2, CompanyName = "Federal Shipping" }
            );
            context.Products.AddRange(
                new Product { ProductID = 1, ProductName = "Chai", UnitPrice = 18.00m },
                new Product { ProductID = 2, ProductName = "Chang", UnitPrice = 19.00m },
                new Product { ProductID = 3, ProductName = "Aniseed Syrup", UnitPrice = 10.00m }
            );
            context.Orders.Add(new Order
            {
                OrderID = SeededOrderID,
                CustomerID = "ALFKI",
                EmployeeID = 1,
                ShipVia = 1,
                OrderDate = new DateTime(2026, 10, 1),
                Order_Details = new List<Order_Detail>
                {
                    new Order_Detail { ProductID = 1, UnitPrice = 18.00m, Quantity = 2, Discount = 0 },
                    new Order_Detail { ProductID = 2, UnitPrice = 19.00m, Quantity = 5, Discount = 0.1f }
                }
            });
            context.SaveChanges();
        }

        private IRenderedComponent<OrderEdit> RenderEditor(int orderID)
        {
            return Render<OrderEdit>(parameters => parameters.Add(p => p.OrderID, orderID));
        }

        private static IReadOnlyList<IElement> CartRows(IRenderedComponent<OrderEdit> cut) => cut.FindAll(".mud-table-body tr");

        private static IElement FindButton(IRenderedComponent<OrderEdit> cut, string text)
        {
            return cut.FindAll("button").Single(b => b.TextContent.Contains(text));
        }

        private static IRenderedComponent<MudSelect<int?>> FindShipperSelect(IRenderedComponent<OrderEdit> cut)
        {
            return cut.FindComponents<MudSelect<int?>>().Single(s => s.Instance.Label == "Logistical Distribution Carrier");
        }

        private IEnumerable<string> ShownSnackbarMessages() =>
            Services.GetRequiredService<ISnackbar>().ShownSnackbars.Select(s => s.Message);

        #region Loading

        [Fact]
        public void Render_NewOrder_ShowsAddTitleAndEmptyCart()
        {
            // Act
            var cut = RenderEditor(0);

            // Assert
            cut.Markup.Should().Contain("Add New Order");
            cut.Markup.Should().Contain("Empty Cart");
            FindButton(cut, "Save Changes").HasAttribute("disabled").Should().BeTrue();
        }

        [Fact]
        public void Render_ExistingOrder_LoadsLineItemsWithTotals()
        {
            // Arrange
            SeedOrder();

            // Act
            var cut = RenderEditor(SeededOrderID);

            // Assert
            cut.WaitForAssertion(() =>
            {
                cut.Markup.Should().Contain($"Edit Order: {SeededOrderID}");
                CartRows(cut).Should().HaveCount(2);
                CartRows(cut).Should().Contain(r => r.TextContent.Contains("Chai"));
                CartRows(cut).Should().Contain(r => r.TextContent.Contains("Chang"));
            });
        }

        [Fact]
        public void Render_ExistingOrderWithShipper_PreselectsThatShipper()
        {
            // Arrange
            SeedOrder();

            // Act
            var cut = RenderEditor(SeededOrderID);

            // Assert
            cut.WaitForAssertion(() =>
            {
                // bUnit doesn't render the option items outside the popover, so the displayed text can't be checked here
                FindShipperSelect(cut).Instance.Value.Should().Be(1);
            });
        }

        [Fact]
        public void Render_NonExistentOrder_ShowsNotFoundError()
        {
            // Act
            var cut = RenderEditor(999);

            // Assert
            cut.WaitForAssertion(() =>
                cut.Find(".mud-alert").TextContent.Should().Contain("No order was found with ID: 999"));
        }

        #endregion

        #region Cart

        [Fact]
        public async Task AddProductLine_NewProduct_AddsCartRowAndMarksFormChanged()
        {
            // Arrange
            SeedOrder();
            var cut = RenderEditor(SeededOrderID);
            cut.WaitForAssertion(() => CartRows(cut).Should().HaveCount(2));

            var syrup = new ProductView { ProductID = 3, ProductName = "Aniseed Syrup", UnitPrice = 10.00m };

            // Act
            await cut.InvokeAsync(() => cut.Instance.AddProductLine(syrup, 3, 0f));

            // Assert
            CartRows(cut).Should().HaveCount(3);
            CartRows(cut).Should().Contain(r => r.TextContent.Contains("Aniseed Syrup"));
            FindButton(cut, "Cancel").Should().NotBeNull();
            ShownSnackbarMessages().Should().Contain("Added 'Aniseed Syrup' to the order.");
        }

        [Fact]
        public async Task AddProductLine_ProductAlreadyInCart_IncreasesQuantityInsteadOfAddingRow()
        {
            // Arrange
            SeedOrder();
            var cut = RenderEditor(SeededOrderID);
            cut.WaitForAssertion(() => CartRows(cut).Should().HaveCount(2));

            var chai = new ProductView { ProductID = 1, ProductName = "Chai", UnitPrice = 18.00m };

            // Act
            await cut.InvokeAsync(() => cut.Instance.AddProductLine(chai, 3, 0f));

            // Assert - Chai started at quantity 2
            CartRows(cut).Should().HaveCount(2);
            var chaiRow = CartRows(cut).Single(r => r.TextContent.Contains("Chai"));
            chaiRow.QuerySelectorAll("td").Select(td => td.TextContent.Trim()).Should().Contain("5");
            ShownSnackbarMessages().Should().Contain("Updated quantity for Chai.");
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task AddProductLine_NonPositiveQuantity_RejectedWithWarning(short quantity)
        {
            // Arrange
            var cut = RenderEditor(0);
            var chai = new ProductView { ProductID = 1, ProductName = "Chai", UnitPrice = 18.00m };

            // Act
            await cut.InvokeAsync(() => cut.Instance.AddProductLine(chai, quantity, 0f));

            // Assert
            cut.Markup.Should().Contain("Empty Cart");
            ShownSnackbarMessages().Should().Contain("Purchase quantity must be greater than zero.");
        }

        [Fact]
        public async Task AddProductLine_NoProductSelected_RejectedWithWarning()
        {
            // Arrange
            var cut = RenderEditor(0);

            // Act
            await cut.InvokeAsync(() => cut.Instance.AddProductLine(new ProductView { ProductID = 0 }, 1, 0f));

            // Assert
            cut.Markup.Should().Contain("Empty Cart");
            ShownSnackbarMessages().Should().Contain("Please select a valid product.");
        }

        [Fact]
        public async Task DeleteButton_ClickedOnSecondRow_RemovesThatProductOnly()
        {
            // Arrange
            SeedOrder();
            var cut = RenderEditor(SeededOrderID);
            cut.WaitForAssertion(() => CartRows(cut).Should().HaveCount(2));

            // Act
            await ClickAsync(cut, () => CartRows(cut).Single(r => r.TextContent.Contains("Chang")).QuerySelector("button")!);

            // Assert
            CartRows(cut).Should().ContainSingle()
                .Which.TextContent.Should().Contain("Chai");
            ShownSnackbarMessages().Should().Contain("Removed 'Chang'.");
        }

        #endregion

        #region Saving & cancelling

        [Fact]
        public async Task Save_AfterRemovingLine_PersistsCartAndShowsFeedback()
        {
            // Arrange
            SeedOrder();
            var cut = RenderEditor(SeededOrderID);
            cut.WaitForAssertion(() => CartRows(cut).Should().HaveCount(2));

            // Act
            await ClickAsync(cut, () => CartRows(cut).Single(r => r.TextContent.Contains("Chang")).QuerySelector("button")!);
            cut.WaitForAssertion(() => FindButton(cut, "Save Changes").HasAttribute("disabled").Should().BeFalse());
            FindButton(cut, "Save Changes").Click();

            // Assert
            cut.WaitForAssertion(() =>
                cut.Find(".mud-alert").TextContent.Should().Contain($"Order {SeededOrderID} was successfully saved and logged."));

            using var verifyContext = CreateContext();
            verifyContext.Order_Details.Where(d => d.OrderID == SeededOrderID).Select(d => d.ProductID)
                .Should().Equal(1);
        }

        [Fact]
        public async Task Save_ChangedShipper_PersistsNewShipper()
        {
            // Arrange
            SeedOrder();
            var cut = RenderEditor(SeededOrderID);
            cut.WaitForAssertion(() => FindShipperSelect(cut).Instance.Value.Should().Be(1));

            // Act - pick Federal Shipping through the select's own path, which also marks the form as touched.
            // Cast to object so this selects by value; SelectOption(int) selects by index.
            var shipperSelect = FindShipperSelect(cut);
            await cut.InvokeAsync(() => shipperSelect.Instance.SelectOption((object)2));
            cut.WaitForAssertion(() => FindButton(cut, "Save Changes").HasAttribute("disabled").Should().BeFalse());
            FindButton(cut, "Save Changes").Click();

            // Assert
            cut.WaitForAssertion(() =>
                cut.Find(".mud-alert").TextContent.Should().Contain($"Order {SeededOrderID} was successfully saved and logged."));

            using var verifyContext = CreateContext();
            verifyContext.Orders.Single(o => o.OrderID == SeededOrderID).ShipVia.Should().Be(2);
        }

        [Fact]
        public async Task Save_NewOrderMissingCustomerAndEmployee_ShowsValidationErrorAndDoesNotSave()
        {
            // Arrange - adding a product enables Save even though the required selections are still empty
            SeedOrder();
            var cut = RenderEditor(0);
            var chai = new ProductView { ProductID = 1, ProductName = "Chai", UnitPrice = 18.00m };
            await cut.InvokeAsync(() => cut.Instance.AddProductLine(chai, 1, 0f));

            // Act
            FindButton(cut, "Save Changes").Click();

            // Assert
            cut.WaitForAssertion(() =>
                cut.Find(".mud-alert").TextContent.Should().Contain("Form Validation Failed")
                    .And.Contain("Please correct all highlighted input selections before saving."));
            ShownSnackbarMessages().Should().Contain("Cannot save order changes. Please check form validation errors.");

            using var verifyContext = CreateContext();
            verifyContext.Orders.Count().Should().Be(1);
        }

        [Fact]
        public void Close_NoChanges_NavigatesToOrdersWithoutPrompt()
        {
            // Arrange
            SeedOrder();
            var dialogProvider = RenderDialogProvider();
            var cut = RenderEditor(SeededOrderID);
            var navigationManager = Services.GetRequiredService<NavigationManager>();

            // Act
            FindButton(cut, "Close").Click();

            // Assert
            navigationManager.Uri.Should().EndWith("/orders");
            dialogProvider.FindAll(".mud-dialog").Should().BeEmpty();
        }

        [Fact]
        public async Task Cancel_WithChanges_DiscardChanges_NavigatesWithoutSaving()
        {
            // Arrange
            SeedOrder();
            var dialogProvider = RenderDialogProvider();
            var cut = RenderEditor(SeededOrderID);
            var navigationManager = Services.GetRequiredService<NavigationManager>();
            cut.WaitForAssertion(() => CartRows(cut).Should().HaveCount(2));
            await ClickAsync(cut, () => CartRows(cut).Single(r => r.TextContent.Contains("Chang")).QuerySelector("button")!);

            // Act
            FindButton(cut, "Cancel").Click();
            dialogProvider.WaitForAssertion(() => dialogProvider.Markup.Should().Contain("Confirm Cancel"));
            dialogProvider.FindAll("button").Single(b => b.TextContent.Contains("Discard Changes")).Click();

            // Assert
            cut.WaitForAssertion(() => navigationManager.Uri.Should().EndWith("/orders"));

            using var verifyContext = CreateContext();
            verifyContext.Order_Details.Count(d => d.OrderID == SeededOrderID).Should().Be(2);
        }

        #endregion
    }
}
