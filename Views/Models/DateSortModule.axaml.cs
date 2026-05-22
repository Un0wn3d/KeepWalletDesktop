using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using KeepWalletDesktop.ViewModels;

namespace KeepWalletDesktop.Views.Mdules;

public partial class DateSortModule : UserControl
{
    private DateTimeOffset _from;
    private DateTimeOffset _to;
    private DateSortPeriod _period = DateSortPeriod.Month;
    private bool _isUpdatingDates;

    public DateSortModule()
    {
        InitializeComponent();
        ApplyPreset(DateSortPeriod.Month);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        ApplyRangeToDataContext();
    }

    private void PreviousButton_Click(object? sender, RoutedEventArgs e)
    {
        MovePeriod(-1);
    }

    private void NextButton_Click(object? sender, RoutedEventArgs e)
    {
        MovePeriod(1);
    }

    private void DatePicker_SelectedDateChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingDates || FromDatePicker.SelectedDate is null || ToDatePicker.SelectedDate is null)
        {
            return;
        }

        _period = DateSortPeriod.Custom;
        _from = ToStartOfDay(FromDatePicker.SelectedDate.Value);
        _to = ToEndOfDay(ToDatePicker.SelectedDate.Value);

        if (_from > _to)
        {
            (_from, _to) = (_to, _from);
        }

        UpdateControls();
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
            DateSortPeriod.Month => now.AddMonths(-1),
            DateSortPeriod.HalfYear => now.AddMonths(-6),
            DateSortPeriod.Year => now.AddYears(-1),
            _ => now.AddMonths(-1)
        };

        UpdateControls();
    }

    private void MovePeriod(int direction)
    {
        var span = _to - _from;

        switch (_period)
        {
            case DateSortPeriod.Hour:
            case DateSortPeriod.TwoHours:
            case DateSortPeriod.ThreeHours:
            case DateSortPeriod.SixHours:
            case DateSortPeriod.TwelveHours:
            case DateSortPeriod.Day:
            case DateSortPeriod.Week:
            case DateSortPeriod.Custom:
                _from = _from.AddTicks(span.Ticks * direction);
                _to = _to.AddTicks(span.Ticks * direction);
                break;
            case DateSortPeriod.Month:
                _from = _from.AddMonths(direction);
                _to = _to.AddMonths(direction);
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
        _isUpdatingDates = true;
        FromDatePicker.SelectedDate = _from.Date;
        ToDatePicker.SelectedDate = _to.Date;
        _isUpdatingDates = false;

        PeriodButton.Content = _period == DateSortPeriod.Custom
            ? $"{_from:dd.MM.yyyy} - {_to:dd.MM.yyyy}"
            : GetPeriodLabel(_period);
        ApplyRangeToDataContext();
    }

    private void ApplyRangeToDataContext()
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.ApplyHomeDateRange(_from, _to);
        }
    }

    private static DateTimeOffset ToStartOfDay(DateTimeOffset date) => new(date.Year, date.Month, date.Day, 0, 0, 0, date.Offset);

    private static DateTimeOffset ToEndOfDay(DateTimeOffset date) => new(date.Year, date.Month, date.Day, 23, 59, 59, date.Offset);

    private static string GetPeriodLabel(DateSortPeriod period)
    {
        return period switch
        {
            DateSortPeriod.Hour => "Остання година",
            DateSortPeriod.TwoHours => "Останні 2 години",
            DateSortPeriod.ThreeHours => "Останні 3 години",
            DateSortPeriod.SixHours => "Останні 6 годин",
            DateSortPeriod.TwelveHours => "Останні 12 годин",
            DateSortPeriod.Day => "Останній день",
            DateSortPeriod.Week => "Останній тиждень",
            DateSortPeriod.Month => "Останній місяць",
            DateSortPeriod.HalfYear => "Останні пів року",
            DateSortPeriod.Year => "Останній рік",
            _ => "Діапазон"
        };
    }

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
