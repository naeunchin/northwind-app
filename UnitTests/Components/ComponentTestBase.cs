using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
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

        /// <summary>
        /// Renders the MudDialogProvider that hosts dialogs and message boxes opened through IDialogService.
        /// Dialog markup appears inside the returned component, not the component under test.
        /// </summary>
        protected IRenderedComponent<MudDialogProvider> RenderDialogProvider()
        {
            return Render<MudDialogProvider>();
        }

        /// <summary>
        /// Finds and clicks an element in a single step on the renderer's dispatcher. Buttons whose OnClick is a lambda
        /// (such as per-row grid buttons) get a new event handler ID on every render, so a render from async page work landing
        /// between a separate find and click would leave the click pointing at a stale handler.
        /// </summary>
        protected static Task ClickAsync<TComponent>(IRenderedComponent<TComponent> cut, Func<IElement> findElement)
            where TComponent : IComponent
        {
            return cut.InvokeAsync(() => findElement().ClickAsync(new MouseEventArgs()));
        }

        Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;

        // MudBlazor's PopoverService only supports async disposal, and xUnit 2 only disposes test classes asynchronously through IAsyncLifetime
        async Task IAsyncLifetime.DisposeAsync() => await DisposeAsync();
    }
}
