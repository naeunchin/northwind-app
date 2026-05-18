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
        private int categoryID = 0;
        private string feedbackMessage = string.Empty;
        private List<string> errorDetails = new();
        private string errorMessage = string.Empty;
        private List<ProductView> ProductList = [];
        #endregion

        #region Injection & Properties
        [Inject]
        protected ProductService ProductService { get; set; } = default!;

        [Inject]
        protected NavigationManager NavigationManager { get; set; } = default!;

        private bool hasError => !string.IsNullOrEmpty(errorMessage) || errorDetails.Any();
        #endregion

        #region Methods
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

                if (!string.IsNullOrWhiteSpace(productName))
                {
                    result = await ProductService.LookupProducts(productName);
                }
                else if (categoryID > 0)
                {
                    result = await ProductService.LookupProducts(categoryID);
                }
                else
                {
                    errorMessage = "Search Validation Failed";
                    errorDetails.Add("Please enter a partial Product Name or select a valid Category to query inventory records.");
                    return;
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
