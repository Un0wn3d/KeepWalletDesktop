using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Media;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System;
using System.Globalization;

namespace KeepWalletDesktop.ViewModels;

public sealed record TransactionItemViewModel(
    int Id,
    Guid AccountId,
    string Type,
    decimal Amount,
    string Currency,
    string? CategoryCode,
    string CategoryDisplay,
    string? Description,
    DateTimeOffset TransactionDate,
    string TypeDisplay)
{
    private const string DateTimeDisplayFormat = "dd.MM.yyyy HH:mm";

    public bool IsIncome => Type == "income";
    public string AmountDisplay => $"{(IsIncome ? "+" : "-")}{Amount.ToString("#,##0.00", CultureInfo.InvariantCulture)}{(Currency == "UAH" ? "₴" : $" {Currency}")}";
    public string TransactionDateDisplay => TransactionDate.ToString(DateTimeDisplayFormat);
    public string TransactionDateShortDisplay => TransactionDate.ToString("dd.MM");
    public string DescriptionDisplay => string.IsNullOrWhiteSpace(Description) ? "-" : Description;
}

public sealed record SavingOptionViewModel(
    int Id,
    string Name,
    string AmountDisplay,
    bool IsSelected);

public sealed record SavingRateHistoryItemViewModel(
    int Id,
    decimal InterestRate,
    string CreatedAtDisplay)
{
    public string InterestRateDisplay => $"{InterestRate:0.####}%";
}

public sealed record SavingItemViewModel(
    int Id,
    string Name,
    decimal Amount,
    short? Priority,
    bool IsPaid,
    string AmountDisplay,
    double ProgressPercent,
    string ProgressDisplay)
{
    public bool IsFullyFunded => ProgressPercent >= 100;
}

public sealed record ScheduledPaymentItemViewModel(
    int Id,
    string Name,
    Guid AccountId,
    int CategoryId,
    decimal Amount,
    string Type,
    string CategoryDisplay,
    string? Description,
    string DueDateDisplay,
    string Frequency,
    bool IsActive,
    string AmountDisplay,
    string StatusDisplay)
{
    public bool IsPaid => !IsActive;
    public string DescriptionDisplay => string.IsNullOrWhiteSpace(Description) ? "-" : Description;
}

public sealed record CategoryLegendItemViewModel(
    string Name,
    decimal Amount,
    IBrush ColorBrush,
    string AmountDisplay);

public partial class CategoryDetailItemViewModel : ObservableObject
{
    public CategoryDetailItemViewModel(
        string categoryCode,
        string name,
        string typeDisplay,
        int transactionCount,
        decimal amount,
        decimal limit,
        IBrush colorBrush)
    {
        CategoryCode = categoryCode;
        Name = name;
        TypeDisplay = typeDisplay;
        TransactionCount = transactionCount;
        Amount = amount;
        Limit = limit;
        ColorBrush = colorBrush;
        limitInput = limit.ToString("0.##", CultureInfo.InvariantCulture);
        RebuildGauge();
    }

    public string CategoryCode { get; }
    public string Name { get; }
    public string TypeDisplay { get; }
    public int TransactionCount { get; }
    public decimal Amount { get; }
    public decimal Limit { get; private set; }
    public IBrush ColorBrush { get; }
    public string AmountDisplay => $"{Amount:N2}";
    public string LimitDisplay => $"{Limit:N2}";
    public bool IsLimitDisabled => Limit <= 0;
    public decimal UsagePercent => IsLimitDisabled ? 0 : Amount / Limit * 100;
    public string UsageDisplay => IsLimitDisabled ? "Off" : $"{Math.Min(999, UsagePercent):0.#}%";
    public string SpentLimitDisplay => $"{Amount:N2} / {Limit:N2}";
    public string RemainingDisplay => $"{Math.Max(0, Limit - Amount):N2}";
    public decimal RemainingAmount => Limit - Amount;
    public int RiskSortRank => Amount <= 0 && Limit > 0
        ? 3
        : Amount <= 0
            ? 4
        : UsagePercent > 100
            ? 0
            : UsagePercent >= 80
                ? 1
                : 2;
    public string RiskBand => UsagePercent >= 90 ? "high" : UsagePercent >= 71 ? "medium" : "low";
    public ISeries[] GaugeSeries { get; private set; } = [];

    [ObservableProperty]
    private string limitInput;

    public bool TryApplyLimit()
    {
        if (!decimal.TryParse(LimitInput, NumberStyles.Number, CultureInfo.InvariantCulture, out var limit) || limit < 0)
        {
            LimitInput = Limit.ToString("0.##", CultureInfo.InvariantCulture);
            return false;
        }

        Limit = limit;
        RebuildGauge();
        OnPropertyChanged(nameof(LimitDisplay));
        OnPropertyChanged(nameof(IsLimitDisabled));
        OnPropertyChanged(nameof(UsagePercent));
        OnPropertyChanged(nameof(UsageDisplay));
        OnPropertyChanged(nameof(SpentLimitDisplay));
        OnPropertyChanged(nameof(RemainingDisplay));
        OnPropertyChanged(nameof(RemainingAmount));
        OnPropertyChanged(nameof(RiskSortRank));
        OnPropertyChanged(nameof(RiskBand));
        return true;
    }

    private void RebuildGauge()
    {
        var used = IsLimitDisabled ? 1 : Math.Min((double)Amount, (double)Limit);
        var remaining = IsLimitDisabled ? 0 : Math.Max(0, (double)Limit - used);
        var gaugeColor = UsagePercent >= 90
            ? new SKColor(220, 38, 38)
            : UsagePercent >= 71
                ? new SKColor(234, 179, 8)
                : new SKColor(22, 163, 74);
        if (IsLimitDisabled)
        {
            gaugeColor = new SKColor(148, 163, 184);
        }

        GaugeSeries =
        [
            new PieSeries<double>
            {
                Values = [used],
                Fill = new SolidColorPaint(gaugeColor),
                Stroke = null,
                InnerRadius = 78,
                DataLabelsSize = 0
            },
            new PieSeries<double>
            {
                Values = [remaining],
                Fill = new SolidColorPaint(new SKColor(226, 232, 240)),
                Stroke = null,
                InnerRadius = 78,
                DataLabelsSize = 0
            }
        ];

        OnPropertyChanged(nameof(GaugeSeries));
    }
}

public sealed record BankAccountItemViewModel(
    Guid Id,
    string Name,
    string Currency,
    decimal Balance,
    bool IsDefault,
    Guid? GroupId,
    string BalanceDisplay);

public sealed record ShareGroupOptionViewModel(
    Guid? GroupId,
    string Name);

public sealed record GroupItemViewModel(
    Guid Id,
    string Name,
    string RoleName,
    int MemberCount,
    string CreatedAtDisplay);

public sealed record GroupMemberItemViewModel(
    Guid GroupId,
    Guid UserId,
    string Username,
    string FullName,
    string RoleName,
    string JoinedAtDisplay);

public sealed record UserListItemViewModel(
    Guid Id,
    string Username,
    string Email,
    string FullName,
    string RoleName,
    bool IsActive,
    string CreatedAtDisplay);

public sealed record AuditLogItemViewModel(
    long Id,
    string Username,
    string Action,
    string EntityType,
    string Details,
    string Device,
    string CreatedAtDisplay);
