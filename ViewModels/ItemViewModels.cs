using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System;
using System.Collections.Generic;
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
    string CategoryColor,
    string? GroupColor,
    string? Description,
    DateTimeOffset TransactionDate,
    string TypeDisplay,
    IReadOnlyList<Guid>? SharedGroupIds = null,
    string? AccountName = null)
{
    private const string DateTimeDisplayFormat = "dd.MM.yyyy HH:mm";

    public bool IsIncome => Type == "income";
    public IReadOnlyList<Guid> GroupIds => SharedGroupIds ?? (GroupId.HasValue ? [GroupId.Value] : []);
    public bool IsShared => GroupIds.Count > 0;
    public string GroupDisplay => IsShared
        ? GroupName ?? "Group"
        : string.Empty;
    public string SharedDisplay => IsShared
        ? SharedBy ?? "-"
        : string.Empty;
    public string AmountDisplay => $"{(IsIncome ? "+" : "-")}{FormatAmount(Amount)} {GetCurrencySymbol(Currency)}";
    public string ReversedAmountDisplay => $"{(IsIncome ? "-" : "+")}{FormatAmount(Amount)} {GetCurrencySymbol(Currency)}";
    public string TransactionDateDisplay => TransactionDate.ToString(DateTimeDisplayFormat);
    public string TransactionDateShortDisplay => TransactionDate.ToString("dd.MM");
    public string AccountDisplay => string.IsNullOrWhiteSpace(AccountName) ? "-" : AccountName;
    public string PayerDisplay => string.IsNullOrWhiteSpace(SharedBy) ? "-" : SharedBy;
    public string SavingTransactionDetailsDisplay => $"Рахунок: {AccountDisplay} · Користувач: {PayerDisplay}";
    public string DescriptionDisplay => string.IsNullOrWhiteSpace(Description) ? "-" : Description;
    public string CategoryInitial => string.IsNullOrWhiteSpace(CategoryDisplay) ? "?" : CategoryDisplay.Trim()[0].ToString().ToUpperInvariant();
    public string CategoryIcon => CategoryIconCatalog.GetGlyph(CategoryIconKey ?? CategoryCode, Type);
    public Bitmap? CategoryIconSampleImage => CategoryIconCatalog.GetSampleImage(CategoryIconKey ?? CategoryCode);
    public bool HasCategoryIconSampleImage => CategoryIconSampleImage is not null;
    public IBrush CategoryAnchorBrush => new SolidColorBrush(ParseColorOrDefault(CategoryColor));
    public IBrush GroupBrush => new SolidColorBrush(ParseColorOrDefault(GroupColor));

    private static string FormatAmount(decimal amount) =>
        decimal.Truncate(amount) == amount
            ? amount.ToString("#,##0", CultureInfo.InvariantCulture)
            : amount.ToString("#,##0.##", CultureInfo.InvariantCulture);

    private static string GetCurrencySymbol(string currency) =>
        currency.ToUpperInvariant() switch
        {
            "UAH" => "₴",
            "USD" => "$",
            "EUR" => "€",
            "GBP" => "£",
            "PLN" => "zł",
            _ => currency
        };

    private static Color ParseColorOrDefault(string? rawColor) =>
        Color.TryParse(rawColor?.Trim(), out var color)
            ? color
            : Color.Parse("#E5E7EB");
}

