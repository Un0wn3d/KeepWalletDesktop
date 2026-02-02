using CommunityToolkit.Mvvm.ComponentModel;
using Diplom.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using Serilog;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;

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
    
    [ObservableProperty]
    private ObservableCollection<Transaction> latestExpenses;
    public MainViewModel()
    {
        latestExpenses = new ObservableCollection<Transaction>();
        Transactions();
    }

    private void Transactions()
    {
        using var db = new AppDBContext();
        var transactions = db.Transaction.ToList();
        
        
        LatestExpenses = new ObservableCollection<Transaction>(transactions);
    }

}