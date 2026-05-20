using Microsoft.AspNetCore.Components;
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

        private bool hasError => !string.IsNullOrEmpty(errorMessage) || errorDetails.Any();
        #endregion

        #region Methods
        
        #endregion
    }
}
