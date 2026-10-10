using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using OLTPSystem.BLL;
using OLTPSystem.DAL;
using OLTPSystem.Entities;
using OLTPSystem.ViewModels;

namespace UnitTests.Services
{
    public class ProductServiceTests : IDisposable
    {
        private readonly NorthwindContext _context;
        private readonly SqliteConnection _connection;

        public ProductServiceTests()
        {
            // Creating & establishing an in-memory SQLite connection
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            // Configuring DbContext to use the open SQLite connection
            var options = new DbContextOptionsBuilder<NorthwindContext>().UseSqlite(_connection).Options;

            _context = new TestNorthwindContext(options);

            // Create DB schema (tables & keys)
            _context.Database.EnsureCreated();
        }

        public void Dispose()
        {
            // Destroying the in-memory DB
            _context.Dispose();
            _connection.Close();
            _connection.Dispose();
        }

        /// <summary>
        /// Creates an independent DbContext instance over the same SQLite connection to verify database state without any cached entity from memory by the EF Core change tracker.
        /// </summary>
        /// <returns>DbContext instance</returns>
        private NorthwindContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<NorthwindContext>().UseSqlite(_connection).Options;

            return new TestNorthwindContext(options);
        }

        /// <summary>
        /// Seeds a category (ID 1, "Beverages") and a supplier (ID 1, "Exotic Liquids") that products can reference.
        /// </summary>
        private async Task SeedCategoryAndSupplierAsync()
        {
            using var seedContext = CreateContext();
            seedContext.Categories.Add(new Category { CategoryID = 1, CategoryName = "Beverages" });
            seedContext.Suppliers.Add(new Supplier { SupplierID = 1, CompanyName = "Exotic Liquids" });
            await seedContext.SaveChangesAsync();
        }

        /// <summary>
        /// Builds a ProductView that passes every AddEditProduct validation rule, for tests to tweak.
        /// </summary>
        private static ProductView CreateValidProductView(int productID = 0, string productName = "Chai") => new ProductView
        {
            ProductID = productID,
            ProductName = productName,
            CategoryID = 1,
            SupplierID = 1,
            QuantityPerUnit = "10 boxes x 20 bags",
            UnitPrice = 18.00m,
            UnitsInStock = 39,
            UnitsOnOrder = 0,
            ReorderLevel = 10,
            Discontinued = false
        };

        [Fact]
        public void Constructor_NullContext_ThrowsArgumentNullException()
        {
            // Act
            Action act = () => new ProductService(null!);

            // Assert
            act.Should().Throw<ArgumentNullException>().WithParameterName("context");
        }

        #region GetProductsAsync tests

        [Fact]
        public async Task GetProductsAsync_WhenNoProductsExist_ReturnsNoRecordsFoundError()
        {
            // Arrange
            using var context = CreateContext();
            var service = new ProductService(context);

            // Act
            var result = await service.GetProductsAsync();

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Errors.Should().ContainSingle(e => e.Code == "No records found");
        }

        [Fact]
        public async Task GetProductsAsync_WhenProductsExist_ReturnsProductsOrderedByName()
        {
            // Arrange
            await SeedCategoryAndSupplierAsync();
            using (var seedContext = CreateContext())
            {
                seedContext.Products.AddRange(
                    new Product { ProductID = 1, ProductName = "Ikura", CategoryID = 1, SupplierID = 1, UnitPrice = 31.00m },
                    new Product { ProductID = 2, ProductName = "Aniseed Syrup", CategoryID = 1, SupplierID = 1, UnitPrice = 10.00m }
                );
                await seedContext.SaveChangesAsync();
            }

            using var context = CreateContext();
            var service = new ProductService(context);

            // Act
            var result = await service.GetProductsAsync();

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().HaveCount(2);
            result.Value[0].ProductName.Should().Be("Aniseed Syrup");
            result.Value[1].ProductName.Should().Be("Ikura");
            result.Value[0].CategoryName.Should().Be("Beverages");
            result.Value[0].SupplierCompanyName.Should().Be("Exotic Liquids");
        }

        [Fact]
        public async Task GetProductsAsync_ProductWithoutCategoryOrSupplier_ReturnsPlaceholderNames()
        {
            // Arrange
            using (var seedContext = CreateContext())
            {
                seedContext.Products.Add(new Product { ProductID = 1, ProductName = "Chai" });
                await seedContext.SaveChangesAsync();
            }

            using var context = CreateContext();
            var service = new ProductService(context);

            // Act
            var result = await service.GetProductsAsync();

            // Assert
            result.IsSuccess.Should().BeTrue();
            var product = result.Value.Single();
            product.CategoryName.Should().Be("Uncategorized");
            product.SupplierCompanyName.Should().Be("No Supplier Listed");
        }

