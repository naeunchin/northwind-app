using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using Microsoft.EntityFrameworkCore.Sqlite;
using Microsoft.EntityFrameworkCore;
using OLTPSystem.BLL;
using OLTPSystem.DAL;
using OLTPSystem.Entities;
using OLTPSystem.ViewModels;
using BYSResults;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Server.Kestrel.Transport.NamedPipes;

namespace UnitTests.Services
{
    public class OrderServiceTests : IDisposable
    {
        private readonly NorthwindContext _context;
        private readonly SqliteConnection _connection;
        private readonly TestDbContextFactory _contextFactory;
        
        public OrderServiceTests()
        {
            // Creating & establishing an in-memory SQLite connection
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            // Congifuring DbContext to use the open SQLite connection
            var options = new DbContextOptionsBuilder<NorthwindContext>().UseSqlite(_connection).Options;

            _context = new TestNorthwindContext(options);

            // Create DB schema (tables & keys)
            _context.Database.EnsureCreated();
            _contextFactory = new TestDbContextFactory(_connection);
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
            return _contextFactory.CreateDbContext();
        }

        [Fact]
        public void Constructor_NullContext_ThrowsArgumentNullException()
        {
            // Act 
            Action act = () => new OrderService(null!);

            // Assert 
            act.Should().Throw<ArgumentNullException>().WithParameterName("contextFactory");
        }

        #region GetOrdersAsync tests

        [Fact]
        public async Task GetOrdersAsync_WhenNoOrdersExist_ReturnsSuccessWithEmptyList()
        {
            // Arrange
            var service = new OrderService(_contextFactory);

            // Act
            var result = await service.GetOrdersAsync();

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value.Should().BeEmpty();
        }

        [Fact]
        public async Task GetOrdersAsync_WhenOrdersExist_ReturnsOrdersByDateDescending()
        {
            // Arrange
            using (var seedContext = CreateContext())
            {
                seedContext.Customers.Add(new Customer { CustomerID = "ALFKI", CompanyName = "Alfreds Futterkiste" });
                seedContext.Orders.AddRange(
                    new Order { OrderID = 1, CustomerID = "ALFKI", OrderDate = new DateTime(2026, 1, 10) },
                    new Order { OrderID = 2, CustomerID = "ALFKI", OrderDate = new DateTime(2026, 5, 20) }
                );
                await seedContext.SaveChangesAsync();
            }

            var service = new OrderService(_contextFactory);

            // Act
            var result = await service.GetOrdersAsync();

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().HaveCount(2);
            result.Value[0].OrderID.Should().Be(2); // Newest first
            result.Value[1].OrderID.Should().Be(1);
            result.Value[0].CustomerCompanyName.Should().Be("Alfreds Futterkiste");
        }

        #endregion

        #region LookupOrders tests

        [Theory]
        [InlineData("")]
        [InlineData("      ")]
        [InlineData(null)]
        public async Task LookupOrders_NullOrWhitespaceSearchTerm_ReturnsMissingInformationError(string? searchTerm)
        {
            // Arrange 
            var service = new OrderService(_contextFactory);

            // Act
            var result = await service.LookupOrders(searchTerm!);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Errors.Should().ContainSingle(e => e.Code == "Missing Information");
        }

        [Fact]
        public async Task LookupOrders_NumericSearchTerm_FiltersByExactOrderID()
        {
            // Arrange 
            using (var seedContext = CreateContext())
            {
                seedContext.Customers.Add(new Customer { CustomerID = "ALFKI", CompanyName = "Alfreds Futterkiste" });
                seedContext.Orders.AddRange(
                    new Order { OrderID = 101, CustomerID = "ALFKI", OrderDate = DateTime.Today },
                    new Order { OrderID = 102, CustomerID = "ALFKI", OrderDate = DateTime.Today }
                );
                await seedContext.SaveChangesAsync();
            }

            var service = new OrderService(_contextFactory);

            // Act 
            var result = await service.LookupOrders("101");
            
            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().ContainSingle();
            result.Value.First().OrderID.Should().Be(101);
        }

