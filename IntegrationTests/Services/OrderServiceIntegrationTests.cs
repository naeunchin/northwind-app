using FluentAssertions;
using IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using OLTPSystem.BLL;
using OLTPSystem.Entities;
using OLTPSystem.ViewModels;

namespace IntegrationTests.Services
{
    [Collection(nameof(DatabaseCollection))]
    public class OrderServiceIntegrationTests : IntegrationTestBase
    {
        private readonly Customer _alfreds = new() { CustomerID = "ALFKI", CompanyName = "Alfreds Futterkiste" };
        private readonly Customer _bolido = new() { CustomerID = "BOLID", CompanyName = "Bólido Comidas" };
        private readonly Employee _nancy = new() { FirstName = "Nancy", LastName = "Davolio" };
        private readonly Product _chai = new() { ProductName = "Chai", UnitPrice = 18.00m };
        private readonly Product _chang = new() { ProductName = "Chang", UnitPrice = 19.00m };
        private readonly Product _syrup = new() { ProductName = "Aniseed Syrup", UnitPrice = 10.00m };

        public OrderServiceIntegrationTests(DatabaseFixture database) : base(database)
        {
        }

        /// <summary>
        /// Seeds the customers, employee and products shared by the order tests.
        /// </summary>
        private Task SeedLookupDataAsync() => SeedAsync(_alfreds, _bolido, _nancy, _chai, _chang, _syrup);

        /// <summary>
        /// Seeds an ALFKI order with one line item per product given, returning the generated order.
        /// </summary>
        private async Task<Order> SeedOrderAsync(DateTime orderDate, params Product[] products)
        {
            var order = new Order
            {
                CustomerID = _alfreds.CustomerID,
                EmployeeID = _nancy.EmployeeID,
                OrderDate = orderDate,
                Order_Details = products
                    .Select(p => new Order_Detail { ProductID = p.ProductID, UnitPrice = p.UnitPrice ?? 0m, Quantity = 1 })
                    .ToList()
            };

            await SeedAsync(order);
            return order;
        }

        private OrderView CreateOrderView(int orderID = 0) => new OrderView
        {
            OrderID = orderID,
            CustomerID = _alfreds.CustomerID,
            EmployeeID = _nancy.EmployeeID,
            OrderDate = new DateTime(2026, 10, 1)
        };

        #region AddEditOrderAsync tests

        [Fact]
        public async Task AddEditOrderAsync_NewOrder_GeneratesOrderIDAndSavesLineItems()
        {
            // Arrange
            await SeedLookupDataAsync();

            var service = new OrderService(ContextFactory);

            var items = new List<OrderDetailView>
            {
                new() { ProductID = _chai.ProductID, UnitPrice = 18.00m, Quantity = 2, Discount = 0 },
                new() { ProductID = _chang.ProductID, UnitPrice = 19.00m, Quantity = 1, Discount = 0.1f }
            };

            // Act
            var result = await service.AddEditOrderAsync(CreateOrderView(), items);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.OrderID.Should().BeGreaterThan(0);
            result.Value.CustomerCompanyName.Should().Be("Alfreds Futterkiste");
            result.Value.EmployeeFullName.Should().Be("Nancy Davolio");

            await using var verifyContext = CreateContext();
            var savedDetails = await verifyContext.Order_Details.Where(d => d.OrderID == result.Value.OrderID).ToListAsync();
            savedDetails.Should().HaveCount(2);
            savedDetails.Should().Contain(d => d.ProductID == _chai.ProductID && d.Quantity == 2);
            savedDetails.Should().Contain(d => d.ProductID == _chang.ProductID && d.Discount == 0.1f);
        }

        [Fact]
        public async Task AddEditOrderAsync_ProductDoesNotExist_RollsBackEntireOrder()
        {
            // Arrange - the header and line items save in one transaction, so a bad line item must not leave an orphaned order
            await SeedLookupDataAsync();

            var service = new OrderService(ContextFactory);

            var items = new List<OrderDetailView>
            {
                new() { ProductID = _chai.ProductID, UnitPrice = 18.00m, Quantity = 1 },
                new() { ProductID = 999, UnitPrice = 1.00m, Quantity = 1 }
            };

            // Act
            var result = await service.AddEditOrderAsync(CreateOrderView(), items);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Errors.Should().ContainSingle(e => e.Code == "Error Saving Changes")
                .Which.Message.Should().Contain("FK_Order_Details_Products");

            await using var verifyContext = CreateContext();
            (await verifyContext.Orders.AnyAsync()).Should().BeFalse();
            (await verifyContext.Order_Details.AnyAsync()).Should().BeFalse();
        }

