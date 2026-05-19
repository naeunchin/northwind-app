using Microsoft.AspNetCore.Components;
using OLTPSystem.BLL;
using OLTPSystem.ViewModels;
using System.Threading.Tasks;

namespace BlazorWebApp.Components.Pages.AppPages
{
    public partial class Inventory
    {
        #region Fields
        private string productName = string.Empty;
        private string searchTerm = string.Empty;
        private List<ProductView> ProductList = [];

        private string feedbackMessage = string.Empty;
        private List<string> errorDetails = new();
        private string errorMessage = string.Empty;
        #endregion

        #region Injection & Properties
        [Inject]
        protected ProductService ProductService { get; set; } = default!;

        [Inject]
        protected NavigationManager NavigationManager { get; set; } = default!;

        private bool hasError => !string.IsNullOrEmpty(errorMessage) || errorDetails.Any();
        #endregion

        #region Methods
        protected override async Task OnInitializedAsync()
        {
            try
            {
                var result = await ProductService.GetProductsAsync();

                if (result.IsSuccess)
                {
                    ProductList = result.Value;
                }
                else
                {
                    errorMessage = "Failed to load the inventory";
                    errorDetails = ErrorMessageHelperClass.GetErrorMessages(result.Errors.ToList());
                }
            }
            catch (Exception ex)
            {
                errorMessage = "An error occurred while connecting to the database";
                errorDetails.Add(ex.Message);
            }
        }

        /// <summary>
        /// Performs the asynchronous lookup of product inventory data based on the user's current selection filter input.
        /// </summary>
        public async Task GetProducts()
        {
            errorDetails.Clear();
            errorMessage = string.Empty;
            feedbackMessage = string.Empty;
            ProductList = [];

            try
            {
                BYSResults.Result<List<ProductView>> result;

                if (string.IsNullOrWhiteSpace(searchTerm))
                {
                    errorMessage = "Search Validation Failed";
                    errorDetails.Add("Please enter a product name or a product ID to search.");
                    return;
                }

                if (int.TryParse(searchTerm, out int parsedProductID))
                {
                    // If it contains numbers, search by ID
                    result = await ProductService.LookupProducts(parsedProductID);
                }
                else
                {
                    // If it contains letters, search by partial name
                    result = await ProductService.LookupProducts(searchTerm);
                }

                if (result.IsSuccess)
                {
                    ProductList = result.Value;
                    feedbackMessage = $"Successfully loaded {ProductList.Count} inventory records.";
                }
                else
                {
                    errorDetails = ErrorMessageHelperClass.GetErrorMessages(result.Errors.ToList());
                }
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
            }
        }

        public void EditProduct(int productID)
        {
            NavigationManager.NavigateTo($"/ProductEdit/{productID}");
        }
        public void NewProduct()
        {
            NavigationManager.NavigateTo("/ProductEdit/0");
        }
        #endregion
    }
}
