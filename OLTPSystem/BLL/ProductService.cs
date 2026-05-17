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
        /// Queries product records that belong to a specific category by its identification number.
        /// </summary>
        /// <param name="categoryID">The unique primary key of the category.</param>
        /// <returns>A BYS Result container wrapping a list of matching ProductView records or error messages.</returns>
        public async Task<Result<List<ProductView>>> LookupProducts(int categoryID)
        {
            var result = new Result<List<ProductView>>();

            if (categoryID <= 0)
            {
                result.AddError(new Error("Invalid ID", "A valid category ID integer must be provided"));
                return result;
            }

            var products = await _context.Products
                .Where(p => p.CategoryID == categoryID)
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
                result.AddError(new Error("No Products Found", $"No inventory products were found matching Category ID: {categoryID}."));
                return result;
            }

            return result.WithValue(products);
        }
    }
}
