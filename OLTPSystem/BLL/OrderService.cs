using BYSResults;
using Microsoft.EntityFrameworkCore;
using OLTPSystem.DAL;
using OLTPSystem.Entities;
using OLTPSystem.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OLTPSystem.BLL
{
    public class OrderService
    {
        #region Data context setup
        private readonly NorthwindContext _context;

        public OrderService(NorthwindContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }
        #endregion

        /// <summary>
        /// Queries all order records from the database and orders them in a desending order by order date.
        /// </summary>
        /// <returns>A BYS Result container wrapping a list of Order view or error messages.</returns>
        public async Task<Result<List<OrderView>>> GetOrdersAsync()
        {
            var result = new Result<List<OrderView>>();

            var orders = await _context.Orders.Select(o => new OrderView
                                                    {
                                                        OrderID = o.OrderID,
                                                        CustomerID = o.CustomerID,
                                                        CustomerCompanyName = o.Customer != null ? o.Customer.CompanyName : "Individual Account",
                                                        EmployeeID = o.EmployeeID,
                                                        EmployeeFullName = o.Employee != null ? o.Employee.FirstName + " " + o.Employee.LastName : "Northwind Employee",
                                                        OrderDate = o.OrderDate,
                                                        ShippedDate = o.ShippedDate,
                                                        Freight = o.Freight,
                                                        ShipCity = o.ShipCity,
                                                        ShipCountry = o.ShipCountry,
                                                        ShipperName = o.ShipViaNavigation != null ? o.ShipViaNavigation.CompanyName : "Unassigned Carrier"
                                                    }).OrderByDescending(o => o.OrderDate).ToListAsync();

            if (orders == null || orders.Count == 0)
            {
                result.AddError(new Error("No Records Located", "The billing ledger database does not contain any historical orders."));
                return result;
            }

            return result.WithValue(orders);
        }

        /// <summary>
        /// Retrieves order records based on the search term input, either a numerical Order ID or a partial Customer ID code string.
        /// </summary>
        /// <param name="searchTerm">The Order ID integer or Customer ID string keyword to filter on.</param>
        /// <returns>A BYS Result container wrapping a list of matching OrderView records or error messages.</returns>
        public async Task<Result<List<OrderView>>> LookupOrders(string searchTerm)
        {
            var result = new Result<List<OrderView>>();

            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                result.AddError(new Error("Missing Information", "An order search term (either Order ID or customer ID) must be provided."));
                return result;
            }

            var query = _context.Orders.AsQueryable();

            string cleanTerm = searchTerm.Trim().ToLower();

            // Check if the search term input is a numerical value
            if (int.TryParse(cleanTerm, out int parsedOrderID))
            {
                // Look for the exact Order ID OR a partial text string match on the CustomerID textual code
                query = query.Where(o => o.OrderID == parsedOrderID);
            }
            else
            {
                // If it is purely text, filter by partial Customer ID code match
                query = query.Where(o => o.CustomerID.ToLower().Contains(cleanTerm));
            }

            var orders = await query.Select(o => new OrderView
                                                        {
                                                            OrderID = o.OrderID,
                                                            CustomerID = o.CustomerID,
                                                            CustomerCompanyName = o.Customer != null ? o.Customer.CompanyName : "Individual Account",
                                                            EmployeeID = o.EmployeeID,
                                                            EmployeeFullName = o.Employee != null ? o.Employee.FirstName + " " + o.Employee.LastName : "Northwind Employee",
                                                            OrderDate = o.OrderDate,
                                                            ShippedDate = o.ShippedDate,
                                                            Freight = o.Freight,
                                                            ShipCity = o.ShipCity,
                                                            ShipCountry = o.ShipCountry,
                                                            ShipperName = o.ShipViaNavigation != null ? o.ShipViaNavigation.CompanyName : "Unassigned Carrier"
                                                        }).OrderByDescending(o => o.OrderDate).ToListAsync();

            if (orders == null || orders.Count <= 0)
            {
                result.AddError(new Error("No Orders Found", $"No records matched the criteria: '{searchTerm}'."));
                return result;
            }

            return result.WithValue(orders);
        }

        /// <summary>
        /// Retrieves a single order record by its primary key.
        /// </summary>
        /// <param name="orderID">The unique primary key of the order.</param>
        /// <returns>A BYS Result container wrapping a list containing a single OrderView by its primary key.</returns>
        public async Task<Result<OrderView>> GetOrderByIDAsync(int orderID)
        {
            var result = new Result<OrderView>();

            var order = await _context.Orders
                                            .Where(o => o.OrderID == orderID)
                                            .Select(o => new OrderView
                                            {
                                                OrderID = o.OrderID,
                                                CustomerID = o.CustomerID,
                                                CustomerCompanyName = o.Customer != null ? o.Customer.CompanyName : "Individual Account",
                                                EmployeeID = o.EmployeeID,
                                                EmployeeFullName = o.Employee != null ? o.Employee.FirstName + " " + o.Employee.LastName : "Northwind Employee",
                                                OrderDate = o.OrderDate,
                                                ShippedDate = o.ShippedDate,
                                                Freight = o.Freight,
                                                ShipCity = o.ShipCity,
                                                ShipCountry = o.ShipCountry,
                                                ShipperName = o.ShipViaNavigation != null ? o.ShipViaNavigation.CompanyName : "Unassigned Carrier"
                                            }).FirstOrDefaultAsync();

            if (order == null)
            {
                result.AddError(new Error("No Order", $"No order was found with ID: {orderID}"));
                return result;
            }

            return result.WithValue(order);
        }

        /// <summary>
        /// Removes an order and its associated detail rows from the database.
        /// </summary>
        /// <param name="orderID">The unique primary key of the order record.</param>
        /// <returns>A BYS Result container wrapping the total number of database rows affected.</returns>
        public async Task<Result<int>> DeleteOrderAsync(int orderID)
        {
            var result = new Result<int>();

            if (orderID <= 0)
            {
                result.AddError(new Error("Missing Information", "An order ID must be provided."));
                return result;
            }

            var order = await _context.Orders.Include(o => o.Order_Details).Where(o => o.OrderID == orderID).FirstOrDefaultAsync();

            if (order == null)
            {
                result.AddError(new Error("Missing Order", $"Order with an ID {orderID} does not exist."));
                return result;
            }

            // Performing a purge of child details lines before dropping the parent Order, since the Northwind database does not have a soft-delete flag (e.g., RemoveFromViewFlag)
            if (order.Order_Details.Any())
            {
                _context.Order_Details.RemoveRange(order.Order_Details);
            }

            // Staging the parent order context deletion
            _context.Orders.Remove(order);

            try
            {
                int rowsAffected = await _context.SaveChangesAsync();
                return result.WithValue(rowsAffected);
            }
            catch (Exception ex)
            {
                _context.ChangeTracker.Clear();

                result.AddError(new Error("Error Saving Changes", ex.InnerException?.Message ?? ex.Message));
                return result;
            }
        }
    }
}
