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
        private readonly OrderService _service;
        private readonly SqliteConnection _connection;
        
        public OrderServiceTests()
        {
            // Creating & establishing an in-memory SQLite connection
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            // Congifuring DbContext to use the open SQLite connection
            var options = new DbContextOptionsBuilder<NorthwindContext>().UseSqlite(_connection).Options;

            _context = new NorthwindContext(options);

            // Create DB schema (tables & keys)
            _context.Database.EnsureCreated();
            _service = new OrderService(_context);
        }

        public void Dispose()
        {
            // Destroying the in-memory DB 
            _context.Dispose();
            _connection.Close();
            _connection.Dispose();
        }

        /// <summary>
        /// Creates an independent DbContext instance over the same SQLite connection to verify database state without EF Core change tracker bias.
        /// </summary>
        /// <returns>DbContext instance</returns>
        private NorthwindContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<NorthwindContext>().UseSqlite(_connection).Options;

            return new NorthwindContext(options);
        }

        [Fact]
        public void Constructor_NullContext_ThrowsArgumentNullException()
        {
            // Act 
            Action act = () => new OrderService(null!);

            // Assert 
            act.Should().Throw<ArgumentNullException>().WithParameterName("context");
        }

        #region GetOrdersAsync tests

        [Fact]
        public async Task GetOrdersAsync_WhenNoOrdersExist_ReturnsSuccessWithEmptyList()
        {
            // Arrange
            using var context = CreateContext();
            var service = new OrderService(context);

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

            using var context = CreateContext();
            var service = new OrderService(context);

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

        #endregion

        #region GetOrderByIDAsync tests

        [Fact]
        public async Task GetOrderByIDAsync_ExistingOrderID_ReturnsOrderViewAndDetails()
        {
            // Arrange
            using (var seedContext = CreateContext())
            {
                seedContext.Orders.Add(new Order
                {
                    OrderID = 300,
                    CustomerID = "ALFKI"
                });
                await seedContext.SaveChangesAsync();
            }

            using var context = CreateContext();
            var service = new OrderService(context);

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
            using var context = CreateContext();
            var service = new OrderService(context);

            // Act
            var result = await service.GetOrderByIDAsync(99999);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Errors.Should().ContainSingle(e => e.Code == "Missing Order");
        }

        #endregion

        #region AddEditOrderAsync tests

        #endregion

        #region DeleteOrderAsync tests

        #endregion

        #region GetOrderDetailsAsync tests

        #endregion

        #region GetCustomersAsync tests

        #endregion

        #region GetEmployeesAsync tests

        #endregion

        #region GetShippersAsync tests

        #endregion

        #region GetProductsAsync tests

        #endregion
    }
}
