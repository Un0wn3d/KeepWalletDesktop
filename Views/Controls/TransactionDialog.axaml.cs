using Avalonia.Controls;
using Avalonia.Input;
using KeepWalletDesktop.ViewModels;

namespace KeepWalletDesktop.Views.Controls;

public partial class TransactionDialog : UserControl
{
    public TransactionDialog()
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
}
