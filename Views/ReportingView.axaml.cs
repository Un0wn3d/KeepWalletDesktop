using Avalonia.Controls;
using KeepWalletDesktop.ViewModels;
using System.ComponentModel;

namespace KeepWalletDesktop.Views;

public partial class ReportingView : UserControl
{
    public ReportingView()
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

        ReportExpensePieChart.TooltipBackgroundPaint = viewModel.ChartTooltipBackgroundPaint;
        ReportExpensePieChart.TooltipTextPaint = viewModel.ChartTooltipTextPaint;
        ReportTimelineChart.TooltipBackgroundPaint = viewModel.ChartTooltipBackgroundPaint;
        ReportTimelineChart.TooltipTextPaint = viewModel.ChartTooltipTextPaint;
    }
}
