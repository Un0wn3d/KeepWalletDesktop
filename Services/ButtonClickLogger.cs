using Avalonia.Controls;
using Avalonia.Interactivity;
using KeepWalletDesktop.ViewModels;
using Serilog;
using System.Linq;

namespace KeepWalletDesktop.Services;

internal static class ButtonClickLogger
{
    private static readonly ILogger UiLog = Log.ForContext("SourceContext", "ButtonClickLogger");
    private static bool isInitialized;

    public static void Initialize()
    {
        if (isInitialized)
        {
            return;
        }

        Button.ClickEvent.AddClassHandler<Button>(LogButtonClick, RoutingStrategies.Bubble, handledEventsToo: true);
        isInitialized = true;
    }

    private static void LogButtonClick(Button button, RoutedEventArgs e)
    {
        var mainViewModel = TopLevel.GetTopLevel(button) is Window { DataContext: MainViewModel viewModel }
            ? viewModel
            : null;

        UiLog.Debug(
            "Button clicked: {ButtonText}; Name={ButtonName}; Classes={ButtonClasses}; Command={Command}; DataContext={ButtonDataContext}; CurrentPage={CurrentPage}",
            GetButtonText(button),
            string.IsNullOrWhiteSpace(button.Name) ? null : button.Name,
            string.Join(",", button.Classes),
            button.Command?.GetType().Name,
            button.DataContext?.GetType().Name,
            mainViewModel?.CurrentPage);
    }

    private static string GetButtonText(Button button)
    {
        return button.Content switch
        {
            null => "(no content)",
            string text when !string.IsNullOrWhiteSpace(text) => text,
            TextBlock textBlock when !string.IsNullOrWhiteSpace(textBlock.Text) => textBlock.Text,
            Label label when label.Content is not null => label.Content.ToString() ?? "(label)",
            Panel panel => string.Join(" ", panel.Children.Select(GetControlText).Where(x => !string.IsNullOrWhiteSpace(x))),
            _ => button.Content.GetType().Name
        };
    }

    private static string? GetControlText(Control control)
    {
        return control switch
        {
            TextBlock textBlock => textBlock.Text,
            Label label => label.Content?.ToString(),
            ContentControl contentControl => contentControl.Content?.ToString(),
            _ => null
        };
    }
}
