using Microsoft.AspNetCore.Components;

namespace BlazorWebApp.Components.Custom
{
    public partial class FeedbackAndErrorDisplay
    {
        //We can have parameters for custom components, as pages do. Parameters can be used to pass data from the parent (page) to the component. 
        #region Parameters
        [Parameter]
        public List<string> ErrorDetails { get; set; } = [];

        [Parameter]
        public string ErrorMessage { get; set; } = string.Empty;

        [Parameter]
        public string FeedbackMessage { get; set; } = string.Empty;

        private bool hasError => !string.IsNullOrEmpty(ErrorMessage) || ErrorDetails.Any();
        #endregion
    }
}
