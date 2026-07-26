using CommunityToolkit.Mvvm.Input;
using Avalonia.Media;
using Avalonia.Threading;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using KeepWalletDesktop.Services;

namespace KeepWalletDesktop.ViewModels;

public partial class MainViewModel
{
    private const int HomeTransactionsPageSize = 10;

    private static readonly string[] DarkExpenseChartPalette =
    [
        "#374151",
        "#0078D4",
        "#10B981",
        "#6366F1",
        "#14B8A6",
        "#F59E0B",
        "#8B5CF6",
        "#64748B"
    ];

    private static readonly string[] LightExpenseChartPalette =
    [
        "#EF4444",
        "#F97316",
        "#EAB308",
        "#22C55E",
        "#14B8A6",
        "#3B82F6",
        "#8B5CF6",
        "#EC4899"
    ];

    private IReadOnlyList<TransactionItemViewModel> homeTransactionSource = [];
    private int homeTransactionTakeCount = HomeTransactionsPageSize;
    private DateTimeOffset homeDateRangeFrom = DateTimeOffset.Now.AddMonths(-1);
    private DateTimeOffset homeDateRangeTo = DateTimeOffset.Now;
    private DispatcherTimer? homeRangeDebounceTimer;
    private DateTimeOffset? pendingHomeRangeFrom;
    private DateTimeOffset? pendingHomeRangeTo;
    private bool isApplyingHomeRange;

    public string Income => FormatTransactionTotals(LatestExpenses.Where(t => t.IsIncome));
    public string Expense => FormatTransactionTotals(LatestExpenses.Where(t => !t.IsIncome));
    public string Money => FormatTransactionBalance();
    public bool HasTransactions => LatestExpenses.Count > 0;
    public bool HasNoTransactions => !HasTransactions;
    public bool HasAllTransactions => LatestExpenses.Count > 0;
    public bool HasExpenseTransactions => LatestExpenses.Any(t => !t.IsIncome);
    public bool HasNoExpenseTransactions => !HasExpenseTransactions;
    public double HomeChartPanelHeight => Math.Clamp(260 + Math.Max(0, ExpenseCategoryLegend.Count - 8) * 28, 260, 430);
    public string TransactionTimelineTitle => Texts.TransactionTimelineTitle;

    public ISeries[] Series { get; private set; } = [];
    public ISeries[] Chart { get; private set; } = [];
    public Axis[] TransactionTimelineXAxes { get; private set; } = [];
    public Axis[] TransactionTimelineYAxes { get; private set; } = [];
    public ObservableCollection<CategoryLegendItemViewModel> ExpenseCategoryLegend { get; private set; } = [];

