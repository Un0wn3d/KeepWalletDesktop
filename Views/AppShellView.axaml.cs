using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using KeepWalletDesktop.ViewModels;

namespace KeepWalletDesktop.Views;

public partial class AppShellView : UserControl
{
    public AppShellView()
    {
        InitializeComponent();
    }

    private void AppShell_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape ||
            DataContext is not MainViewModel { IsCategoryDetailsOpen: true } viewModel)
        {
            return;
        }

        viewModel.CloseCategoryDetailsCommand.Execute(null);
        e.Handled = true;
    }

    private void CategoryDetailsOverlay_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!ReferenceEquals(e.Source, sender))
        {
            return;
        }

        if (DataContext is not MainViewModel { IsCategoryDetailsOpen: true } viewModel)
        {
            return;
        }

        viewModel.CloseCategoryDetailsCommand.Execute(null);
        e.Handled = true;
    }

    private void CategorySettings_OnChanged(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel &&
            viewModel.SaveCategoriesCommand.CanExecute(null))
        {
            viewModel.SaveCategoriesCommand.Execute(null);
        }
    }
}
