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
            // Act
            var result = await _service.GetOrdersAsync();

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value.Should().BeEmpty();
        }

        [Fact]
        public async Task GetOrdersAsync_WhenOrdersExist_ReturnsOrdersByDateDescending()
        {
            // Arrange 
            var oldestDate = new DateTime(2026, 1, 1);
            var middleDate = new DateTime(2026, 5, 1);
            var newestDate = new DateTime(2026, 10, 1);

            _context.Orders.AddRange(
                new Order { OrderID = 1, CustomerID = "OLDAT", OrderDate = oldestDate },
                new Order { OrderID = 2, CustomerID = "MIDAT", OrderDate = middleDate },
                new Order { OrderID = 3, CustomerID = "NWDAT", OrderDate = newestDate }
                );
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.GetOrdersAsync();

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().HaveCount(3);
            result.Value.Select(o => o.OrderID).Should().ContainInConsecutiveOrder(3, 2, 1);
        }

        #endregion
    }
}
