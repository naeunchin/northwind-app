using Microsoft.AspNetCore.Components;
using MudBlazor;
using OLTPSystem.BLL;
using OLTPSystem.ViewModels;

namespace BlazorWebApp.Components.Pages.AppPages
{
    public partial class Orders
    {
        #region Fields
        private string searchTerm = string.Empty;
        private List<OrderView> OrderList = [];

        private string feedbackMessage = string.Empty;
        private List<string> errorDetails = new();
        private string errorMessage = string.Empty;
        private bool hasError => !string.IsNullOrEmpty(errorMessage) || errorDetails.Any();
        #endregion

        #region Injection & Properties
        [Inject]
        protected OrderService OrderService { get; set; } = default!;

        [Inject]
        protected NavigationManager NavigationManager { get; set; } = default!;

        [Inject]
        protected ISnackbar Snackbar { get; set; } = default!;

        [Inject]
        protected IDialogService DialogService { get; set; } = default!;
        #endregion

        #region Methods
        protected override async Task OnInitializedAsync()
        {
            ResetMessageStates();
            await LoadAllOrders();
        }

        /// <summary>
        /// Retrieves all order records.
        /// </summary>
        /// <returns>A task container wrapping the data stream retrieval block.</returns>
        private async Task LoadAllOrders()
        {
            try
            {
                var result = await OrderService.GetOrdersAsync();
                if (result.IsSuccess)
                {
                    OrderList = result.Value;
                }
                else
                {
                    errorDetails = ErrorMessageHelperClass.GetErrorMessages(result.Errors.ToList());
                }
            }
            catch (Exception ex)
            {
                errorDetails.Add(ex.GetBaseException().Message);
            }
        }

        /// <summary>
        /// Queries order records based on either a order ID or customer ID code.
        /// </summary>
        /// <returns>An asynchronous process handler tracking data payload filtering.</returns>
        public async Task GetOrders()
        {
            ResetMessageStates();

            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                await LoadAllOrders();
                return;
            }

            try
            {
                var result = await OrderService.LookupOrders(searchTerm);
                if (result.IsSuccess)
                {
                    OrderList = result.Value;
                    feedbackMessage = $"Found {OrderList.Count} orders matching the search term.";
                }
                else
                {
                    errorMessage = "No results found.";
                    errorDetails = ErrorMessageHelperClass.GetErrorMessages(result.Errors.ToList());
                }
            }
            catch (Exception ex)
            {
                errorDetails.Add(ex.Message);
            }
        }

        /// <summary>
        /// Redirects application routing to the editing user interface for an order with a specified ID.
        /// </summary>
        /// <param name="orderID">The unique primary key of the target order record.</param>
        public void EditOrder(int orderID)
        {
            NavigationManager.NavigateTo($"/OrderEdit/{orderID}");
        }

        /// <summary>
        /// Redirects application routing to the editing user interface to add a new order record.
        /// </summary>
        public void NewOrder()
        {
            NavigationManager.NavigateTo("/OrderEdit/0");
        }

        /// <summary>
        /// Performs a physical deletion of a specified order record from the database, along with its children.
        /// </summary>
        /// <param name="orderID">The unique primary key of the target order record.</param>
        /// <returns>A task tracking the user interaction response state machine and safe service layer execution sequence.</returns>
        public async Task DeleteOrder(int orderID)
        {
            ResetMessageStates();

            bool? confirmResult = await DialogService.ShowMessageBox(
                "CRITICAL WARNING: Order Deletion",
                $"Are you sure you want to permanently erase Order #{orderID}? This will also delete all child invoice details items. This action cannot be undone.",
                yesText: "Confirm Deletion",
                noText: "Cancel");

            if (confirmResult == true)
            {
                try
                {
                    var result = await OrderService.DeleteOrderAsync(orderID);
                    if (result.IsSuccess)
                    {
                        Snackbar.Add($"Order #{orderID} has been successfully deleted.", Severity.Success);
                        await LoadAllOrders();
                    }
                    else
                    {
                        errorDetails = ErrorMessageHelperClass.GetErrorMessages(result.Errors.ToList());
                        Snackbar.Add("Failed to complete record deletion.", Severity.Error);
                    } 
                }
                catch (Exception ex)
                {
                    errorDetails.Add(ex.Message);
                }
            }
        }

        private void ResetMessageStates()
        {
            errorMessage = string.Empty;
            feedbackMessage = string.Empty;
            errorDetails.Clear();
        }
        #endregion
    }
}