        [Fact]
        public async Task LookupOrders_StringSearchTerm_FiltersByPartialCustomerID()
        {
            // Arrange
            using (var seedContext = CreateContext())
            {
                seedContext.Customers.AddRange(
                    new Customer { CustomerID = "ALFKI", CompanyName = "Alfreds Futterkiste" },
                    new Customer { CustomerID = "BOLID", CompanyName = "Bólido Comidas" }
                );
                seedContext.Orders.AddRange(
                    new Order { OrderID = 1, CustomerID = "ALFKI", OrderDate = DateTime.Today },
                    new Order { OrderID = 2, CustomerID = "BOLID", OrderDate = DateTime.Today }
                );
                await seedContext.SaveChangesAsync();
            }

            var service = new OrderService(_contextFactory);

            // Act
            var result = await service.LookupOrders("alf");

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().ContainSingle();
            result.Value.First().CustomerID.Should().Be("ALFKI");
        }

        [Fact]
        public async Task LookupOrders_WhenNoMatchesFound_ReturnsNoRecordsLocatedError()
        {
            // Arrange
            var service = new OrderService(_contextFactory);

            // Act
            var result = await service.LookupOrders("NONEXISTENT");

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Errors.Should().ContainSingle(e => e.Code == "No Records Located");
        }

        #endregion

        #region GetOrderByIDAsync tests

        [Fact]
        public async Task GetOrderByIDAsync_ExistingOrderID_ReturnsOrderViewAndDetails()
        {
            // Arrange
            using (var seedContext = CreateContext())
            {
                seedContext.Customers.Add(new Customer { CustomerID = "ALFKI", CompanyName = "Alfreds Futterkiste" });
                seedContext.Orders.Add(new Order
                {
                    OrderID = 300,
                    CustomerID = "ALFKI"
                });
                await seedContext.SaveChangesAsync();
            }

            var service = new OrderService(_contextFactory);

            // Act
            var result = await service.GetOrderByIDAsync(300);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value.OrderID.Should().Be(300);
            result.Value.CustomerID.Should().Be("ALFKI");
        }

        [Fact]
        public async Task GetOrderByIDAsync_NonExistentOrderID_ReturnsMissingOrderError()
        {
            // Arrange
            var service = new OrderService(_contextFactory);

            // Act
            var result = await service.GetOrderByIDAsync(99999);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Errors.Should().ContainSingle(e => e.Code == "Missing Order");
        }

        #endregion

        #region AddEditOrderAsync tests

        [Fact]
        public async Task AddEditOrderAsync_NullEditOrder_ReturnsMissingInformationError()
        {
            // Arrange
            var service = new OrderService(_contextFactory);

            // Act
            var result = await service.AddEditOrderAsync(null!, new List<OrderDetailView>());

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Errors.Should().ContainSingle(e => e.Code == "Missing Information");
        }

        [Fact]
        public async Task AddEditOrderAsync_MissingRequiredFields_ReturnsValidationFailures()
        {
            // Arrange
            var service = new OrderService(_contextFactory);

            var editOrder = new OrderView
            {
                CustomerID = "",
                EmployeeID = 0
            };

            // Act
            var result = await service.AddEditOrderAsync(editOrder, new List<OrderDetailView>());

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Errors.Should().HaveCount(3);
        }

        [Fact]
        public async Task AddEditOrderAsync_NonExistentOrderID_ReturnsCannotFindOrderError()
        {
            // Arrange
            var service = new OrderService(_contextFactory);

            var editOrder = new OrderView
            {
                OrderID = 999,
                CustomerID = "ALFKI",
                EmployeeID = 1
            };
            var items = new List<OrderDetailView> { new OrderDetailView { ProductID = 1, Quantity = 1 } };

            // Act
            var result = await service.AddEditOrderAsync(editOrder, items);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Errors.Should().ContainSingle(e => e.Code == "Cannot find an order to edit");
        }

