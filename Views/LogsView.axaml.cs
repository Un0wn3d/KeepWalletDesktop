using Avalonia.Controls;
using Avalonia.Threading;
using KeepWalletDesktop.ViewModels;
using System;

namespace KeepWalletDesktop.Views;

public partial class LogsView : UserControl
{
    private readonly DispatcherTimer scrollDebounceTimer;

    public LogsView()
    {
        InitializeComponent();
        scrollDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(180)
        };
        scrollDebounceTimer.Tick += ScrollDebounceTimer_OnTick;
    }

    private void LogsScrollViewer_OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer)
        {
            return;
        }

        var distanceToBottom = scrollViewer.Extent.Height - scrollViewer.Viewport.Height - scrollViewer.Offset.Y;
        if (distanceToBottom > 160)
        {
            return;
        }

        scrollDebounceTimer.Stop();
        scrollDebounceTimer.Start();
    }

    private void ScrollDebounceTimer_OnTick(object? sender, EventArgs e)
    {
        scrollDebounceTimer.Stop();
        if (DataContext is MainViewModel viewModel && viewModel.LoadMoreLogsCommand.CanExecute(null))
        {
            viewModel.LoadMoreLogsCommand.Execute(null);
        }
    }
}
