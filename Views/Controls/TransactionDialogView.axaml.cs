using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using KeepWalletDesktop.ViewModels;

namespace KeepWalletDesktop.Views.Controls;

public partial class TransactionDialogView : UserControl
{
    public TransactionDialogView()
    {
        InitializeComponent();
    }

    private void DialogOverlay_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!ReferenceEquals(e.Source, sender))
        {
            return;
        }

        if (DataContext is MainViewModel viewModel &&
            viewModel.CloseTransactionDialogCommand.CanExecute(null))
        {
            viewModel.CloseTransactionDialogCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void TransactionCategoryComboBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel &&
            viewModel.SelectedTransactionCategory?.Code == "__new_category" &&
            viewModel.CreateTransactionCategoryFromDialogCommand.CanExecute(null))
        {
            viewModel.CreateTransactionCategoryFromDialogCommand.Execute(null);
        }
    }
}