        [Fact]
        public async Task AddEditOrderAsync_ExistingOrder_SyncsLineItems()
        {
            // Arrange
            await SeedLookupDataAsync();
            var order = await SeedOrderAsync(new DateTime(2026, 10, 1), _chai, _chang);

            var service = new OrderService(ContextFactory);

            // Chai modified, Chang removed, Aniseed Syrup added
            var items = new List<OrderDetailView>
            {
                new() { ProductID = _chai.ProductID, UnitPrice = 18.00m, Quantity = 5 },
                new() { ProductID = _syrup.ProductID, UnitPrice = 10.00m, Quantity = 1 }
            };

            // Act
            var result = await service.AddEditOrderAsync(CreateOrderView(order.OrderID), items);

            // Assert
            result.IsSuccess.Should().BeTrue();

            await using var verifyContext = CreateContext();
            var savedDetails = await verifyContext.Order_Details.Where(d => d.OrderID == order.OrderID).ToListAsync();
            savedDetails.Should().HaveCount(2);
            savedDetails.Should().Contain(d => d.ProductID == _chai.ProductID && d.Quantity == 5);
            savedDetails.Should().Contain(d => d.ProductID == _syrup.ProductID);
            savedDetails.Should().NotContain(d => d.ProductID == _chang.ProductID);
        }

        #endregion

        #region DeleteOrderAsync tests

        [Fact]
        public async Task DeleteOrderAsync_ExistingOrderWithDetails_DeletesOrderAndDetails()
        {
            // Arrange
            await SeedLookupDataAsync();
            var order = await SeedOrderAsync(new DateTime(2026, 10, 1), _chai, _chang);
            var otherOrder = await SeedOrderAsync(new DateTime(2026, 10, 2), _chai);

            var service = new OrderService(ContextFactory);

            // Act
            var result = await service.DeleteOrderAsync(order.OrderID);

            // Assert - one order row plus two detail rows
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Be(3);

            await using var verifyContext = CreateContext();
            (await verifyContext.Orders.Select(o => o.OrderID).ToListAsync()).Should().Equal(otherOrder.OrderID);
            (await verifyContext.Order_Details.CountAsync()).Should().Be(1);
        }

        #endregion

        #region Query tests

        [Fact]
        public async Task GetOrdersAsync_ReturnsNewestFirstWithJoinedNames()
        {
            // Arrange
            await SeedLookupDataAsync();
            var older = await SeedOrderAsync(new DateTime(2026, 1, 10), _chai);
            var newer = await SeedOrderAsync(new DateTime(2026, 5, 20), _chai);

            var service = new OrderService(ContextFactory);

            // Act
            var result = await service.GetOrdersAsync();

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Select(o => o.OrderID).Should().Equal(newer.OrderID, older.OrderID);
            result.Value[0].CustomerCompanyName.Should().Be("Alfreds Futterkiste");
            result.Value[0].EmployeeFullName.Should().Be("Nancy Davolio");
            result.Value[0].ShipperName.Should().Be("Unassigned Carrier");
        }

        [Fact]
        public async Task LookupOrders_PartialCustomerIDDifferentCase_ReturnsMatchingOrders()
        {
            // Arrange
            await SeedLookupDataAsync();
            var alfredsOrder = await SeedOrderAsync(new DateTime(2026, 10, 1), _chai);
            await SeedAsync(new Order { CustomerID = _bolido.CustomerID, EmployeeID = _nancy.EmployeeID, OrderDate = new DateTime(2026, 10, 1) });

            var service = new OrderService(ContextFactory);

            // Act
            var result = await service.LookupOrders("alf");

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().ContainSingle().Which.OrderID.Should().Be(alfredsOrder.OrderID);
        }

        [Fact]
        public async Task GetOrderDetailsAsync_ExistingOrder_ReturnsLineItemsWithProductNames()
        {
            // Arrange
            await SeedLookupDataAsync();
            var order = await SeedOrderAsync(new DateTime(2026, 10, 1), _chai, _chang);

            var service = new OrderService(ContextFactory);

            // Act
            var result = await service.GetOrderDetailsAsync(order.OrderID);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Select(d => d.ProductName).Should().BeEquivalentTo("Chai", "Chang");
        }

        #endregion
    }
}
