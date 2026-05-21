using Microsoft.AspNetCore.Components;
using MudBlazor;
using OLTPSystem.BLL;
using OLTPSystem.ViewModels;
using System.Threading.Tasks;

namespace BlazorWebApp.Components.Pages.AppPages
{
    public partial class OrderEdit
    {
        #region Fields

        private OrderView editOrder = new();
        private List<OrderDetailView> orderItems = [];

        private List<CustomerLookupView> customerOptions = [];
        private List<EmployeeLookupView> employeeOptions = [];
        private List<ShipperLookupView> shipperOptions = [];
        private List<ProductView> productOptions = [];

        private MudForm orderForm = new();
        private bool isFormValid;
        private bool hasFormChanged;

        private DialogOptions _dialogOptions = new() { MaxWidth = MaxWidth.Small, FullWidth = true, CloseButton = true };

        private string feedbackMessage = string.Empty;
        private List<string> errorDetails = new();
        private string errorMessage = string.Empty;

        private bool hasError => !string.IsNullOrEmpty(errorMessage) || errorDetails.Any();
        private string closeButtonText => hasFormChanged ? "Cancel" : "Close";
        #endregion

        #region Parameters
        [Parameter]
        public int OrderID { get; set; }
        #endregion

        #region Injection & Properties
        [Inject]
        protected OrderService OrderService { get; set; } = default!;

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
                var customerResults = await OrderService.GetCustomersAsync();
                var employeeResults = await OrderService.GetEmployeesAsync();
                var shipperResults = await OrderService.GetShippersAsync();
                var productResults = await OrderService.GetProductsAsync();

                if (customerResults.IsSuccess) customerOptions = customerResults.Value ?? [];
                if (employeeResults.IsSuccess) employeeOptions = employeeResults.Value ?? [];
                if (shipperResults.IsSuccess) shipperOptions = shipperResults.Value ?? [];
                if (productResults.IsSuccess) productOptions = productResults.Value ?? [];

                if (OrderID > 0)
                {
                    var orderResult = await OrderService.GetOrderByIDAsync(OrderID);
                    
                    if (orderResult.IsSuccess)
                    {
                        editOrder = orderResult.Value ?? new();

                        var detailsResult = await OrderService.GetOrderDetailsAsync(OrderID);
                        if (detailsResult.IsSuccess)
                        {
                            orderItems.Clear();

                            if (detailsResult.Value != null)
                            {
                                orderItems.AddRange(detailsResult.Value);
                            }
                            else
                            {
                                errorMessage = "Failed to load order details.";
                                errorDetails.AddRange(detailsResult.Errors.Select(e => e.Message));
                            }
                        }
                    }
                    else
                    {
                        errorDetails = ErrorMessageHelperClass.GetErrorMessages(orderResult.Errors.ToList());
                    }
                }
                else
                {
                    editOrder = new OrderView
                    {
                        OrderID = 0,
                        OrderDate = DateTime.Today,
                        Freight = 0.00m
                    };
                    orderItems = [];
                }
            }
            catch (Exception ex)
            {
                errorMessage = "An error occurred while loading form structures.";
                errorDetails.Add(ex.GetBaseException().Message);
            }

            StateHasChanged();
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
                                                        "Are you sure you want to cancel editing this order? All unsaved changes will be lost.",
                                                        yesText: "Discard Changes",
                                                        noText: "Stay on Page");

                if (result != true)
                {
                    return;
                }
            }

            NavigationManager.NavigateTo("/orders");
        }

        /// <summary>
        /// Performs the creation or update of an order record.
        /// </summary>
        public async Task AddEditOrder()
        {
            ResetMessageStates();

            try
            {
                await orderForm.Validate();

                if (!isFormValid)
                {
                    errorMessage = "Form Validation Failed";
                    errorDetails.Add("Please correct all highlighted input selections before saving.");
                    Snackbar.Add("Cannot save order changes. Please check form validation errors.", Severity.Error);
                    return;
                }

                var result = await OrderService.AddEditOrderAsync(editOrder, orderItems);

                if (result.IsSuccess)
                {
                    editOrder = result.Value ?? new();

                    if (editOrder.OrderID > 0)
                    {
                        feedbackMessage = $"Order {editOrder.OrderID} was successfully saved and logged.";
                        Snackbar.Add("Changes saved successfully.", Severity.Success);

                        hasFormChanged = false;
                        isFormValid = false;
                        orderForm.ResetTouched();
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
                errorMessage = "An error occurred while saving the order.";
                errorDetails.Add(ex.GetBaseException().Message);
            }
        }

        /// <summary>
        /// Adds a new product line item to the order cart, or updates the quantity if it already exists.
        /// </summary>
        /// <param name="chosenProduct">The selected product object being added to the order.</param>
        /// <param name="quantity">The number of units being purchased.</param>
        /// <param name="discount">The percentage discount rate applied to the line item (represented as a fraction between 0.0 and 1.0).</param>
        public void AddProductLine(ProductView chosenProduct, short quantity, float discount)
        {
            ResetMessageStates();

            if (chosenProduct == null || chosenProduct.ProductID <= 0)
            {
                Snackbar.Add("Please select a valid product.", Severity.Warning);
                return;
            }

            if (quantity <= 0)
            {
                Snackbar.Add("Purchase quantity must be greater than zero.", Severity.Warning);
                return;
            }

            var existingItem = orderItems.FirstOrDefault(x => x.ProductID == chosenProduct.ProductID);
            if (existingItem != null)
            {
                existingItem.Quantity += quantity;
                Snackbar.Add($"Updated quantity for {chosenProduct.ProductName}.", Severity.Info);
            }
            else
            {
                var newItem = new OrderDetailView
                {
                    OrderID = editOrder.OrderID,
                    ProductID = chosenProduct.ProductID,
                    ProductName = chosenProduct.ProductName,
                    UnitPrice = chosenProduct.UnitPrice ?? 0.00m,
                    Quantity = quantity,
                    Discount = discount
                };

                orderItems.Add(newItem);
                Snackbar.Add($"Added '{chosenProduct.ProductName}' to the order.", Severity.Success);
            }

            hasFormChanged = true;
            StateHasChanged();
        }

        /// <summary>
        /// Drops a selected detail index row out of the order.
        /// </summary>
        /// <param name="productID"></param>
        public void RemoveProductLine(int productID)
        {
            ResetMessageStates();

            var targetItem = orderItems.FirstOrDefault(x => x.ProductID <= productID);
            if (targetItem != null)
            {
                orderItems.Remove(targetItem);
                Snackbar.Add($"Removed '{targetItem.ProductName}'.", Severity.Warning);
                hasFormChanged = true;
                StateHasChanged();
            }
        }

        /// <summary>
        /// Resets dialog input variables and opens the add product item popup window.
        /// </summary>
        public async Task OpenAddProductItemDialog()
        {
            // Product list is passed into the dialog component
            var parameters = new DialogParameters { ["ProductOptions"] = productOptions } ;

            // Launch the modal window 
            var dialog = await DialogService.ShowAsync<AddProductDialog>("Add Products", parameters, _dialogOptions);
            var result = await dialog.Result;

            if (result != null && !result.Canceled)
            {
            
                // Using a tuple to add the product items 
                var data = (Tuple<ProductView, short, float>)result.Data!;
                AddProductLine(data.Item1, data.Item2, data.Item3);
            }
        }

        /// <summary>
        /// Redirects user routing back to the orders page.
        /// </summary>
        protected void ReturnToOrders()
        {
            NavigationManager.NavigateTo("/orders");
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
