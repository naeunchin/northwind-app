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
        /// Applies business rules and validations to commit insertion or modification of order records.
        /// </summary>
        /// <param name="editOrder">The view model transaction state submitted from the user interface.</param>
        /// <returns>A BYS Result container wrapping the refreshed order state or errors.</returns>
        public async Task<Result<OrderView>> AddEditOrderAsync(OrderView editOrder, List<OrderDetailView> orderItems)
        {
            var result = new Result<OrderView>();

            if (editOrder == null)
            {
                result.AddError(new Error("Missing Information", "No order was provided."));
                return result;
            }

            #region Business Logic & Validations
            if (string.IsNullOrWhiteSpace(editOrder.CustomerID))
                result.AddError(new Error("Missing Information", "A customer account is required."));

            if (editOrder.EmployeeID == null || editOrder.EmployeeID <= 0)
                result.AddError(new Error("Missing Information", "An employee must be assigned."));

            if (orderItems == null || !orderItems.Any())
                result.AddError(new Error("Validation Failure", "An order must contain at least one product line item to be processed."));

            if (result.IsFailure)
                return result;
            #endregion

            Order? order = await _context.Orders
                .Include(o => o.Order_Details)
                .Where(o => o.OrderID == editOrder.OrderID)
                .FirstOrDefaultAsync();

            if (order == null && editOrder.OrderID == 0)
            {
                order = new Order();
            }
            else if (order == null && editOrder.OrderID != 0)
            {
                result.AddError(new Error("Cannot find an order to edit", $"Order ID {editOrder.OrderID} cannot be found, edits cannot be made."));
                return result;
            }

            order.CustomerID = editOrder.CustomerID;
            order.EmployeeID = editOrder.EmployeeID;
            order.OrderDate = editOrder.OrderDate ?? DateTime.Today;
            order.ShippedDate = editOrder.ShippedDate;
            order.Freight = editOrder.Freight ?? 0.00m;
            order.ShipCity = editOrder.ShipCity;
            order.ShipCountry = editOrder.ShipCountry;

            if (order.OrderID == 0)
                _context.Orders.Add(order);
            else
                _context.Orders.Update(order);

            try
            {
                await _context.SaveChangesAsync();

                var refreshQuery = await LookupOrders(order.OrderID.ToString());
                if (refreshQuery.IsSuccess && refreshQuery.Value.Any())
                {
                    return result.WithValue(refreshQuery.Value.First());
                }

                result.AddError(new Error("Sync Fault", "Order committed, but data tracking failed to refresh cleanly."));
                return result;
            }
            catch (Exception ex)
            {
                _context.ChangeTracker.Clear();
                result.AddError(new Error("Error Saving Changes", ex.InnerException?.Message ?? ex.Message));
                return result;
            }
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

        /// <summary>
        /// Retrieves all child line items associated with a specific parent order.
        /// </summary>
        /// <param name="orderID">The unique primary key of the parent order.</param>
        public async Task<Result<List<OrderDetailView>>> GetOrderDetailsAsync(int orderID)
        {
            var result = new Result<List<OrderDetailView>>();

            var items = await _context.Order_Details
                                                .Include(od => od.Product)
                                                .Where(od => od.OrderID == orderID)
                                                .Select(od => new OrderDetailView
                                                {
                                                    OrderID = od.OrderID,
                                                    ProductID = od.ProductID,
                                                    ProductName = od.Product != null ? od.Product.ProductName : "Unknown Product",
                                                    UnitPrice = od.UnitPrice,
                                                    Quantity = od.Quantity,
                                                    Discount = od.Discount
                                                })
                                                .ToListAsync();

            return result.WithValue(items);
        }

        /// <summary>
        /// Retrieves all customers to populate selection dropdown menus in the user interface.
        /// </summary>
        /// <returns></returns>
        public async Task<Result<List<CustomerLookupView>>> GetCustomersAsync()
        {
            var result = new Result<List<CustomerLookupView>>();

            var data = await _context.Customers
                                            .Select(c => new CustomerLookupView
                                            {
                                                CustomerID = c.CustomerID,
                                                CompanyName = c.CompanyName
                                            })
                                            .OrderBy(c => c.CompanyName)
                                            .ToListAsync();

            return result.WithValue(data);
        }

        /// <summary>
        /// Retrieves all employees to populate selection dropdown menus in the user interface.
        /// </summary>
        public async Task<Result<List<EmployeeLookupView>>> GetEmployeesAsync()
        {
            var result = new Result<List<EmployeeLookupView>>();

            var data = await _context.Employees
                                            .Select(e => new EmployeeLookupView
                                            {
                                                EmployeeID = e.EmployeeID,
                                                FullName = e.FirstName + " " + e.LastName
                                            })
                                            .OrderBy(e => e.FullName)
                                            .ToListAsync();

            return result.WithValue(data);
        }

        /// <summary>
        /// Retrieves all shipping vendors to populate selection dropdown menus in the user interface.
        /// </summary>
        public async Task<Result<List<ShipperLookupView>>> GetShippersAsync()
        {
            var result = new Result<List<ShipperLookupView>>();

            var data = await _context.Shippers
                                            .Select(s => new ShipperLookupView
                                            {
                                                ShipperID = s.ShipperID,
                                                CompanyName = s.CompanyName
                                            })
                                            .OrderBy(s => s.CompanyName)
                                            .ToListAsync();

            return result.WithValue(data);
        }

        /// <summary>
        /// Retrieves all active products.
        /// </summary>
        public async Task<Result<List<ProductView>>> GetProductsAsync()
        {
            var result = new Result<List<ProductView>>();

            var data = await _context.Products
                                            .Where(p => !p.Discontinued)
                                            .Select(p => new ProductView
                                            {
                                                ProductID = p.ProductID,
                                                ProductName = p.ProductName,
                                                UnitPrice = p.UnitPrice ?? 0.00m
                                            })
                                            .OrderBy(p => p.ProductName)
                                            .ToListAsync();

            return result.WithValue(data);
        }
    }
}
