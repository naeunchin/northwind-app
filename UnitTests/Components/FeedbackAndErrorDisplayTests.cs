using BlazorWebApp.Components.Custom;
using Bunit;
using FluentAssertions;

namespace UnitTests.Components
{
    public class FeedbackAndErrorDisplayTests : ComponentTestBase
    {
        [Fact]
        public void Render_NoMessages_RendersNothing()
        {
            // Act
            var cut = Render<FeedbackAndErrorDisplay>();

            // Assert
            cut.FindAll(".mud-alert").Should().BeEmpty();
        }

        [Fact]
        public void Render_ErrorMessageAndDetails_RendersErrorAlertWithEachDetail()
        {
            // Act
            var cut = Render<FeedbackAndErrorDisplay>(parameters => parameters
                .Add(p => p.ErrorMessage, "No results found.")
                .Add(p => p.ErrorDetails, new List<string> { "First problem", "Second problem" }));

            // Assert
            var alert = cut.Find(".mud-alert");
            alert.ClassList.Should().Contain("mud-alert-outlined-error");
            alert.TextContent.Should().Contain("No results found.")
                .And.Contain("First problem")
                .And.Contain("Second problem");
        }

        [Fact]
        public void Render_ErrorDetailsWithoutMessage_StillRendersErrorAlert()
        {
            // Act
            var cut = Render<FeedbackAndErrorDisplay>(parameters => parameters
                .Add(p => p.ErrorDetails, new List<string> { "Database unavailable" }));

            // Assert
            cut.Find(".mud-alert").TextContent.Should().Contain("Database unavailable");
        }

        [Fact]
        public void Render_FeedbackMessageOnly_RendersSuccessAlert()
        {
            // Act
            var cut = Render<FeedbackAndErrorDisplay>(parameters => parameters
                .Add(p => p.FeedbackMessage, "Found 2 orders matching the search term."));

            // Assert
            var alert = cut.Find(".mud-alert");
            alert.ClassList.Should().Contain("mud-alert-outlined-success");
            alert.TextContent.Should().Contain("Found 2 orders matching the search term.");
        }

        [Fact]
        public void Render_ParametersUpdated_SwitchesFromErrorToFeedback()
        {
            // Arrange
            var cut = Render<FeedbackAndErrorDisplay>(parameters => parameters
                .Add(p => p.ErrorMessage, "No results found."));

            // Act
            cut.Render(parameters => parameters
                .Add(p => p.ErrorMessage, string.Empty)
                .Add(p => p.FeedbackMessage, "Saved."));

            // Assert
            var alerts = cut.FindAll(".mud-alert");
            alerts.Should().ContainSingle();
            alerts[0].ClassList.Should().Contain("mud-alert-outlined-success");
        }
    }
}