        [Fact]
        public async Task AddEditOrderAsync_NewOrder_SavesOrderHeaderAndLineItems()
        {
            // Arrange
            using (var seedContext = CreateContext())
            {
                seedContext.Customers.Add(new Customer { CustomerID = "ALFKI", CompanyName = "Alfreds Futterkiste" });
                seedContext.Employees.Add(new Employee { EmployeeID = 1, FirstName = "Nancy", LastName = "Davolio" });
                seedContext.Products.Add(new Product { ProductID = 1, ProductName = "Chai", UnitPrice = 18.00m, UnitsInStock = 10, Discontinued = false });
                await seedContext.SaveChangesAsync();
            }

            var orderView = new OrderView
            {
                OrderID = 0,
                CustomerID = "ALFKI",
                EmployeeID = 1,
                OrderDate = DateTime.Today
            };

            var details = new List<OrderDetailView> { new OrderDetailView { ProductID = 1, UnitPrice = 18.00m, Quantity = 2, Discount = 0 } };

            var service = new OrderService(_contextFactory);

            // Act
            var addResult = await service.AddEditOrderAsync(orderView, details);

            // Assert
            addResult.IsSuccess.Should().BeTrue();
            addResult.Value.Should().NotBeNull();
            addResult.Value.OrderID.Should().BeGreaterThan(0);
            addResult.Value.CustomerID.Should().Be("ALFKI");

            using var verifyContext = CreateContext();
            var savedOrder = verifyContext.Orders.FirstOrDefault(o => o.OrderID == addResult.Value.OrderID);
            savedOrder.Should().NotBeNull();
            savedOrder.CustomerID.Should().Be("ALFKI");
        }

        [Fact]
        public async Task AddEditOrderAsync_ExistingOrder_SyncsLineItemsCorrectly()
        {
            // Arrange - Create an existing order with two line items
            using (var seedContext = CreateContext())
            {
                seedContext.Customers.Add(new Customer { CustomerID = "ALFKI", CompanyName = "Alfreds Futterkiste" });
                seedContext.Employees.Add(new Employee { EmployeeID = 1, FirstName = "Nancy", LastName = "Davolio" });
                seedContext.Products.AddRange(
                    new Product { ProductID = 1, ProductName = "Chai", UnitPrice = 10.00m, Discontinued = false },
                    new Product { ProductID = 2, ProductName = "Chang", UnitPrice = 20.00m, Discontinued = false },
                    new Product { ProductID = 3, ProductName = "Aniseed Syrup", UnitPrice = 15.00m, Discontinued = false }
                );
                seedContext.Orders.Add(new Order
                {
                    OrderID = 100,
                    CustomerID = "ALFKI",
                    EmployeeID = 1,
                    Order_Details = new List<Order_Detail>
                {
                    new Order_Detail { OrderID = 100, ProductID = 1, UnitPrice = 10.00m, Quantity = 1, Discount = 0 },
                    new Order_Detail { OrderID = 100, ProductID = 2, UnitPrice = 20.00m, Quantity = 2, Discount = 0 }
                }
                });
                await seedContext.SaveChangesAsync();
            }

            var service = new OrderService(_contextFactory);

            // Act - Submit updated items: Product 1 modified (quantity 5), Product 2 removed, Product 3 added
            var updatedItems = new List<OrderDetailView>
        {
            new OrderDetailView { ProductID = 1, UnitPrice = 10.00m, Quantity = 5, Discount = 0 },
            new OrderDetailView { ProductID = 3, UnitPrice = 15.00m, Quantity = 1, Discount = 0 }
        };

            var orderView = new OrderView { OrderID = 100, CustomerID = "ALFKI", EmployeeID = 1 };
            var result = await service.AddEditOrderAsync(orderView, updatedItems);

            // Assert
            result.IsSuccess.Should().BeTrue();

            using var verifyContext = CreateContext();
            var savedDetails = await verifyContext.Order_Details
                .Where(d => d.OrderID == 100)
                .ToListAsync();

            savedDetails.Should().HaveCount(2);
            savedDetails.Should().Contain(d => d.ProductID == 1 && d.Quantity == 5);
            savedDetails.Should().Contain(d => d.ProductID == 3 && d.Quantity == 1);
            savedDetails.Should().NotContain(d => d.ProductID == 2);
        }

