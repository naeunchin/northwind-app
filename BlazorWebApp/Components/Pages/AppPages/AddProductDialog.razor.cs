using Microsoft.AspNetCore.Components;
using MudBlazor;
using OLTPSystem.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BlazorWebApp.Components.Pages.AppPages
{
    public partial class AddProductDialog
    {
        [CascadingParameter]
        IMudDialogInstance MudDialog { get; set; } = default!;

        [Parameter]
        public List<ProductView> ProductOptions { get; set; } = new();

        private int _selectedProductID = 0;
        private short _quantity = 1;
        private int _discountPercent = 0;

        private void Submit()
        {
            var chosenProduct = ProductOptions.FirstOrDefault(p => p.ProductID == _selectedProductID);
            if (chosenProduct != null)
            {
                float actualDiscount = _discountPercent / 100f;

                // A single object that accepts 3 pieces of data without creating a new class
                var resultData = new Tuple<ProductView, short, float>(chosenProduct, _quantity, actualDiscount);
                MudDialog.Close(DialogResult.Ok(resultData));
            }
        }

        private void Cancel() => MudDialog.Cancel();
    }
}
