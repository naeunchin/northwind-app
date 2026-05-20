using BYSResults;
using Microsoft.EntityFrameworkCore;
using OLTPSystem.DAL;
using OLTPSystem.Entities;
using OLTPSystem.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OLTPSystem.BLL
{
    public class ProductService
    {
        #region Data context setup
        private readonly NorthwindContext _context;

        public ProductService(NorthwindContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }
        #endregion

        /// <summary>
        /// Queries all product records from the database.
        /// </summary>
        /// <returns>A BYS Result container wrapping a list of Product view or error messages.</returns>
        public async Task<Result<List<ProductView>>> GetProductsAsync()
        {
            var result = new Result<List<ProductView>>();

            var products = await _context.Products.Select(p => new ProductView
            {
                ProductID = p.ProductID,
                ProductName = p.ProductName,
                SupplierID = p.SupplierID,
                CategoryID = p.CategoryID,
                QuantityPerUnit = p.QuantityPerUnit,
                UnitPrice = p.UnitPrice,
                UnitsInStock = p.UnitsInStock,
                UnitsOnOrder = p.UnitsOnOrder,
                ReorderLevel = p.ReorderLevel,
                Discontinued = p.Discontinued,
                CategoryName = p.Category != null ? p.Category.CategoryName : "Uncategorized",
                SupplierCompanyName = p.Supplier != null ? p.Supplier.CompanyName : "No Supplier Listed"
            }).OrderBy(p => p.ProductName).ToListAsync();

            if (products == null || products.Count == 0)
            {
                result.AddError(new Error("No records found", "No product records were found in the database."));
                return result;
            }

            return result.WithValue(products);
        }

        /// <summary>
        /// Queries product records where the product name contains the partial search string.
        /// </summary>
        /// <param name="productName">The string text or partial name to search for.</param>
        /// <returns>A BYS Result container wrapping a list of matching ProductView records or error messages.</returns>
        public async Task<Result<List<ProductView>>> LookupProducts(string productName)
        {
            var result = new Result<List<ProductView>>();

            if (string.IsNullOrWhiteSpace(productName)) 
            {
                result.AddError(new Error("Missing information", "A product search term must be provided."));
                return result;
            }

            var products = await _context.Products
                .Where(p => p.ProductName.ToLower().Contains(productName.ToLower()))
                .Select(p => new ProductView
            {
                ProductName = p.ProductName,
                SupplierID = p.SupplierID,
                CategoryID = p.CategoryID,
                QuantityPerUnit = p.QuantityPerUnit,
                UnitPrice = p.UnitPrice,
                UnitsInStock = p.UnitsInStock,
                UnitsOnOrder = p.UnitsOnOrder,
                ReorderLevel = p.ReorderLevel,
                Discontinued = p.Discontinued,
                CategoryName = p.Category != null ? p.Category.CategoryName : "Uncategorized",
                SupplierCompanyName = p.Supplier != null ? p.Supplier.CompanyName : "No Supplier Listed"
            }).OrderBy(p => p.ProductName).ToListAsync();

            if (products == null || products.Count <= 0)
            {
                result.AddError(new Error("No Products Found", $"No products were located containing the term: '{productName}'."));
                return result;
            }

            return result.WithValue(products);
        }

        /// <summary>
        /// Queries a specific product record by its unique identification number.
        /// </summary>
        /// <param name="productID">The unique primary key of the product.</param>
        /// <returns>A BYS Result container wrapping a list containing the single matching ProductView.</returns>
        public async Task<Result<List<ProductView>>> LookupProducts(int productID)
        {
            var result = new Result<List<ProductView>>();

            if (productID <= 0)
            {
                result.AddError(new Error("Invalid ID", "A valid Product ID integer must be provided."));
                return result;
            }

            var products = await _context.Products
                .Where(p => p.ProductID == productID)
                .Select(p => new ProductView
                {
                    ProductID = p.ProductID,
                    ProductName = p.ProductName,
                    SupplierID = p.SupplierID,
                    CategoryID = p.CategoryID,
                    QuantityPerUnit = p.QuantityPerUnit,
                    UnitPrice = p.UnitPrice,
                    UnitsInStock = p.UnitsInStock,
                    UnitsOnOrder = p.UnitsOnOrder,
                    ReorderLevel = p.ReorderLevel,
                    Discontinued = p.Discontinued,
                    CategoryName = p.Category != null ? p.Category.CategoryName : "Uncategorized",
                    SupplierCompanyName = p.Supplier != null ? p.Supplier.CompanyName : "No Supplier Listed"
                }).ToListAsync();

            if (products == null || products.Count <= 0)
            {
                result.AddError(new Error("Not Found", $"No inventory product was found matching ID: {productID}."));
                return result;
            }

            return result.WithValue(products);
        }

        /// <summary>
        /// Retrieves a single product record by its primary key.
        /// </summary>
        /// <param name="productID"></param>
        /// <returns></returns>
        public async Task<Result<ProductView>> GetProductByIDAsync(int productID)
        {
            var result = new Result<ProductView>();

            var product = await _context.Products
                                                .Where(p => p.ProductID == productID)
                                                .Select(p => new ProductView
                                                {
                                                    ProductID = productID,
                                                    ProductName = p.ProductName,
                                                    SupplierID = p.SupplierID,
                                                    CategoryID = p.CategoryID,
                                                    QuantityPerUnit = p.QuantityPerUnit,
                                                    UnitPrice = p.UnitPrice,
                                                    UnitsInStock = p.UnitsInStock,
                                                    UnitsOnOrder = p.UnitsOnOrder,
                                                    ReorderLevel = p.ReorderLevel,
                                                    Discontinued = p.Discontinued,
                                                    CategoryName = p.Category != null ? p.Category.CategoryName : "Uncategorized",
                                                    SupplierCompanyName = p.Supplier != null ? p.Supplier.CompanyName : "No Supplier listed"
                                                })
                                                .FirstOrDefaultAsync();
            if (product == null)
            {
                result.AddError(new Error("No Product", $"No product was found with ID: {productID}"));
                return result;
            }

            return result.WithValue(product);
        }

        /// <summary>
        /// Applies business rules and validations to commit insertion or modification of product records.
        /// </summary>
        /// <param name="editProduct">The view model transaction state suvmitted from the user interface.</param>
        /// <returns>A BYS Result container wrapping the refreshed product state or errors.</returns>
        public async Task<Result<ProductView>> AddEditProduct(ProductView editProduct)
        {
            var result = new Result<ProductView>();

            if (editProduct == null)
            {
                result.AddError(new Error("Missing Information", "No product was provided."));
                return result;
            }

            #region Business logic & validation
            
            // Mandatory string fields
            if (string.IsNullOrWhiteSpace(editProduct.ProductName))
                result.AddError(new Error("Missing Information", "Product name is required."));

            // FK relationships must be explicitly mapped 
            if (editProduct.CategoryID == null || editProduct.CategoryID <= 0)
                result.AddError(new Error("Missing Information", "A valid category classification must be assigned."));

            if (editProduct.SupplierID == null || editProduct.SupplierID <= 0)
                result.AddError(new Error("Missing Information", "A valid supplier organization must be assigned."));

            // Prevent negative inputs
            if (editProduct.UnitPrice != null && editProduct.UnitPrice < 0)
                result.AddError(new Error("Invalid Value", "Unit Price cannot be less than $0.00."));

            if (editProduct.UnitsInStock != null && editProduct.UnitsInStock < 0)
                result.AddError(new Error("Invalid Value", "Units In Stock inventory totals cannot be negative."));

            if (editProduct.UnitsOnOrder != null && editProduct.UnitsOnOrder < 0)
                result.AddError(new Error("Invalid Value", "Units On Order transactional backlogs cannot be negative."));

            if (editProduct.ReorderLevel != null && editProduct.ReorderLevel < 0)
                result.AddError(new Error("Invalid Value", "Reorder Threshold safety levels cannot be negative."));

            // Duplicate record check: A product name cannot match an existing item unless it belongs to the row currently being edited 
            bool existingProduct = await _context.Products
                                                 .AnyAsync(x => x.ProductName.ToLower() == editProduct.ProductName.ToLower() && x.ProductID != editProduct.ProductID);

            if (existingProduct)
            {
                result.AddError(new Error("Duplicate Data Warning", $"An inventory item named {editProduct.ProductName} already exists in the database and cannot be entered again."));
            }

            // If any validation checks failed, exit early and display the errors 
            if (result.IsFailure)
                return result;
            #endregion

            Product? product = await _context.Products.Where(x => x.ProductID == editProduct.ProductID).FirstOrDefaultAsync();

            if (product == null && editProduct.ProductID == 0)
            {
                product = new();
            }
            else if (product == null && editProduct.ProductID != 0)
            {
                result.AddError(new Error("Cannot find a record to edit", $"Product ID {editProduct.ProductID} cannot be found, edits cannot be made."));
                return result;
            }

            product.ProductName = editProduct.ProductName;
            product.SupplierID = editProduct.SupplierID;
            product.CategoryID = editProduct.CategoryID;
            product.QuantityPerUnit = editProduct.QuantityPerUnit;
            product.UnitPrice = editProduct.UnitPrice;
            product.UnitsInStock = editProduct.UnitsInStock;
            product.UnitsOnOrder = editProduct.UnitsOnOrder;
            product.ReorderLevel = editProduct.ReorderLevel;
            product.Discontinued = editProduct.Discontinued;

            if (product.ProductID == 0)
                _context.Products.Add(product);
            else
                _context.Products.Update(product);

            try
            {
                await _context.SaveChangesAsync();
                return await GetProductByIDAsync(product.ProductID);
            }
            catch (Exception ex)
            {
                _context.ChangeTracker.Clear();
                result.AddError(new Error("Error Saving Changes", ex.InnerException?.Message ?? string.Empty));
                return result;
            }
        }

        /// <summary>
        /// Retrieves all categories to populate selection dropdown menus in the user interface.
        /// </summary>
        /// <returns>A list of categories.</returns>
        public async Task<Result<List<CategoryView>>> GetCategoriesAsync()
        {
            var result = new Result<List<CategoryView>>();
            var data = await _context.Categories.Select(c => new CategoryView
            {
                CategoryID = c.CategoryID,
                CategoryName = c.CategoryName
            }).OrderBy(c => c.CategoryName).ToListAsync();

            return result.WithValue(data);
        }

        /// <summary>
        /// Retrieves all suppliers to populate selection dropdown menus in the user interface.
        /// </summary>
        /// <returns>A list of suppliers.</returns>
        public async Task<Result<List<SupplierView>>> GetSuppliersAsync()
        {
            var result = new Result<List<SupplierView>>();
            var data = await _context.Suppliers.Select(s => new SupplierView
            {
                SupplierID = s.SupplierID,
                SupplierCompanyName = s.CompanyName
            }).OrderBy(s => s.SupplierCompanyName).ToListAsync();

            return result.WithValue(data);
        }
    }
}
