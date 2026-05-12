using Avalonia.Controls;
using KeepWalletDesktop.ViewModels;

namespace KeepWalletDesktop.Views.Controls;

public partial class AllTransactionsDialog : UserControl
{
    public AllTransactionsDialog()
    {
        InitializeComponent();
    }

    private void TransactionsScrollViewer_OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer ||
            DataContext is not MainViewModel viewModel)
        {
            return;
        }

        var remaining = scrollViewer.Extent.Height - scrollViewer.Viewport.Height - scrollViewer.Offset.Y;
        if (remaining <= 48)
        {
            viewModel.LoadMoreFilteredTransactionsCommand.Execute(null);
        }
    }
}
