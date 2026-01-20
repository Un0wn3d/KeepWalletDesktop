using CommunityToolkit.Mvvm.ComponentModel;
using Diplom.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using Serilog;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace Diplom.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public ISeries[] Series { get; set; }
        = new ISeries[]
        {
            new PieSeries<double> { Values = new double[] { 2 } },
            new PieSeries<double> { Values = new double[] { 4 } }, 
            new PieSeries<double> { Values = new double[] { 1 } },
            new PieSeries<double> { Values = new double[] { 4 } },
            new PieSeries<double> { Values = new double[] { 3 } }
        };

    private readonly DatabaseService _databaseService;

    [ObservableProperty]
    private ObservableCollection<DatabaseService.Transaction> _latestExpenses = new();

    public MainViewModel()
    {
        _databaseService = new DatabaseService();
        LoadLatestExpenses();
    }

    private async void LoadLatestExpenses()
    {
        try
        {
            var expenses = await _databaseService.GetLatestExpensesAsync(5);
            Log.Information("Loaded {Count} expenses", expenses.Count);
            LatestExpenses = new ObservableCollection<DatabaseService.Transaction>(expenses);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load expenses");
        }
    }
}