        #endregion

        #region LookupProducts(string) tests

        [Theory]
        [InlineData("")]
        [InlineData("      ")]
        [InlineData(null)]
        public async Task LookupProducts_NullOrWhitespaceName_ReturnsMissingInformationError(string? productName)
        {
            // Arrange
            using var context = CreateContext();
            var service = new ProductService(context);

            // Act
            var result = await service.LookupProducts(productName!);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Errors.Should().ContainSingle(e => e.Code == "Missing information");
        }

        [Fact]
        public async Task LookupProducts_PartialName_ReturnsCaseInsensitiveMatchesOrderedByName()
        {
            // Arrange
            using (var seedContext = CreateContext())
            {
                seedContext.Products.AddRange(
                    new Product { ProductID = 1, ProductName = "Chang" },
                    new Product { ProductID = 2, ProductName = "Chai" },
                    new Product { ProductID = 3, ProductName = "Ikura" }
                );
                await seedContext.SaveChangesAsync();
            }

            using var context = CreateContext();
            var service = new ProductService(context);

            // Act
            var result = await service.LookupProducts("CHA");

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Select(p => p.ProductName).Should().Equal("Chai", "Chang");
        }

        [Fact]
        public async Task LookupProducts_NameWithNoMatches_ReturnsNoProductsFoundError()
        {
            // Arrange
            using (var seedContext = CreateContext())
            {
                seedContext.Products.Add(new Product { ProductID = 1, ProductName = "Chai" });
                await seedContext.SaveChangesAsync();
            }

            using var context = CreateContext();
            var service = new ProductService(context);

            // Act
            var result = await service.LookupProducts("NONEXISTENT");

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Errors.Should().ContainSingle(e => e.Code == "No Products Found");
        }

        #endregion

        #region LookupProducts(int) tests

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task LookupProducts_NonPositiveID_ReturnsInvalidIDError(int productID)
        {
            // Arrange
            using var context = CreateContext();
            var service = new ProductService(context);

            // Act
            var result = await service.LookupProducts(productID);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Errors.Should().ContainSingle(e => e.Code == "Invalid ID");
        }

        [Fact]
        public async Task LookupProducts_ExistingID_ReturnsSingleMatchingProduct()
        {
            // Arrange
            using (var seedContext = CreateContext())
            {
                seedContext.Products.AddRange(
                    new Product { ProductID = 1, ProductName = "Chai" },
                    new Product { ProductID = 2, ProductName = "Chang" }
                );
                await seedContext.SaveChangesAsync();
            }

            using var context = CreateContext();
            var service = new ProductService(context);

            // Act
            var result = await service.LookupProducts(2);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().ContainSingle();
            result.Value.First().ProductName.Should().Be("Chang");
        }

        [Fact]
        public async Task LookupProducts_NonExistentID_ReturnsNotFoundError()
        {
            // Arrange
            using var context = CreateContext();
            var service = new ProductService(context);

            // Act
            var result = await service.LookupProducts(99999);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Errors.Should().ContainSingle(e => e.Code == "Not Found");
        }

        #endregion

        #region GetProductByIDAsync tests

        [Fact]
        public async Task GetProductByIDAsync_ExistingID_ReturnsMappedProductView()
        {
            // Arrange
            await SeedCategoryAndSupplierAsync();
            using (var seedContext = CreateContext())
            {
                seedContext.Products.Add(new Product
                {
                    ProductID = 1,
                    ProductName = "Chai",
                    CategoryID = 1,
                    SupplierID = 1,
                    QuantityPerUnit = "10 boxes x 20 bags",
                    UnitPrice = 18.00m,
                    UnitsInStock = 39,
                    UnitsOnOrder = 5,
                    ReorderLevel = 10,
                    Discontinued = true
                });
                await seedContext.SaveChangesAsync();
            }

            using var context = CreateContext();
            var service = new ProductService(context);

            // Act
            var result = await service.GetProductByIDAsync(1);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeEquivalentTo(new ProductView
            {
                ProductID = 1,
                ProductName = "Chai",
                CategoryID = 1,
                SupplierID = 1,
                QuantityPerUnit = "10 boxes x 20 bags",
                UnitPrice = 18.00m,
                UnitsInStock = 39,
                UnitsOnOrder = 5,
                ReorderLevel = 10,
                Discontinued = true,
                CategoryName = "Beverages",
                SupplierCompanyName = "Exotic Liquids"
            });
        }