public sealed record SavingOptionViewModel(
    int Id,
    Guid? GroupId,
    string Name,
    string IconKey,
    string Color,
    string Currency,
    string DeadlineDisplay,
    string AmountDisplay,
    string TargetAmountDisplay,
    double ProgressPercent,
    string ProgressDisplay,
    bool IsSelected,
    bool IsCompleted,
    string? OwnerDisplay = null,
    Guid? UserId = null,
    IReadOnlyList<Guid>? SharedGroupIds = null)
{
    public IReadOnlyList<Guid> GroupIds => SharedGroupIds ?? (GroupId.HasValue ? [GroupId.Value] : []);
    public string IconGlyph => CategoryIconCatalog.GetGlyph(IconKey, "expense");
    public IBrush AccentBrush => new SolidColorBrush(Avalonia.Media.Color.Parse(string.IsNullOrWhiteSpace(Color) ? "#E5E7EB" : Color));
    public string OwnerDisplayText => string.IsNullOrWhiteSpace(OwnerDisplay) ? "-" : OwnerDisplay;
}

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
    int RecurringPaymentId,
    string Name,
    Guid AccountId,
    Guid? GroupId,
    string? GroupName,
    string? GroupColor,
    int CategoryId,
    decimal Amount,
    string Type,
    string CategoryDisplay,
    string? CategoryIconKey,
    string CategoryColor,
    string? Description,
    string Currency,
    DateTimeOffset DueDate,
    TimeSpan RepeatInterval,
    string Frequency,
    bool IsActive,
    string AmountDisplay,
    string StatusDisplay,
    string DaysRemainingDisplay,
    string? OwnerDisplay = null,
    IReadOnlyList<Guid>? SharedGroupIds = null)
{
    public bool IsIncome => Type == "income";
    public bool IsPaid => !IsActive;
    public IReadOnlyList<Guid> GroupIds => SharedGroupIds ?? (GroupId.HasValue ? [GroupId.Value] : []);
    public bool IsShared => GroupIds.Count > 0;
    public string GroupDisplay => IsShared
        ? OwnerDisplayText
        : string.Empty;
    public string CategoryIcon => CategoryIconCatalog.GetGlyph(CategoryIconKey, Type);
    public string DescriptionDisplay => string.IsNullOrWhiteSpace(Description) ? "-" : Description;
    public string OwnerDisplayText => string.IsNullOrWhiteSpace(OwnerDisplay) ? "-" : OwnerDisplay;
    public string DueDateDisplay => DueDate.ToLocalTime().ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
    public IBrush CategoryAnchorBrush => new SolidColorBrush(ParseColorOrDefault(CategoryColor));
    public IBrush GroupBrush => new SolidColorBrush(ParseColorOrDefault(GroupColor));
    public bool IsOverdue => IsActive && DueDate.ToLocalTime().Date < DateTime.Today;
    public bool IsDueSoon => IsActive && !IsOverdue && (DueDate.ToLocalTime().Date - DateTime.Today).Days <= 3;
    public bool IsDueLater => IsActive && !IsOverdue &&
                              (DueDate.ToLocalTime().Date - DateTime.Today).Days > 3 &&
                              (DueDate.ToLocalTime().Date - DateTime.Today).Days <= 14;
    public bool IsDueFar => IsActive && !IsOverdue && (DueDate.ToLocalTime().Date - DateTime.Today).Days > 14;
    public string AmountWithSymbolDisplay => $"{(IsIncome ? "+" : "-")}{FormatAmount(Amount)} {GetCurrencySymbol(Currency)}";

    private static string FormatAmount(decimal amount) =>
        decimal.Truncate(amount) == amount
            ? amount.ToString("#,##0", CultureInfo.InvariantCulture)
            : amount.ToString("#,##0.##", CultureInfo.InvariantCulture);

    private static string GetCurrencySymbol(string currency) =>
        currency.ToUpperInvariant() switch
        {
            "UAH" => "₴",
            "USD" => "$",
            "EUR" => "€",
            "PLN" => "zł",
            "SEK" => "kr",
            _ => currency
        };

    private static Color ParseColorOrDefault(string? rawColor) =>
        Color.TryParse(rawColor?.Trim(), out var color)
            ? color
            : Color.Parse("#E5E7EB");
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
    string BalanceDisplay,
    string? GroupName = null,
    string? GroupColor = null,
    string? GroupOwnerDisplay = null,
    Guid? UserId = null,
    IReadOnlyList<Guid>? SharedGroupIds = null)
{
    public IReadOnlyList<Guid> GroupIds => SharedGroupIds ?? (GroupId.HasValue ? [GroupId.Value] : []);
    public bool IsShared => GroupIds.Count > 0;
    public string GroupDisplay => IsShared
        ? string.IsNullOrWhiteSpace(GroupOwnerDisplay)
            ? "-"
            : GroupOwnerDisplay
        : string.Empty;
    public string OwnerDisplayText => string.IsNullOrWhiteSpace(GroupOwnerDisplay) ? "-" : GroupOwnerDisplay;
    public IBrush GroupBrush => new SolidColorBrush(ParseColorOrDefault(GroupColor));

    private static Color ParseColorOrDefault(string? rawColor) =>
        Color.TryParse(rawColor?.Trim(), out var color)
            ? color
            : Color.Parse("#E5E7EB");
}

public sealed record ShareGroupOptionViewModel(
    Guid? GroupId,
    string Name);

public partial class GroupSelectionItemViewModel : ObservableObject
{
    public GroupSelectionItemViewModel(Guid id, string name, bool isSelected = false)
    {
        Id = id;
        Name = name;
        this.isSelected = isSelected;
    }

    public Guid Id { get; }
    public string Name { get; }

    [ObservableProperty]
    private bool isSelected;
}

public sealed record GroupAccessResourceItemViewModel(
    string ResourceType,
    string Id,
    string Name,
    string Details,
    object Source)
{
    public string DisplayName => string.IsNullOrWhiteSpace(Details)
        ? Name
        : $"{Name} - {Details}";
}