        #endregion

        #region DeleteOrderAsync tests

        [Fact]
        public async Task DeleteOrderAsync_InvalidOrderID_ReturnsMissingInformationError()
        {
            // Arrange 
            var service = new OrderService(_contextFactory);

            // Act 
            var result = await service.DeleteOrderAsync(0);

            // Assert 
            result.IsFailure.Should().BeTrue();
            result.Errors.Should().ContainSingle(e => e.Code == "Missing Information");
        }

        [Fact]
        public async Task DeleteOrderAsync_OrderDoesNotExist_ReturnsMissingOrderError()
        {
            // Arrange
            var service = new OrderService(_contextFactory);

            // Act
            var result = await service.DeleteOrderAsync(999);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Errors.Should().ContainSingle(e => e.Code == "Missing Order");
        }

        [Fact]
        public async Task DeleteOrderAsync_ExistingOrderWithDetails_DeletesOrderAndDetails()
        {
            // Arrange
            using (var seedContext = CreateContext())
            {
                seedContext.Customers.Add(new Customer { CustomerID = "ALFKI", CompanyName = "Alfreds Futterkiste" });
                seedContext.Products.Add(new Product { ProductID = 1, ProductName = "Chai", UnitPrice = 10.00m, Discontinued = false });

                var order = new Order
                {
                    OrderID = 500,
                    CustomerID = "ALFKI",
                    Order_Details = new List<Order_Detail> { new Order_Detail { OrderID = 500, ProductID = 1, UnitPrice = 10m, Quantity = 2 } }
                };
                seedContext.Orders.Add(order);
                await seedContext.SaveChangesAsync();
            }

            var service = new OrderService(_contextFactory);

            // Act
            var result = await service.DeleteOrderAsync(500);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeGreaterThan(0);

            using var verifyContext = CreateContext();
            var deletedOrder = await verifyContext.Orders.FindAsync(500);
            deletedOrder.Should().BeNull();
            var remainingDetails = await verifyContext.Order_Details.Where(od => od.OrderID == 500).ToListAsync();
            remainingDetails.Should().BeEmpty();
        }

        #endregion

        #region GetOrderDetailsAsync tests

        [Fact]
        public async Task GetOrderDetailsAsync_ExistingOrderID_ReturnsMappedOrderDetails()
        {
            // Arrange
            using (var seedContext = CreateContext())
            {
                seedContext.Customers.Add(new Customer { CustomerID = "ALFKI", CompanyName = "Alfreds Futterkiste" });
                seedContext.Products.AddRange(
                    new Product { ProductID = 1, ProductName = "Chai", UnitPrice = 18.00m, UnitsInStock = 10, Discontinued = false },
                    new Product { ProductID = 2, ProductName = "Chang", UnitPrice = 19.00m, UnitsInStock = 20, Discontinued = false }
                );

                seedContext.Orders.Add(new Order
                {
                    OrderID = 100,
                    CustomerID = "ALFKI",
                    Order_Details = new List<Order_Detail>
                {
                    new Order_Detail { OrderID = 100, ProductID = 1, UnitPrice = 18.00m, Quantity = 2, Discount = 0 },
                    new Order_Detail { OrderID = 100, ProductID = 2, UnitPrice = 19.00m, Quantity = 5, Discount = 0.1f }
                }
                });
                await seedContext.SaveChangesAsync();
            }

            var service = new OrderService(_contextFactory);

            // Act
            var result = await service.GetOrderDetailsAsync(100);

            // Assert
            result.IsSuccess.Should().BeTrue();
            var details = result.Value.ToList();
            details.Should().HaveCount(2);

            var firstDetail = details.First(d => d.ProductID == 1);
            firstDetail.ProductName.Should().Be("Chai");
            firstDetail.Quantity.Should().Be(2);
            firstDetail.UnitPrice.Should().Be(18.00m);
            firstDetail.LineTotal.Should().Be(36.00m);
        }

        [Fact]
        public async Task GetOrderDetailsAsync_NonExistentOrderID_ReturnsEmptyList()
        {
            // Arrange
            var service = new OrderService(_contextFactory);

            // Act
            var result = await service.GetOrderDetailsAsync(99999);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value.Should().BeEmpty();
        }

