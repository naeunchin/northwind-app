using FluentAssertions;
using IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using OLTPSystem.BLL;
using OLTPSystem.Entities;
using OLTPSystem.ViewModels;

namespace IntegrationTests.Services
{
    [Collection(nameof(DatabaseCollection))]
    public class ProductServiceIntegrationTests : IntegrationTestBase
    {
        private readonly Category _beverages = new() { CategoryName = "Beverages" };
        private readonly Supplier _exoticLiquids = new() { CompanyName = "Exotic Liquids" };

        public ProductServiceIntegrationTests(DatabaseFixture database) : base(database)
        {
        }

        /// <summary>
        /// Builds a ProductView that passes every AddEditProduct validation rule, using the seeded category and supplier.
        /// </summary>
        private ProductView CreateValidProductView(int productID = 0, string productName = "Chai") => new ProductView
        {
            ProductID = productID,
            ProductName = productName,
            CategoryID = _beverages.CategoryID,
            SupplierID = _exoticLiquids.SupplierID,
            QuantityPerUnit = "10 boxes x 20 bags",
            UnitPrice = 18.00m,
            UnitsInStock = 39,
            UnitsOnOrder = 0,
            ReorderLevel = 10,
            Discontinued = false
        };

        #region AddEditProduct tests

        [Fact]
        public async Task AddEditProduct_NewProduct_GeneratesIdentityAndPersists()
        {
            // Arrange
            await SeedAsync(_beverages, _exoticLiquids);

            await using var context = CreateContext();
            var service = new ProductService(context);

            // Act
            var result = await service.AddEditProduct(CreateValidProductView());

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.ProductID.Should().BeGreaterThan(0);
            result.Value.CategoryName.Should().Be("Beverages");
            result.Value.SupplierCompanyName.Should().Be("Exotic Liquids");

            await using var verifyContext = CreateContext();
            var savedProduct = await verifyContext.Products.SingleAsync();
            savedProduct.ProductID.Should().Be(result.Value.ProductID);
            savedProduct.ProductName.Should().Be("Chai");
            savedProduct.UnitPrice.Should().Be(18.00m);
        }

        [Fact]
        public async Task AddEditProduct_ExistingProduct_UpdatesFields()
        {
            // Arrange
            await SeedAsync(_beverages, _exoticLiquids);
            var existing = new Product { ProductName = "Chai", CategoryID = _beverages.CategoryID, SupplierID = _exoticLiquids.SupplierID, UnitPrice = 18.00m };
            await SeedAsync(existing);

            await using var context = CreateContext();
            var service = new ProductService(context);

            var editProduct = CreateValidProductView(productID: existing.ProductID);
            editProduct.UnitPrice = 25.50m;
            editProduct.Discontinued = true;

            // Act
            var result = await service.AddEditProduct(editProduct);

            // Assert
            result.IsSuccess.Should().BeTrue();

            await using var verifyContext = CreateContext();
            var savedProduct = await verifyContext.Products.SingleAsync();
            savedProduct.UnitPrice.Should().Be(25.50m);
            savedProduct.Discontinued.Should().BeTrue();
        }

        [Fact]
        public async Task AddEditProduct_DuplicateNameDifferentCase_ReturnsDuplicateDataWarning()
        {
            // Arrange
            await SeedAsync(_beverages, _exoticLiquids);
            await SeedAsync(new Product { ProductName = "Chai", CategoryID = _beverages.CategoryID, SupplierID = _exoticLiquids.SupplierID });

            await using var context = CreateContext();
            var service = new ProductService(context);

            // Act
            var result = await service.AddEditProduct(CreateValidProductView(productName: "CHAI"));

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Errors.Should().ContainSingle(e => e.Code == "Duplicate Data Warning");
        }

        [Fact]
        public async Task AddEditProduct_CategoryDoesNotExist_ReturnsForeignKeyError()
        {
            // Arrange
            await SeedAsync(_beverages, _exoticLiquids);

            await using var context = CreateContext();
            var service = new ProductService(context);

            var editProduct = CreateValidProductView();
            editProduct.CategoryID = 999;

            // Act
            var result = await service.AddEditProduct(editProduct);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Errors.Should().ContainSingle(e => e.Code == "Error Saving Changes")
                .Which.Message.Should().Contain("FK_Products_Categories");

            await using var verifyContext = CreateContext();
            (await verifyContext.Products.AnyAsync()).Should().BeFalse();
        }