public partial class GroupItemViewModel : ObservableObject
{
    public GroupItemViewModel(Guid id, string name, string iconKey, string? color, string roleName, int memberCount, string createdAtDisplay, string? roleDisplayName = null, string? ownerDisplay = null)
    {
        Id = id;
        Name = name;
        selectedIconKey = iconKey;
        selectedColor = string.IsNullOrWhiteSpace(color) ? "#3B82F6" : color;
        RoleName = roleName;
        RoleDisplayName = roleDisplayName ?? roleName;
        OwnerDisplay = ownerDisplay;
        MemberCount = memberCount;
        CreatedAtDisplay = createdAtDisplay;
    }

    public Guid Id { get; }
    public string Name { get; }
    public string RoleName { get; }
    public string RoleDisplayName { get; }
    public string? OwnerDisplay { get; }
    public string OwnerDisplayText => string.IsNullOrWhiteSpace(OwnerDisplay) ? RoleDisplayName : OwnerDisplay;
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
    public IBrush AccentBrush => new SolidColorBrush(ParseColorOrDefault(SelectedColor));

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IconGlyph))]
    [NotifyPropertyChangedFor(nameof(IconSampleImage))]
    [NotifyPropertyChangedFor(nameof(HasIconSampleImage))]
    private string selectedIconKey;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AccentBrush))]
    private string selectedColor;

    [ObservableProperty]
    private bool isActive;

    public void RefreshIconGlyph()
    {
        OnPropertyChanged(nameof(IconGlyph));
        OnPropertyChanged(nameof(IconSampleImage));
        OnPropertyChanged(nameof(HasIconSampleImage));
    }

    private static Color ParseColorOrDefault(string? rawColor) =>
        Color.TryParse(rawColor?.Trim(), out var color)
            ? color
            : Color.Parse("#3B82F6");
}

public partial class GroupMemberItemViewModel : ObservableObject
{
    public GroupMemberItemViewModel(Guid groupId, Guid userId, string username, string fullName, string roleName, string joinedAtDisplay, bool isRemovable = false, string? roleDisplayName = null)
    {
        GroupId = groupId;
        UserId = userId;
        Username = username;
        FullName = fullName;
        this.roleName = roleName;
        selectedRoleName = roleName;
        JoinedAtDisplay = joinedAtDisplay;
        IsRemovable = isRemovable;
        RoleDisplayName = roleDisplayName ?? roleName;
    }

    public Guid GroupId { get; }
    public Guid UserId { get; }
    public string Username { get; }
    public string FullName { get; }
    public string JoinedAtDisplay { get; }
    public string RoleDisplayName { get; }
    public string DisplayName => Username;
    public bool IsOwnerRole => string.Equals(RoleName, "owner", StringComparison.OrdinalIgnoreCase);
    public bool IsRemovable { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOwnerRole))]
    private string roleName;

    [ObservableProperty]
    private string selectedRoleName;

    [ObservableProperty]
    private bool isEditingRole;
}

public partial class UserListItemViewModel : ObservableObject
{
    public UserListItemViewModel(
        Guid id,
        string username,
        string email,
        string fullName,
        string roleName,
        bool isActive,
        string createdAtDisplay)
    {
        Id = id;
        usernameInput = username;
        emailInput = email;
        fullNameInput = fullName == "-" ? string.Empty : fullName;
        selectedRoleName = roleName;
        isActiveInput = isActive;
        createdAtInput = createdAtDisplay;
    }

    public Guid Id { get; }
    public bool IsNew => Id == Guid.Empty;

    public string Username => UsernameInput;
    public string Email => EmailInput;
    public string FullName => string.IsNullOrWhiteSpace(FullNameInput) ? "-" : FullNameInput;
    public string RoleName => SelectedRoleName;
    public bool IsActive => IsActiveInput;
    public string CreatedAtDisplay => CreatedAtInput;

    public string DisplayName => string.IsNullOrWhiteSpace(FullName) || FullName == "-"
        ? Username
        : $"{FullName} - {Username}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Username))]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    private string usernameInput = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Email))]
    private string emailInput = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FullName))]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    private string fullNameInput = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RoleName))]
    private string selectedRoleName = "user";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsActive))]
    private bool isActiveInput;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CreatedAtDisplay))]
    private string createdAtInput = string.Empty;

    [ObservableProperty]
    private string passwordInput = string.Empty;

    [ObservableProperty]
    private bool isEditing;
}

public sealed record AuditLogItemViewModel(
    long Id,
    string Username,
    string Action,
    string EntityType,
    string Details,
    string Device,
    DateTimeOffset CreatedAt,
    string CreatedAtDisplay);
