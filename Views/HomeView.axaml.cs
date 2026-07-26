using Avalonia.Controls;
using KeepWalletDesktop.ViewModels;
using System;
using System.ComponentModel;

namespace KeepWalletDesktop.Views;

public partial class HomeView : UserControl
{
    private static readonly TimeSpan ScrollThrottleInterval = TimeSpan.FromMilliseconds(300);
    private DateTimeOffset lastLoadMoreScrollAt = DateTimeOffset.MinValue;

    public HomeView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => AttachViewModel();
        AttachViewModel();
    }

    private MainViewModel? attachedViewModel;

    private void AttachViewModel()
    {
        if (attachedViewModel is not null)
        {
            attachedViewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
        }

        attachedViewModel = DataContext as MainViewModel;
        if (attachedViewModel is not null)
        {
            attachedViewModel.PropertyChanged += ViewModel_OnPropertyChanged;
        }

        ApplyChartTooltips();
    }

    private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.ChartTooltipBackgroundPaint) or nameof(MainViewModel.ChartTooltipTextPaint))
        {
            ApplyChartTooltips();
        }
    }

    private void ApplyChartTooltips()
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        ExpensePieChart.TooltipBackgroundPaint = viewModel.ChartTooltipBackgroundPaint;
        ExpensePieChart.TooltipTextPaint = viewModel.ChartTooltipTextPaint;
        TimelineChart.TooltipBackgroundPaint = viewModel.ChartTooltipBackgroundPaint;
        TimelineChart.TooltipTextPaint = viewModel.ChartTooltipTextPaint;
    }

    private void HomeScrollViewer_OnScrollChanged(object? sender, ScrollChangedEventArgs e)
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
            viewModel.LoadMoreHomeTransactionsCommand.Execute(null);
        }
    }
}
