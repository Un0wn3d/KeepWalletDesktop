using Avalonia.Controls;
using KeepWalletDesktop.ViewModels;
using System;

namespace KeepWalletDesktop.Views.Controls;

public partial class AllTransactionsDialogView : UserControl
{
    private static readonly TimeSpan ScrollThrottleInterval = TimeSpan.FromMilliseconds(300);
    private DateTimeOffset lastLoadMoreScrollAt = DateTimeOffset.MinValue;

    public AllTransactionsDialogView()
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
        var now = DateTimeOffset.UtcNow;
        if (remaining <= 48 && now - lastLoadMoreScrollAt >= ScrollThrottleInterval)
        {
            lastLoadMoreScrollAt = now;
            viewModel.LoadMoreFilteredTransactionsCommand.Execute(null);
        }
    }

    private void TransactionAdvancedFilter_OnChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.RefreshAllTransactionsAdvancedFilters();
        }
    }
}
