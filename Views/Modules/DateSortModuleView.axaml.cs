using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System.Linq;
using KeepWalletDesktop.ViewModels;

namespace KeepWalletDesktop.Views.Modules;

public partial class DateSortModuleView : UserControl
{
    private const int MaxVisualCalendarRangeDays = 62;

    private DateTimeOffset _from;
    private DateTimeOffset _to;
    private DateSortPeriod _period = DateSortPeriod.Month;
    private bool _isUpdatingDates;
    private DispatcherTimer? _calendarSelectionDebounceTimer;
    private MainViewModel? _viewModel;

    public DateSortModuleView()
    {
        InitializeComponent();
        ApplyPreset(DateSortPeriod.Month);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        base.OnDataContextChanged(e);
        _viewModel = DataContext as MainViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        UpdateControls();
        ApplyRangeToDataContext();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.Texts))
        {
            UpdateControls();
        }
    }

    private void PreviousButton_Click(object? sender, RoutedEventArgs e)
    {
        MovePeriod(-1);
    }

    private void NextButton_Click(object? sender, RoutedEventArgs e)
    {
        MovePeriod(1);
    }

    private void RangeCalendar_SelectedDatesChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingDates || RangeCalendar.SelectedDates.Count == 0)
        {
            return;
        }

        var selectedFrom = RangeCalendar.SelectedDates.Min();
        var selectedTo = RangeCalendar.SelectedDates.Max();

        _period = DateSortPeriod.Custom;
        _from = ToStartOfDay(selectedFrom);
        _to = ToEndOfDay(selectedTo);

        if (_from > _to)
        {
            (_from, _to) = (_to, _from);
        }

        _calendarSelectionDebounceTimer ??= new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(220)
        };
        _calendarSelectionDebounceTimer.Stop();
        _calendarSelectionDebounceTimer.Tick -= CalendarSelectionDebounceTimerOnTick;
        _calendarSelectionDebounceTimer.Tick += CalendarSelectionDebounceTimerOnTick;
        _calendarSelectionDebounceTimer.Start();
    }

    private void CalendarSelectionDebounceTimerOnTick(object? sender, EventArgs e)
    {
        if (_calendarSelectionDebounceTimer is null)
        {
            return;
        }

        _calendarSelectionDebounceTimer.Stop();
        _calendarSelectionDebounceTimer.Tick -= CalendarSelectionDebounceTimerOnTick;
        UpdateControls();
    }

    private void PeriodFlyout_Opened(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(RefreshCalendarVisualSelection, DispatcherPriority.Loaded);
    }

    private void LastHourButton_Click(object? sender, RoutedEventArgs e) => ApplyPreset(DateSortPeriod.Hour);

    private void LastTwoHoursButton_Click(object? sender, RoutedEventArgs e) => ApplyPreset(DateSortPeriod.TwoHours);

    private void LastThreeHoursButton_Click(object? sender, RoutedEventArgs e) => ApplyPreset(DateSortPeriod.ThreeHours);

    private void LastSixHoursButton_Click(object? sender, RoutedEventArgs e) => ApplyPreset(DateSortPeriod.SixHours);

    private void LastTwelveHoursButton_Click(object? sender, RoutedEventArgs e) => ApplyPreset(DateSortPeriod.TwelveHours);

    private void LastDayButton_Click(object? sender, RoutedEventArgs e) => ApplyPreset(DateSortPeriod.Day);

    private void LastWeekButton_Click(object? sender, RoutedEventArgs e) => ApplyPreset(DateSortPeriod.Week);

    private void LastMonthButton_Click(object? sender, RoutedEventArgs e) => ApplyPreset(DateSortPeriod.Month);

    private void LastHalfYearButton_Click(object? sender, RoutedEventArgs e) => ApplyPreset(DateSortPeriod.HalfYear);

    private void LastYearButton_Click(object? sender, RoutedEventArgs e) => ApplyPreset(DateSortPeriod.Year);

    private void ApplyPreset(DateSortPeriod period)
    {
        var now = DateTime.Now;
        _period = period;
        if (period == DateSortPeriod.Month)
        {
            SetMonthRange(now.Year, now.Month);
        }
        else
        {
            _to = now;
            _from = period switch
            {
                DateSortPeriod.Hour => now.AddHours(-1),
                DateSortPeriod.TwoHours => now.AddHours(-2),
                DateSortPeriod.ThreeHours => now.AddHours(-3),
                DateSortPeriod.SixHours => now.AddHours(-6),
                DateSortPeriod.TwelveHours => now.AddHours(-12),
                DateSortPeriod.Day => now.AddDays(-1),
                DateSortPeriod.Week => now.AddDays(-7),
                DateSortPeriod.HalfYear => now.AddMonths(-6),
                DateSortPeriod.Year => now.AddYears(-1),
                _ => now.AddMonths(-1)
            };
        }

        UpdateControls();
    }

    private void MovePeriod(int direction)
    {
        var span = _to - _from;
        var days = (_to.Date - _from.Date).Days + 1;

        switch (_period)
        {
            case DateSortPeriod.Hour:
            case DateSortPeriod.TwoHours:
            case DateSortPeriod.ThreeHours:
            case DateSortPeriod.SixHours:
            case DateSortPeriod.TwelveHours:
            case DateSortPeriod.Day:
            case DateSortPeriod.Week:
                _from = _from.AddTicks(span.Ticks * direction);
                _to = _to.AddTicks(span.Ticks * direction);
                break;
            case DateSortPeriod.Custom:
                _from = ToStartOfDay(_from.Date.AddDays(days * direction));
                _to = ToEndOfDay(_to.Date.AddDays(days * direction));
                break;
            case DateSortPeriod.Month:
                var nextMonth = _from.AddMonths(direction);
                SetMonthRange(nextMonth.Year, nextMonth.Month);
                break;
            case DateSortPeriod.HalfYear:
                _from = _from.AddMonths(6 * direction);
                _to = _to.AddMonths(6 * direction);
                break;
            case DateSortPeriod.Year:
                _from = _from.AddYears(direction);
                _to = _to.AddYears(direction);
                break;
        }

        UpdateControls();
    }

    private void UpdateControls()
    {
        RefreshCalendarVisualSelection();

        PeriodButton.Content = _period == DateSortPeriod.Custom
            ? $"{_from:dd.MM.yyyy} - {_to:dd.MM.yyyy}"
            : GetPeriodLabel(_period);
        ApplyRangeToDataContext();
    }

    private void RefreshCalendarVisualSelection()
    {
        _isUpdatingDates = true;
        try
        {
            RangeCalendar.DisplayDate = _from.Date;
            RangeCalendar.SelectedDates.Clear();

            var visualTo = (_to.Date - _from.Date).Days > MaxVisualCalendarRangeDays
                ? _from.Date.AddDays(MaxVisualCalendarRangeDays)
                : _to.Date;
            RangeCalendar.SelectedDates.AddRange(_from.Date, visualTo);
        }
        finally
        {
            _isUpdatingDates = false;
        }

        RangeCalendar.InvalidateVisual();
    }

    private void SetMonthRange(int year, int month)
    {
        _from = new DateTimeOffset(year, month, 1, 0, 0, 0, DateTimeOffset.Now.Offset);
        var lastDay = DateTime.DaysInMonth(year, month);
        _to = new DateTimeOffset(year, month, lastDay, 23, 59, 59, DateTimeOffset.Now.Offset);
    }

    private void ApplyRangeToDataContext()
    {
        if (DataContext is MainViewModel viewModel)
        {
            if (viewModel.IsScheduledPaymentsPage)
            {
                return;
            }

            if (viewModel.IsAllTransactionsPage)
            {
                viewModel.ApplyAllTransactionsDateRange(_from, _to);
            }
            else if (viewModel.IsLogsPage)
            {
                viewModel.ApplyLogsDateRange(_from, _to);
            }
            else
            {
                viewModel.ApplyHomeDateRange(_from, _to);
            }
        }
    }

    private static DateTimeOffset ToStartOfDay(DateTime date) => new(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeOffset.Now.Offset);

    private static DateTimeOffset ToStartOfDay(DateTimeOffset date) => new(date.Year, date.Month, date.Day, 0, 0, 0, date.Offset);

    private static DateTimeOffset ToEndOfDay(DateTimeOffset date) => new(date.Year, date.Month, date.Day, 23, 59, 59, date.Offset);

    private string GetPeriodLabel(DateSortPeriod period)
    {
        if (period == DateSortPeriod.Month)
        {
            var current = DateTime.Today;
            var monthDiff = (_from.Year - current.Year) * 12 + (_from.Month - current.Month);
            return monthDiff switch
            {
                -1 => GetTexts().DateRangePreviousMonth,
                0 => GetTexts().DateRangeThisMonth,
                1 => GetTexts().DateRangeNextMonth,
                _ => $"{_from:MM.yyyy}"
            };
        }

        return period switch
        {
            DateSortPeriod.Hour => GetTexts().DateRangeLastHour,
            DateSortPeriod.TwoHours => GetTexts().DateRangeLastTwoHours,
            DateSortPeriod.ThreeHours => GetTexts().DateRangeLastThreeHours,
            DateSortPeriod.SixHours => GetTexts().DateRangeLastSixHours,
            DateSortPeriod.TwelveHours => GetTexts().DateRangeLastTwelveHours,
            DateSortPeriod.Day => GetTexts().DateRangeLastDay,
            DateSortPeriod.Week => GetTexts().DateRangeLastWeek,
            DateSortPeriod.HalfYear => GetTexts().DateRangeLastHalfYear,
            DateSortPeriod.Year => GetTexts().DateRangeLastYear,
            _ => GetTexts().DateRangeCustom
        };
    }

    private ILocalizedText GetTexts() => _viewModel?.Texts ?? LocalizedText.Load("ua");

    private enum DateSortPeriod
    {
        Custom,
        Hour,
        TwoHours,
        ThreeHours,
        SixHours,
        TwelveHours,
        Day,
        Week,
        Month,
        HalfYear,
        Year
    }
}
