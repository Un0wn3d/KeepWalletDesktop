using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Media;
using Avalonia.Media.Imaging;
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
    Guid? GroupId,
    string? GroupName,
    string? SharedBy,
    string Type,
    int? SavingId,
    decimal Amount,
    string Currency,
    string? CategoryCode,
    string CategoryDisplay,
    string? CategoryIconKey,
    string? Description,
    DateTimeOffset TransactionDate,
    string TypeDisplay)
{
    private const string DateTimeDisplayFormat = "dd.MM.yyyy HH:mm";

    public bool IsIncome => Type == "income";
    public bool IsShared => GroupId.HasValue;
    public string SharedDisplay => IsShared
        ? $"{GroupName ?? "Group"} - {SharedBy ?? "-"}"
        : string.Empty;
    public string AmountDisplay => $"{(IsIncome ? "+" : "-")}{FormatAmount(Amount)}{(Currency == "UAH" ? " ₴" : $" {Currency}")}";
    public string ReversedAmountDisplay => $"{(IsIncome ? "-" : "+")}{FormatAmount(Amount)}{(Currency == "UAH" ? " ₴" : $" {Currency}")}";
    public string TransactionDateDisplay => TransactionDate.ToString(DateTimeDisplayFormat);
    public string TransactionDateShortDisplay => TransactionDate.ToString("dd.MM");
    public string DescriptionDisplay => string.IsNullOrWhiteSpace(Description) ? "-" : Description;
    public string CategoryInitial => string.IsNullOrWhiteSpace(CategoryDisplay) ? "?" : CategoryDisplay.Trim()[0].ToString().ToUpperInvariant();
    public string CategoryIcon => CategoryIconCatalog.GetGlyph(CategoryIconKey ?? CategoryCode, Type);
    public Bitmap? CategoryIconSampleImage => CategoryIconCatalog.GetSampleImage(CategoryIconKey ?? CategoryCode);
    public bool HasCategoryIconSampleImage => CategoryIconSampleImage is not null;
    public IBrush CategoryAnchorBrush => IsIncome
        ? new SolidColorBrush(Color.FromRgb(209, 250, 229))
        : new SolidColorBrush(Color.FromRgb(243, 244, 246));

    private static string FormatAmount(decimal amount) =>
        decimal.Truncate(amount) == amount
            ? amount.ToString("#,##0", CultureInfo.InvariantCulture)
            : amount.ToString("#,##0.##", CultureInfo.InvariantCulture);

}

public sealed record SavingOptionViewModel(
    int Id,
    Guid? GroupId,
    string Name,
    string DeadlineDisplay,
    string AmountDisplay,
    string TargetAmountDisplay,
    double ProgressPercent,
    string ProgressDisplay,
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
    string ProgressDisplay,
    bool IsFullyFunded);

public sealed record ScheduledPaymentItemViewModel(
    int Id,
    string Name,
    Guid AccountId,
    int CategoryId,
    decimal Amount,
    string Type,
    string CategoryDisplay,
    string? CategoryIconKey,
    string? Description,
    string DueDateDisplay,
    string Frequency,
    bool IsActive,
    string AmountDisplay,
    string StatusDisplay)
{
    public bool IsPaid => !IsActive;
    public string CategoryIcon => CategoryIconCatalog.GetGlyph(CategoryIconKey, Type);
    public string DescriptionDisplay => string.IsNullOrWhiteSpace(Description) ? "-" : Description;
    public string DaysRemainingDisplay
    {
        get
        {
            if (!DateTime.TryParseExact(DueDateDisplay, "dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var due))
            {
                return "-";
            }

            var days = (due.Date - DateTime.Today).Days;
            if (days < 0)
            {
                return $"Просрочено {-days} дн.";
            }

            return days == 0 ? "Сьогодні" : $"{days} дн.";
        }
    }
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

public partial class GroupItemViewModel : ObservableObject
{
    public GroupItemViewModel(Guid id, string name, string iconKey, string roleName, int memberCount, string createdAtDisplay)
    {
        Id = id;
        Name = name;
        selectedIconKey = iconKey;
        RoleName = roleName;
        MemberCount = memberCount;
        CreatedAtDisplay = createdAtDisplay;
    }

    public Guid Id { get; }
    public string Name { get; }
    public string RoleName { get; }
    public int MemberCount { get; }
    public string CreatedAtDisplay { get; }
    public int RoleSortOrder => RoleName.ToLowerInvariant() switch
    {
        "owner" => 0,
        "member" => 1,
        "viewer" => 2,
        _ => 3
    };
    public bool IsOwnerRole => string.Equals(RoleName, "owner", StringComparison.OrdinalIgnoreCase);
    public bool IsMemberRole => string.Equals(RoleName, "member", StringComparison.OrdinalIgnoreCase);
    public bool IsViewerRole => string.Equals(RoleName, "viewer", StringComparison.OrdinalIgnoreCase);
    public string IconGlyph => CategoryIconCatalog.GetGlyph(SelectedIconKey, "expense");
    public Bitmap? IconSampleImage => CategoryIconCatalog.GetSampleImage(SelectedIconKey);
    public bool HasIconSampleImage => IconSampleImage is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IconGlyph))]
    [NotifyPropertyChangedFor(nameof(IconSampleImage))]
    [NotifyPropertyChangedFor(nameof(HasIconSampleImage))]
    private string selectedIconKey;

    [ObservableProperty]
    private bool isActive;

    public void RefreshIconGlyph()
    {
        OnPropertyChanged(nameof(IconGlyph));
        OnPropertyChanged(nameof(IconSampleImage));
        OnPropertyChanged(nameof(HasIconSampleImage));
    }
}

public partial class GroupMemberItemViewModel : ObservableObject
{
    public GroupMemberItemViewModel(Guid groupId, Guid userId, string username, string fullName, string roleName, string joinedAtDisplay)
    {
        GroupId = groupId;
        UserId = userId;
        Username = username;
        FullName = fullName;
        this.roleName = roleName;
        selectedRoleName = roleName;
        JoinedAtDisplay = joinedAtDisplay;
    }

    public Guid GroupId { get; }
    public Guid UserId { get; }
    public string Username { get; }
    public string FullName { get; }
    public string JoinedAtDisplay { get; }
    public string DisplayName => string.IsNullOrWhiteSpace(FullName) || FullName == "-" ? Username : $"{FullName} - {Username}";

    [ObservableProperty]
    private string roleName;

    [ObservableProperty]
    private string selectedRoleName;

    [ObservableProperty]
    private bool isEditingRole;
}

public sealed record UserListItemViewModel(
    Guid Id,
    string Username,
    string Email,
    string FullName,
    string RoleName,
    bool IsActive,
    string CreatedAtDisplay)
{
    public string DisplayName => string.IsNullOrWhiteSpace(FullName) || FullName == "-"
        ? Username
        : $"{FullName} - {Username}";
}

public sealed record AuditLogItemViewModel(
    long Id,
    string Username,
    string Action,
    string EntityType,
    string Details,
    string Device,
    string CreatedAtDisplay);