        [Fact]
        public async Task AddEditProduct_NameLongerThanColumn_ReturnsValidationErrorBeforeSaving()
        {
            // Arrange
            await SeedAsync(_beverages, _exoticLiquids);

            await using var context = CreateContext();
            var service = new ProductService(context);

            // Act
            var result = await service.AddEditProduct(CreateValidProductView(productName: new string('A', ProductService.ProductNameMaxLength + 1)));

            // Assert - caught by validation rather than SQL Server's truncation error
            result.IsFailure.Should().BeTrue();
            result.Errors.Should().ContainSingle().Which.Code.Should().Be("Invalid Value");

            await using var verifyContext = CreateContext();
            (await verifyContext.Products.AnyAsync()).Should().BeFalse();
        }

        [Fact]
        public async Task AddEditProduct_NameAtMaxLength_FitsColumn()
        {
            // Arrange - guards against ProductNameMaxLength drifting from the nvarchar(40) column
            await SeedAsync(_beverages, _exoticLiquids);

            await using var context = CreateContext();
            var service = new ProductService(context);

            var productName = new string('A', ProductService.ProductNameMaxLength);

            // Act
            var result = await service.AddEditProduct(CreateValidProductView(productName: productName));

            // Assert
            result.IsSuccess.Should().BeTrue();

            await using var verifyContext = CreateContext();
            (await verifyContext.Products.SingleAsync()).ProductName.Should().Be(productName);
        }

        #endregion

        #region Query tests

        [Fact]
        public async Task GetProductsAsync_MixedRelationships_ReturnsJoinedNamesAndPlaceholders()
        {
            // Arrange
            await SeedAsync(_beverages, _exoticLiquids);
            await SeedAsync(
                new Product { ProductName = "Chai", CategoryID = _beverages.CategoryID, SupplierID = _exoticLiquids.SupplierID },
                new Product { ProductName = "Aniseed Syrup" });

            await using var context = CreateContext();
            var service = new ProductService(context);

            // Act
            var result = await service.GetProductsAsync();

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().HaveCount(2);

            var aniseed = result.Value[0];
            aniseed.ProductName.Should().Be("Aniseed Syrup");
            aniseed.CategoryName.Should().Be("Uncategorized");
            aniseed.SupplierCompanyName.Should().Be("No Supplier Listed");

            var chai = result.Value[1];
            chai.CategoryName.Should().Be("Beverages");
            chai.SupplierCompanyName.Should().Be("Exotic Liquids");
        }

        [Fact]
        public async Task LookupProducts_PartialNameDifferentCase_ReturnsMatches()
        {
            // Arrange
            await SeedAsync(
                new Product { ProductName = "Chang" },
                new Product { ProductName = "Chai" },
                new Product { ProductName = "Ikura" });

            await using var context = CreateContext();
            var service = new ProductService(context);

            // Act
            var result = await service.LookupProducts("CHA");

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Select(p => p.ProductName).Should().Equal("Chai", "Chang");
        }

        [Fact]
        public async Task GetCategoriesAndSuppliers_ReturnOrderedByName()
        {
            // Arrange
            await SeedAsync(
                new Category { CategoryName = "Seafood" },
                new Category { CategoryName = "Beverages" },
                new Supplier { CompanyName = "Tokyo Traders" },
                new Supplier { CompanyName = "Exotic Liquids" });

            await using var context = CreateContext();
            var service = new ProductService(context);

            // Act
            var categories = await service.GetCategoriesAsync();
            var suppliers = await service.GetSuppliersAsync();

            // Assert
            categories.Value.Select(c => c.CategoryName).Should().Equal("Beverages", "Seafood");
            suppliers.Value.Select(s => s.SupplierCompanyName).Should().Equal("Exotic Liquids", "Tokyo Traders");
        }

        #endregion
    }
}