        [Fact]
        public async Task GetProductByIDAsync_NonExistentID_ReturnsNoProductError()
        {
            // Arrange
            using var context = CreateContext();
            var service = new ProductService(context);

            // Act
            var result = await service.GetProductByIDAsync(99999);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Errors.Should().ContainSingle(e => e.Code == "No Product");
        }

        #endregion

        #region AddEditProduct tests

        [Fact]
        public async Task AddEditProduct_NullProduct_ReturnsMissingInformationError()
        {
            // Arrange
            using var context = CreateContext();
            var service = new ProductService(context);

            // Act
            var result = await service.AddEditProduct(null!);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Errors.Should().ContainSingle(e => e.Code == "Missing Information");
        }

        [Fact]
        public async Task AddEditProduct_MissingNameCategoryAndSupplier_ReturnsThreeMissingInformationErrors()
        {
            // Arrange
            using var context = CreateContext();
            var service = new ProductService(context);

            var editProduct = CreateValidProductView(productName: "   ");
            editProduct.CategoryID = null;
            editProduct.SupplierID = 0;

            // Act
            var result = await service.AddEditProduct(editProduct);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Errors.Should().HaveCount(3);
            result.Errors.Should().OnlyContain(e => e.Code == "Missing Information");
        }

        [Fact]
        public async Task AddEditProduct_NullProductName_ReturnsMissingInformationErrorWithoutThrowing()
        {
            // Arrange
            using var context = CreateContext();
            var service = new ProductService(context);

            var editProduct = CreateValidProductView(productName: null!);

            // Act
            var result = await service.AddEditProduct(editProduct);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Errors.Should().ContainSingle()
                .Which.Code.Should().Be("Missing Information");
        }

        [Fact]
        public async Task AddEditProduct_NegativeNumericValues_ReturnsFourInvalidValueErrors()
        {
            // Arrange
            using var context = CreateContext();
            var service = new ProductService(context);

            var editProduct = CreateValidProductView();
            editProduct.UnitPrice = -0.01m;
            editProduct.UnitsInStock = -1;
            editProduct.UnitsOnOrder = -1;
            editProduct.ReorderLevel = -1;

            // Act
            var result = await service.AddEditProduct(editProduct);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Errors.Should().HaveCount(4);
            result.Errors.Should().OnlyContain(e => e.Code == "Invalid Value");
        }

        [Fact]
        public async Task AddEditProduct_DuplicateNameDifferentCase_ReturnsDuplicateDataWarning()
        {
            // Arrange
            await SeedCategoryAndSupplierAsync();
            using (var seedContext = CreateContext())
            {
                seedContext.Products.Add(new Product { ProductID = 1, ProductName = "Chai", CategoryID = 1, SupplierID = 1 });
                await seedContext.SaveChangesAsync();
            }

            using var context = CreateContext();
            var service = new ProductService(context);

            // Act
            var result = await service.AddEditProduct(CreateValidProductView(productName: "CHAI"));

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Errors.Should().ContainSingle(e => e.Code == "Duplicate Data Warning");

            using var verifyContext = CreateContext();
            (await verifyContext.Products.CountAsync()).Should().Be(1);
        }

        [Fact]
        public async Task AddEditProduct_NonExistentProductID_ReturnsCannotFindRecordError()
        {
            // Arrange
            await SeedCategoryAndSupplierAsync();

            using var context = CreateContext();
            var service = new ProductService(context);

            // Act
            var result = await service.AddEditProduct(CreateValidProductView(productID: 999));

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Errors.Should().ContainSingle(e => e.Code == "Cannot find a record to edit");
        }

        [Fact]
        public async Task AddEditProduct_NewProduct_InsertsAndReturnsRefreshedProduct()
        {
            // Arrange
            await SeedCategoryAndSupplierAsync();

            using var context = CreateContext();
            var service = new ProductService(context);

            // Act
            var result = await service.AddEditProduct(CreateValidProductView());

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.ProductID.Should().BeGreaterThan(0);
            result.Value.ProductName.Should().Be("Chai");
            result.Value.CategoryName.Should().Be("Beverages");
            result.Value.SupplierCompanyName.Should().Be("Exotic Liquids");

            using var verifyContext = CreateContext();
            var savedProduct = await verifyContext.Products.FindAsync(result.Value.ProductID);
            savedProduct.Should().NotBeNull();
            savedProduct!.ProductName.Should().Be("Chai");
            savedProduct.UnitPrice.Should().Be(18.00m);
            savedProduct.UnitsInStock.Should().Be((short)39);
        }

