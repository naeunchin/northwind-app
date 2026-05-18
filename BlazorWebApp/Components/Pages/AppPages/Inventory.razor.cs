using Microsoft.AspNetCore.Components;
using OLTPSystem.BLL;
using OLTPSystem.ViewModels;

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
        public void GetProducts()
        {
            errorDetails.Clear();
            errorMessage = string.Empty;
            feedbackMessage = string.Empty;
            ProductList = [];

            try
            {
                BYSResults.Result<List<ProductView>> result;
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
