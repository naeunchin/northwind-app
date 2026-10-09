using Bunit;
using MudBlazor;
using MudBlazor.Services;

namespace UnitTests.Components
{
    /// <summary>
    /// Base class for bUnit component tests. Each test class instance gets its own render context and service container.
    /// </summary>
    public abstract class ComponentTestBase : BunitContext, IAsyncLifetime
    {
        protected ComponentTestBase()
        {
            // MudBlazor components call JS interop (e.g. for focus and popovers); loose mode returns default results instead of throwing
            JSInterop.Mode = JSRuntimeMode.Loose;

            Services.AddMudServices();
        }

        /// <summary>
        /// Renders the MudBlazor providers normally placed in MainLayout. Required by components that use popovers (MudDataGrid, MudSelect, menus).
        /// Call after registering services, since rendering locks the service container.
        /// </summary>
        protected void RenderMudProviders()
        {
            Render<MudPopoverProvider>();
        }

        Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;

        // MudBlazor's PopoverService only supports async disposal, and xUnit 2 only disposes test classes asynchronously through IAsyncLifetime
        async Task IAsyncLifetime.DisposeAsync() => await DisposeAsync();
    }
}
