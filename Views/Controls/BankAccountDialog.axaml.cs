using Avalonia.Controls;
using Avalonia.Input;
using KeepWalletDesktop.ViewModels;

namespace KeepWalletDesktop.Views.Controls;

public partial class BankAccountDialog : UserControl
{
    public BankAccountDialog()
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
            viewModel.CloseBankAccountDialogCommand.CanExecute(null))
        {
            viewModel.CloseBankAccountDialogCommand.Execute(null);
            e.Handled = true;
        }
    }
}
