using Avalonia.Controls;
using Avalonia.Interactivity;
using KeepWalletDesktop.ViewModels;

namespace KeepWalletDesktop.Views;

public partial class ScheduledPaymentsView : UserControl
{
    public ScheduledPaymentsView()
    {
        InitializeComponent();
    }

    private void ScheduledPaymentAdvancedFilter_OnChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { DataContext: FilterSelectionOptionViewModel option } checkBox)
        {
            option.IsSelected = checkBox.IsChecked == true;
        }

        if (DataContext is MainViewModel viewModel &&
            viewModel.RefreshScheduledPaymentAdvancedFiltersCommand.CanExecute(null))
        {
            viewModel.RefreshScheduledPaymentAdvancedFiltersCommand.Execute(null);
        }
    }
}
