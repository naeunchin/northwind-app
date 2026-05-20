using Microsoft.AspNetCore.Components;
using MudBlazor;
using OLTPSystem.BLL;
using OLTPSystem.ViewModels;
using System.Threading.Tasks;

namespace BlazorWebApp.Components.Pages.AppPages
{
    public partial class InventoryEdit
    {
        #region Fields
        
        private ProductView editProduct = new();

        private string feedbackMessage = string.Empty;
        private List<string> errorDetails = new();
        private string errorMessage = string.Empty;

        private List<CategoryView> categoryOptions = [];
        private List<SupplierView> supplierOptions = [];

        private MudForm productForm = new();
        private bool isFormValid;
        private bool hasFormChanged;

        private bool hasError => !string.IsNullOrEmpty(errorMessage) || errorDetails.Any();
        private string closeButtonText => hasFormChanged ? "Cancel" : "Close";
        #endregion

        #region Parameters
        [Parameter]
        public int ProductID { get; set; }
        #endregion

        #region Injection & Properties
        [Inject]
        protected ProductService ProductService { get; set; } = default!;

        [Inject]
        protected NavigationManager NavigationManager { get; set; } = default!;

        [Inject]
        protected IDialogService DialogService { get; set; } = default!;

        [Inject]
        protected ISnackbar Snackbar { get; set; } = default!;
        #endregion

        #region Methods
        protected override async Task OnInitializedAsync()
        {
            ResetMessageStates();

            try
            {
                var categoryResults = await ProductService.GetCategoriesAsync();
                var supplierResults = await ProductService.GetSuppliersAsync();

                if (categoryResults.IsSuccess)
                    categoryOptions = categoryResults.Value ?? [];

                if (supplierResults.IsSuccess)
                    supplierOptions = supplierResults.Value ?? [];

                if (ProductID > 0)
                {
                    var productResult = await ProductService.GetProductByIDAsync(ProductID);

                    if (productResult.IsSuccess)
                    {
                        editProduct = productResult.Value ?? new();
                    }
                    else
                    {
                        errorDetails = ErrorMessageHelperClass.GetErrorMessages(productResult.Errors.ToList());
                    }
                }
                else
                {
                    editProduct = new ProductView
                    {
                        ProductID = 0,
                        Discontinued = false,
                        UnitPrice = 0.00m,
                        UnitsInStock = 0,
                        UnitsOnOrder = 0,
                        ReorderLevel = 0
                    };
                }
            }
            catch (Exception ex)
            {
                errorMessage = "An error occurred while loading form structures.";
                errorDetails.Add(ex.GetBaseException().Message);
            }
        }

        /// <summary>
        /// Prompts user before discarding changes if the form state has been modified.
        /// </summary>
        public async Task Cancel()
        {
            if (hasFormChanged)
            {
                bool? result = await DialogService.ShowMessageBox(
                                                        "Confirm Cancel",
                                                        "Are you sure you want to cancel editing this product? All unsaved changes will be lost.",
                                                        yesText: "Discard Changes",
                                                        noText: "Stay on Page");

                if (result != true)
                {
                    return;
                }
            }

            NavigationManager.NavigateTo("/inventory");
        }

        public async Task AddEditProduct()
        {
            ResetMessageStates();

            try
            {
                await productForm.Validate();

                if (!isFormValid)
                {
                    errorMessage = "Form Validation Failed";
                    errorDetails.Add("Please correct all red highlighted input fields before saving.");
                    Snackbar.Add("Cannot save product. Please check form validation errors.", Severity.Error);
                    return;
                }

                var result = await ProductService.AddEditProduct(editProduct);

                if (result.IsSuccess)
                {
                    editProduct = result.Value ?? new();

                    if (editProduct.ProductID > 0)
                    {
                        feedbackMessage = $"Product {editProduct.ProductName} was successfully saved.";
                        Snackbar.Add("Changes saved successully.", Severity.Success);

                        hasFormChanged = false;
                        isFormValid = false;
                        productForm.ResetTouched();
                    }
                }
                else
                {
                    errorDetails = ErrorMessageHelperClass.GetErrorMessages(result.Errors.ToList());
                    Snackbar.Add("Database rejected changes. Please review errors.", Severity.Error);
                }
            }
            catch (Exception ex)
            {
                errorMessage = "An error occurred while saving the product.";
                errorDetails.Add(ex.GetBaseException().Message);
            }
        }

        protected void ReturnToInventory()
        {
            NavigationManager.NavigateTo("/inventory");
        }

        /// <summary>
        /// Resets error and feedback message fields.
        /// </summary>
        private void ResetMessageStates()
        {
            errorMessage = string.Empty;
            feedbackMessage = string.Empty;
            errorDetails.Clear();
        }
        #endregion
    }
}
