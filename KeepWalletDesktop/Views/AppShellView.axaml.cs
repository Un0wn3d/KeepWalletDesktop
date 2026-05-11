using Avalonia.Controls;
using Avalonia.Input;
using KeepWalletDesktop.ViewModels;

namespace KeepWalletDesktop.Views;

public partial class AppShellView : UserControl
{
    public AppShellView()
    {
        InitializeComponent();
    }

    private void CategoryLimit_OnLostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SaveCategoryLimit(sender);
    }

    private void CategoryLimit_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        SaveCategoryLimit(sender);
        e.Handled = true;
    }

    private void SaveCategoryLimit(object? sender)
    {
        if (sender is not TextBox { DataContext: CategoryDetailItemViewModel category } ||
            DataContext is not MainViewModel viewModel)
        {
            return;
        }

        viewModel.UpdateCategoryLimitCommand.Execute(category);
    }
}