        #endregion

        #region GetCustomersAsync tests

        [Fact]
        public async Task GetCustomersAsync_ReturnsAllCustomersOrderedByName()
        {
            // Arrange
            using (var seedContext = CreateContext())
            {
                seedContext.Customers.AddRange(
                    new Customer { CustomerID = "BOLID", CompanyName = "Bólido Comidas" },
                    new Customer { CustomerID = "ALFKI", CompanyName = "Alfreds Futterkiste" }
                );
                await seedContext.SaveChangesAsync();
            }

            var service = new OrderService(_contextFactory);

            // Act
            var result = await service.GetCustomersAsync();

            // Assert
            result.IsSuccess.Should().BeTrue();
            var customers = result.Value.ToList();
            customers.Should().HaveCount(2);
            customers.First().CompanyName.Should().Be("Alfreds Futterkiste");
        }

        #endregion

        #region GetEmployeesAsync tests

        [Fact]
        public async Task GetEmployeesAsync_ReturnsAllEmployeesOrderedByLastName()
        {
            // Arrange
            using (var seedContext = CreateContext())
            {
                seedContext.Employees.AddRange(
                    new Employee { EmployeeID = 1, FirstName = "Nancy", LastName = "Davolio" },
                    new Employee { EmployeeID = 2, FirstName = "Andrew", LastName = "Adams" }
                );
                await seedContext.SaveChangesAsync();
            }

            var service = new OrderService(_contextFactory);

            // Act
            var result = await service.GetEmployeesAsync();

            // Assert
            result.IsSuccess.Should().BeTrue();
            var employees = result.Value.ToList();
            employees.Should().HaveCount(2);

            // Verifies sorting order (Adams before Davolio)
            employees.First().FullName.Should().Contain("Adams");
            employees.Last().FullName.Should().Contain("Davolio");
        }

        #endregion

        #region GetShippersAsync tests

        [Fact]
        public async Task GetShippersAsync_ReturnsAllShippersOrderedByName()
        {
            // Arrange
            using (var seedContext = CreateContext())
            {
                seedContext.Shippers.AddRange(
                    new Shipper { ShipperID = 1, CompanyName = "Speedy Express" },
                    new Shipper { ShipperID = 2, CompanyName = "Federal Shipping" }
                );
                await seedContext.SaveChangesAsync();
            }

            var service = new OrderService(_contextFactory);

            // Act
            var result = await service.GetShippersAsync();

            // Assert
            result.IsSuccess.Should().BeTrue();
            var shippers = result.Value.ToList();
            shippers.Should().HaveCount(2);

            shippers.First().CompanyName.Should().Be("Federal Shipping");
            shippers.Last().CompanyName.Should().Be("Speedy Express");
        }

        #endregion

        #region GetProductsAsync tests

        [Fact]
        public async Task GetProductsAsync_ReturnsActiveProductsOrderedByName()
        {
            // Arrange
            using (var seedContext = CreateContext())
            {
                seedContext.Products.AddRange(
                    new Product { ProductID = 1, ProductName = "Ikura", UnitPrice = 31.00m, Discontinued = false },
                    new Product { ProductID = 2, ProductName = "Aniseed Syrup", UnitPrice = 10.00m, Discontinued = false }
                );
                await seedContext.SaveChangesAsync();
            }

            var service = new OrderService(_contextFactory);

            // Act
            var result = await service.GetProductsAsync();

            // Assert
            result.IsSuccess.Should().BeTrue();
            var products = result.Value.ToList();
            products.Should().HaveCount(2);

            products.First().ProductName.Should().Be("Aniseed Syrup");
            products.Last().ProductName.Should().Be("Ikura");
        }

        [Fact]
        public async Task GetProductsAsync_WhenNoProductsExist_ReturnsEmptyList()
        {
            // Arrange
            var service = new OrderService(_contextFactory);

            // Act
            var result = await service.GetProductsAsync();

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value.Should().BeEmpty();
        }

        #endregion
    }
}
