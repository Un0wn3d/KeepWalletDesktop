using Avalonia.Controls;
using Avalonia.Interactivity;
using KeepWalletDesktop.ViewModels;

namespace KeepWalletDesktop.Views.Controls;

public partial class FloatingActionMenu : UserControl
{
    public FloatingActionMenu()
    {
        InitializeComponent();
    }

    private void ToggleFab_Click(object? sender, RoutedEventArgs e)
    {
        ActionList.IsVisible = !ActionList.IsVisible;
    }

    private void CreateTransaction_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.CreateTransactionCommand.Execute(null);
        }

        ActionList.IsVisible = false;
    }

    private void CreatePiggyBank_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.CreateNewPiggyBankCommand.Execute(null);
        }

        ActionList.IsVisible = false;
    }
}
