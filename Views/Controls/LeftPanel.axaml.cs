using Avalonia.Controls;
using Avalonia.Interactivity;
using KeepWalletDesktop.ViewModels;

namespace KeepWalletDesktop.Views.Controls;

public partial class LeftPanel : UserControl
{
    public LeftPanel()
    {
        InitializeComponent();
    }
    
    //[RelayCommand]
    private void btn_Home(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.OpenHomeCommand.Execute(null);
        }
    }

    private void btn_Savings(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.OpenSavingsCommand.Execute(null);
        }
    }

    private void btn_BankAccounts(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.OpenBankAccountsCommand.Execute(null);
        }
    }

    private void btn_Groups(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.OpenGroupsCommand.Execute(null);
        }
    }

    private void btn_ScheduledPayments(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.OpenScheduledPaymentsCommand.Execute(null);
        }
    }

    private void btn_Categories(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.OpenCategoriesCommand.Execute(null);
        }
    }

    private void btn_Reporting(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.OpenReportingCommand.Execute(null);
        }
    }

    private void btn_AllTransactions(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.OpenAllTransactionsTabCommand.Execute(null);
        }
    }

    private void btn_Settings(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.OpenSettingsCommand.Execute(null);
        }
    }
}
