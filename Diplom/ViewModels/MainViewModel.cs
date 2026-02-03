using CommunityToolkit.Mvvm.ComponentModel;
using Diplom.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using Serilog;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore.SkiaSharpView.Avalonia;

namespace Diplom.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    static AppDBContext db = new AppDBContext();
    
    public ISeries[] Series { get; set; }
        = new ISeries[]
        {
            new PieSeries<double> { Values = new double[] { 2 } },
            new PieSeries<double> { Values = new double[] { 4 } }, 
            new PieSeries<double> { Values = new double[] { 1 } },
            new PieSeries<double> { Values = new double[] { 4 } },
            new PieSeries<double> { Values = new double[] { 3 } }
        };

    public ISeries[] Chart { get; set; }
        = new ISeries[]
    {
        new ColumnSeries<Double>
        {
            Values = db.Transaction
                .Where(m => m.Type == "Income")
                .Select(m => m.Cost)
                .ToList(),
            Name = "Income", 
            Stroke = null
        },
        new ColumnSeries<double>
        {
            Values = db.Transaction
                .Where(m => m.Type == "Expense")
                .Select(m => m.Cost)
                .ToList(),
            Name = "Outcome", 
            Stroke = null
        }
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
        var transactions = db.Transaction.ToList();
        LatestExpenses = new ObservableCollection<Transaction>(transactions);
    }

    // Todo ReWrite
    public string Money 
    {
        get
        {
            // Считаем сумму всех доходов
            var income = db.Transaction
                .Where(t => t.Type == "Income")
                .Sum(t => (double?)t.Cost) ?? 0;

            // Считаем сумму всех расходов
            var expense = db.Transaction
                .Where(t => t.Type == "Expense") // Убедитесь, что название категории совпадает с БД
                .Sum(t => (double?)t.Cost) ?? 0;

            // Возвращаем разницу, отформатированную как строку
            return (income - expense).ToString("N2") + " ₴";
        }
    }

    
    public string Income => db.Transaction
        .Where(m => m.Type == "Income")
        .Sum(m => m.Cost)
        .ToString("C");

    public string Expense => db.Transaction
        .Where(e => e.Type == "Expense")
        .Sum(t => t.Cost)
        .ToString("C");
}