    public void ApplyHomeDateRange(DateTimeOffset from, DateTimeOffset to)
    {
        if (from > to)
        {
            (from, to) = (to, from);
        }

        var normalizedFrom = from;
        var normalizedTo = to;
        if (normalizedFrom.Offset != homeDateRangeFrom.Offset)
        {
            normalizedFrom = normalizedFrom.ToOffset(homeDateRangeFrom.Offset);
        }

        if (normalizedTo.Offset != homeDateRangeTo.Offset)
        {
            normalizedTo = normalizedTo.ToOffset(homeDateRangeTo.Offset);
        }

        if (normalizedFrom.UtcDateTime == homeDateRangeFrom.UtcDateTime &&
            normalizedTo.UtcDateTime == homeDateRangeTo.UtcDateTime)
        {
            return;
        }

        pendingHomeRangeFrom = from;
        pendingHomeRangeTo = to;

        homeRangeDebounceTimer ??= new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(350)
        };
        homeRangeDebounceTimer.Stop();
        homeRangeDebounceTimer.Tick -= HomeRangeDebounceTimerOnTick;
        homeRangeDebounceTimer.Tick += HomeRangeDebounceTimerOnTick;
        homeRangeDebounceTimer.Start();
    }

    private void HomeRangeDebounceTimerOnTick(object? sender, EventArgs e)
    {
        if (homeRangeDebounceTimer is null || isApplyingHomeRange)
        {
            return;
        }

        homeRangeDebounceTimer.Stop();
        homeRangeDebounceTimer.Tick -= HomeRangeDebounceTimerOnTick;

        if (!pendingHomeRangeFrom.HasValue || !pendingHomeRangeTo.HasValue)
        {
            return;
        }

        isApplyingHomeRange = true;
        try
        {
            var from = pendingHomeRangeFrom.Value;
            var to = pendingHomeRangeTo.Value;
            pendingHomeRangeFrom = null;
            pendingHomeRangeTo = null;

            homeDateRangeFrom = from;
            homeDateRangeTo = to;
            ViewModelLog.Debug(
                "Home date range changed: From={From:o}, To={To:o}",
                homeDateRangeFrom,
                homeDateRangeTo);
            RefreshHomeTransactionsFromDateRange();
        }
        finally
        {
            isApplyingHomeRange = false;
        }
    }

    [RelayCommand]
    private async Task OpenHomeAsync()
    {
        if (!EnsureAuthorizedNavigation())
        {
            return;
        }

        SelectedBankAccount = null;
        CurrentPage = HomePage;
        await LoadTransactionsAsync();
    }

    [RelayCommand]
    public void LoadMoreHomeTransactions()
    {
        if (LatestExpenses.Count <= VisibleTransactions.Count)
        {
            ViewModelLog.Debug(
                "Home load more skipped: all transactions are visible. VisibleCount={VisibleCount}, SourceCount={SourceCount}",
                VisibleTransactions.Count,
                LatestExpenses.Count);
            return;
        }

        var previousTakeCount = homeTransactionTakeCount;
        homeTransactionTakeCount += HomeTransactionsPageSize;
        ViewModelLog.Information(
            "Home load more requested: PreviousTakeCount={PreviousTakeCount}, NewTakeCount={NewTakeCount}, SourceCount={SourceCount}",
            previousTakeCount,
            homeTransactionTakeCount,
            LatestExpenses.Count);
        UpdateVisibleTransactions();
    }

    [RelayCommand]
    private async Task OpenCategoryDetailsAsync()
    {
        if (!await EnsureBankAccountExistsAsync(Texts.MissingBankAccountCategoryExpensesPrompt))
        {
            return;
        }

        IsCategorySettingsMode = false;
        await LoadTransactionsAsync();
        await LoadBudgetsAsync();
        UpdateCategoryDetails();
        IsCategoryDetailsOpen = true;
    }

    private void UpdateCharts()
    {
        var incomeTotal = LatestExpenses.Where(t => t.IsIncome).Sum(t => (double)Math.Abs(t.Amount));
        var expenseTotal = LatestExpenses.Where(t => !t.IsIncome).Sum(t => (double)Math.Abs(t.Amount));
        var chartCurrency = SelectedBankAccount?.Currency
            ?? LatestExpenses.Select(t => t.Currency).FirstOrDefault(c => !string.IsNullOrWhiteSpace(c))
            ?? "UAH";
        var expenseGroups = LatestExpenses
            .Where(t => !t.IsIncome)
            .GroupBy(t => t.CategoryCode)
            .OrderByDescending(g => g.Sum(t => t.Amount))
            .ThenBy(g => GetCategoryDisplayName(g.Key))
            .ToArray();

        if (expenseGroups.Length > 0 || !IsApiAuthorized)
        {
            Series =
                expenseGroups
                    .Select((g, index) =>
                    {
                        var color = GetExpenseCategoryChartColor(g, g.Key, index);
                        var amount = Math.Abs(g.Sum(t => t.Amount));
                        return new PieSeries<double>
                        {
                            Values = [(double)amount],
                            Name = GetCategoryDisplayName(g.Key),
                            Fill = new SolidColorPaint(SKColor.Parse(color)),
                            Stroke = null
                        };
                    })
                    .Cast<ISeries>()
                    .ToArray();

            ExpenseCategoryLegend = new ObservableCollection<CategoryLegendItemViewModel>(
                expenseGroups.Select((g, index) =>
                {
                    var color = GetExpenseCategoryChartColor(g, g.Key, index);
                    var amount = Math.Abs(g.Sum(t => t.Amount));
                    return new CategoryLegendItemViewModel(
                        GetCategoryDisplayName(g.Key),
                        amount,
                        new SolidColorBrush(Color.Parse(color)),
                        $"{amount:N2} {chartCurrency}");
                }));
        }

        UpdateCategoryDetails();

        var startDate = homeDateRangeFrom.ToLocalTime().Date;
        var endDate = homeDateRangeTo.ToLocalTime().Date;
        if (endDate < startDate)
        {
            endDate = startDate;
        }

        var dayCount = (endDate - startDate).Days + 1;
        var grouped = LatestExpenses
            .GroupBy(t => t.TransactionDate.ToLocalTime().Date)
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    Income = g.Where(t => t.IsIncome).Sum(t => (double)t.Amount),
                    Expense = g.Where(t => !t.IsIncome).Sum(t => (double)Math.Abs(t.Amount))
                });

        var labels = new List<string>(dayCount);
        var incomeValues = new List<double>(dayCount);
        var expenseValues = new List<double>(dayCount);

        for (var day = 0; day < dayCount; day++)
        {
            var date = startDate.AddDays(day);
            labels.Add(date.ToString("dd.MM", CultureInfo.InvariantCulture));

            if (grouped.TryGetValue(date, out var totals))
            {
                incomeValues.Add(Math.Max(0, totals.Income));
                expenseValues.Add(Math.Max(0, totals.Expense));
            }
            else
            {
                incomeValues.Add(0);
                expenseValues.Add(0);
            }
        }

        var timelineMaxValue = incomeValues.Concat(expenseValues).DefaultIfEmpty(0).Max();
        var timelineYMax = timelineMaxValue <= 0
            ? 1
            : Math.Max(1, timelineMaxValue * 1.15);

        Chart =
        [
            new LineSeries<double>
            {
                Values = incomeValues,
                Name = Texts.TransactionIncomeType,
                GeometrySize = 8,
                LineSmoothness = 0.65,
                Stroke = new SolidColorPaint(new SKColor(16, 185, 129)) { StrokeThickness = 3 },
                Fill = new SolidColorPaint(new SKColor(16, 185, 129, 36))
            },
            new LineSeries<double>
            {
                Values = expenseValues,
                Name = Texts.TransactionExpenseType,
                GeometrySize = 8,
                LineSmoothness = 0.65,
                Stroke = new SolidColorPaint(new SKColor(55, 65, 81)) { StrokeThickness = 3 },
                Fill = new SolidColorPaint(new SKColor(55, 65, 81, 28))
            }
        ];

        TransactionTimelineXAxes =
        [
            new Axis
            {
                Labels = labels,
                MinLimit = 0,
                MaxLimit = Math.Max(0, labels.Count - 1)
            }
        ];

        TransactionTimelineYAxes =
        [
            new Axis
            {
                MinLimit = 0,
                MaxLimit = timelineYMax,
                MinStep = timelineYMax <= 1 ? 1 : Math.Max(1, timelineYMax / 4)
            }
        ];

        OnPropertyChanged(nameof(Series));
        OnPropertyChanged(nameof(Chart));
        OnPropertyChanged(nameof(TransactionTimelineXAxes));
        OnPropertyChanged(nameof(TransactionTimelineYAxes));
        OnPropertyChanged(nameof(ExpenseCategoryLegend));
        OnPropertyChanged(nameof(HomeChartPanelHeight));
        OnPropertyChanged(nameof(CategoryDetails));
        OnPropertyChanged(nameof(HasTransactions));
        OnPropertyChanged(nameof(HasNoTransactions));
        OnPropertyChanged(nameof(HasAllTransactions));
        OnPropertyChanged(nameof(HasExpenseTransactions));
        OnPropertyChanged(nameof(HasNoExpenseTransactions));
    }

    private string[] GetExpenseChartPalette() =>
        string.Equals(SelectedTheme?.Code, ThemeManager.Light, StringComparison.OrdinalIgnoreCase)
            ? LightExpenseChartPalette
            : DarkExpenseChartPalette;

    private SKColor GetChartTooltipBackgroundColor() =>
        string.Equals(SelectedTheme?.Code, ThemeManager.Light, StringComparison.OrdinalIgnoreCase)
            ? SKColor.Parse("#EDF5FF")
            : SKColor.Parse("#162338");

    private SKColor GetChartTooltipTextColor() =>
        string.Equals(SelectedTheme?.Code, ThemeManager.Light, StringComparison.OrdinalIgnoreCase)
            ? SKColor.Parse("#071426")
            : SKColor.Parse("#F8FBFF");

    private SKColor GetChartTooltipBorderColor() =>
        string.Equals(SelectedTheme?.Code, ThemeManager.Light, StringComparison.OrdinalIgnoreCase)
            ? SKColor.Parse("#C7D8EE")
            : SKColor.Parse("#2B4268");

    private string GetExpenseCategoryChartColor(IEnumerable<TransactionItemViewModel> transactions, string? categoryCode, int index)
    {
        var palette = GetExpenseChartPalette();
        var fallback = palette[index % palette.Length];

        var transactionColor = transactions
            .Select(t => t.CategoryColor)
            .FirstOrDefault(color => TryNormalizeChartColor(color, out _));
        if (TryNormalizeChartColor(transactionColor, out var normalizedTransactionColor))
        {
            return normalizedTransactionColor;
        }

        if (int.TryParse(categoryCode, NumberStyles.Integer, CultureInfo.InvariantCulture, out var categoryId))
        {
            var apiCategory = apiCategories.FirstOrDefault(c => c.Id == categoryId);
            if (TryNormalizeChartColor(apiCategory?.Color, out var categoryColor))
            {
                return categoryColor;
            }
        }

        return fallback;
    }

    private static bool TryNormalizeChartColor(string? rawColor, out string color)
    {
        color = string.Empty;
        if (string.IsNullOrWhiteSpace(rawColor))
        {
            return false;
        }

        var normalized = rawColor.Trim();
        if (!SKColor.TryParse(normalized, out _) || !Color.TryParse(normalized, out _))
        {
            return false;
        }

        color = normalized;
        return true;
    }

    private void UpdateVisibleTransactions()
    {
        var visible = LatestExpenses.Take(homeTransactionTakeCount).ToArray();
        VisibleTransactions = new ObservableCollection<TransactionItemViewModel>(visible);
        ViewModelLog.Verbose(
            "Home visible transactions refreshed: VisibleCount={VisibleCount}, SourceCount={SourceCount}, TakeCount={TakeCount}, From={From:o}, To={To:o}",
            visible.Length,
            LatestExpenses.Count,
            homeTransactionTakeCount,
            homeDateRangeFrom,
            homeDateRangeTo);
    }

    private void RefreshHomeTransactionsFromDateRange()
    {
        homeTransactionTakeCount = HomeTransactionsPageSize;
        LatestExpenses = new ObservableCollection<TransactionItemViewModel>(
            homeTransactionSource
                .Where(t => t.TransactionDate >= homeDateRangeFrom && t.TransactionDate <= homeDateRangeTo)
                .OrderByDescending(t => t.TransactionDate));

        ViewModelLog.Debug(
            "Home transactions filtered by period: FilteredCount={FilteredCount}, SourceCount={SourceCount}, InitialTakeCount={TakeCount}, From={From:o}, To={To:o}",
            LatestExpenses.Count,
            homeTransactionSource.Count,
            homeTransactionTakeCount,
            homeDateRangeFrom,
            homeDateRangeTo);

        AvailableBalance = LatestExpenses.Where(t => t.IsIncome).Sum(t => (double)t.Amount) -
                           LatestExpenses.Where(t => !t.IsIncome).Sum(t => (double)t.Amount);

        Dispatcher.UIThread.Post(() =>
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            ApplyTransactionFilters();
            ViewModelLog.Debug("ApplyTransactionFilters: {Ms}ms", sw.ElapsedMilliseconds);

            UpdateVisibleTransactions();
            ViewModelLog.Debug("UpdateVisibleTransactions: {Ms}ms", sw.ElapsedMilliseconds);

            UpdateCharts();
            ViewModelLog.Debug("UpdateCharts: {Ms}ms", sw.ElapsedMilliseconds);

            OnPropertyChanged(nameof(Money));
            OnPropertyChanged(nameof(Income));
            OnPropertyChanged(nameof(Expense));
            OnPropertyChanged(nameof(HasTransactions));
            OnPropertyChanged(nameof(HasNoTransactions));
            OnPropertyChanged(nameof(HasAllTransactions));
            OnPropertyChanged(nameof(HasExpenseTransactions));
            OnPropertyChanged(nameof(HasNoExpenseTransactions));

            ViewModelLog.Debug("RefreshHomeTransactions background post done: {Ms}ms total", sw.ElapsedMilliseconds);
        }, DispatcherPriority.Background);
    }
}