        [Fact]
        public async Task AddEditProduct_ExistingProductKeepingSameName_UpdatesFields()
        {
            // Arrange
            await SeedCategoryAndSupplierAsync();
            using (var seedContext = CreateContext())
            {
                seedContext.Products.Add(new Product { ProductID = 1, ProductName = "Chai", CategoryID = 1, SupplierID = 1, UnitPrice = 18.00m });
                await seedContext.SaveChangesAsync();
            }

            using var context = CreateContext();
            var service = new ProductService(context);

            // Same name as its own row must not trigger the duplicate check
            var editProduct = CreateValidProductView(productID: 1);
            editProduct.UnitPrice = 25.50m;
            editProduct.Discontinued = true;

            // Act
            var result = await service.AddEditProduct(editProduct);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.UnitPrice.Should().Be(25.50m);
            result.Value.Discontinued.Should().BeTrue();

            using var verifyContext = CreateContext();
            var savedProduct = await verifyContext.Products.FindAsync(1);
            savedProduct!.UnitPrice.Should().Be(25.50m);
            savedProduct.Discontinued.Should().BeTrue();
            (await verifyContext.Products.CountAsync()).Should().Be(1);
        }

        [Fact]
        public async Task AddEditProduct_CategoryDoesNotExist_ReturnsErrorSavingChanges()
        {
            // Arrange - SQLite enforces foreign keys, so the insert fails on save
            await SeedCategoryAndSupplierAsync();

            using var context = CreateContext();
            var service = new ProductService(context);

            var editProduct = CreateValidProductView();
            editProduct.CategoryID = 999;

            // Act
            var result = await service.AddEditProduct(editProduct);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Errors.Should().ContainSingle(e => e.Code == "Error Saving Changes");
            context.ChangeTracker.Entries().Should().BeEmpty();

            using var verifyContext = CreateContext();
            (await verifyContext.Products.CountAsync()).Should().Be(0);
        }

        #endregion

        #region GetCategoriesAsync tests

        [Fact]
        public async Task GetCategoriesAsync_ReturnsAllCategoriesOrderedByName()
        {
            // Arrange
            using (var seedContext = CreateContext())
            {
                seedContext.Categories.AddRange(
                    new Category { CategoryID = 1, CategoryName = "Seafood" },
                    new Category { CategoryID = 2, CategoryName = "Beverages" }
                );
                await seedContext.SaveChangesAsync();
            }

            using var context = CreateContext();
            var service = new ProductService(context);

            // Act
            var result = await service.GetCategoriesAsync();

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Select(c => c.CategoryName).Should().Equal("Beverages", "Seafood");
        }

        [Fact]
        public async Task GetCategoriesAsync_WhenNoCategoriesExist_ReturnsEmptyList()
        {
            // Arrange
            using var context = CreateContext();
            var service = new ProductService(context);

            // Act
            var result = await service.GetCategoriesAsync();

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeEmpty();
        }

        #endregion

        #region GetSuppliersAsync tests

        [Fact]
        public async Task GetSuppliersAsync_ReturnsAllSuppliersOrderedByCompanyName()
        {
            // Arrange
            using (var seedContext = CreateContext())
            {
                seedContext.Suppliers.AddRange(
                    new Supplier { SupplierID = 1, CompanyName = "Tokyo Traders" },
                    new Supplier { SupplierID = 2, CompanyName = "Exotic Liquids" }
                );
                await seedContext.SaveChangesAsync();
            }

            using var context = CreateContext();
            var service = new ProductService(context);

            // Act
            var result = await service.GetSuppliersAsync();

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Select(s => s.SupplierCompanyName).Should().Equal("Exotic Liquids", "Tokyo Traders");
        }

        [Fact]
        public async Task GetSuppliersAsync_WhenNoSuppliersExist_ReturnsEmptyList()
        {
            // Arrange
            using var context = CreateContext();
            var service = new ProductService(context);

            // Act
            var result = await service.GetSuppliersAsync();

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeEmpty();
        }

        #endregion
    }
}
