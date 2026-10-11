using BlazorWebApp.Components.Pages.AppPages;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using OLTPSystem.ViewModels;

namespace UnitTests.Components
{
    /// <summary>
    /// Opens AddProductDialog through IDialogService, the way OrderEdit does, and checks the result it returns.
    /// </summary>
    public class AddProductDialogTests : ComponentTestBase
    {
        private readonly List<ProductView> _productOptions =
        [
            new ProductView { ProductID = 1, ProductName = "Chai", UnitPrice = 18.00m },
            new ProductView { ProductID = 2, ProductName = "Chang", UnitPrice = 19.00m }
        ];

        private readonly IRenderedComponent<MudDialogProvider> _dialogProvider;

        public AddProductDialogTests()
        {
            RenderMudProviders();
            _dialogProvider = RenderDialogProvider();
        }

        private async Task<IDialogReference> OpenDialogAsync()
        {
            var dialogService = Services.GetRequiredService<IDialogService>();
            var parameters = new DialogParameters { ["ProductOptions"] = _productOptions };

            var reference = await _dialogProvider.InvokeAsync(() => dialogService.ShowAsync<AddProductDialog>("Add Products", parameters));
            _dialogProvider.WaitForAssertion(() => _dialogProvider.FindAll(".mud-dialog").Should().ContainSingle());

            return reference;
        }

        /// <summary>
        /// Selects a product the way @bind-Value would after the user picks it from the dropdown.
        /// </summary>
        private Task SelectProductAsync(int productID)
        {
            var select = _dialogProvider.FindComponent<MudSelect<int>>();
            return _dialogProvider.InvokeAsync(() => select.Instance.ValueChanged.InvokeAsync(productID));
        }

        private void SetNumericField(string label, string value)
        {
            _dialogProvider.FindAll(".mud-input-control")
                           .First(c => c.QuerySelector("label")?.TextContent.Contains(label) == true)
                           .QuerySelector("input")!
                           .Change(value);
        }

        private void ClickButton(string text)
        {
            _dialogProvider.FindAll("button").Single(b => b.TextContent.Contains(text)).Click();
        }

        [Fact]
        public async Task Submit_ProductQuantityAndDiscount_ReturnsTupleWithDiscountAsFraction()
        {
            // Arrange
            var reference = await OpenDialogAsync();
            await SelectProductAsync(2);
            SetNumericField("Quantity Count", "3");
            SetNumericField("Discount Rate", "15");

            // Act
            ClickButton("Add Product Item");
            var result = await reference.Result;

            // Assert
            result!.Canceled.Should().BeFalse();
            var (product, quantity, discount) = (Tuple<ProductView, short, float>)result.Data!;
            product.ProductName.Should().Be("Chang");
            quantity.Should().Be(3);
            discount.Should().BeApproximately(0.15f, 0.0001f);
        }

        [Fact]
        public async Task Submit_DefaultsOnly_ReturnsQuantityOneAndNoDiscount()
        {
            // Arrange
            var reference = await OpenDialogAsync();
            await SelectProductAsync(1);

            // Act
            ClickButton("Add Product Item");
            var result = await reference.Result;

            // Assert
            var (product, quantity, discount) = (Tuple<ProductView, short, float>)result!.Data!;
            product.ProductID.Should().Be(1);
            quantity.Should().Be(1);
            discount.Should().Be(0f);
        }

        [Fact]
        public async Task Submit_NoProductSelected_KeepsDialogOpen()
        {
            // Arrange
            var reference = await OpenDialogAsync();

            // Act
            ClickButton("Add Product Item");

            // Assert
            _dialogProvider.FindAll(".mud-dialog").Should().ContainSingle();
            reference.Result.IsCompleted.Should().BeFalse();
        }

        [Fact]
        public async Task Cancel_ClosesDialogWithCanceledResult()
        {
            // Arrange
            var reference = await OpenDialogAsync();

            // Act
            ClickButton("Cancel");
            var result = await reference.Result;

            // Assert
            result!.Canceled.Should().BeTrue();
            _dialogProvider.WaitForAssertion(() => _dialogProvider.FindAll(".mud-dialog").Should().BeEmpty());
        }

        [Fact]
        public async Task Render_ListsEveryProductOption()
        {
            // Arrange
            await OpenDialogAsync();

            // Act
            var select = _dialogProvider.FindComponent<MudSelect<int>>();

            // Assert - placeholder entry plus one per product
            var items = _dialogProvider.FindComponents<MudSelectItem<int>>().Select(i => i.Instance.Value);
            items.Should().Equal(0, 1, 2);
            select.Instance.Value.Should().Be(0);
        }
    }
}
