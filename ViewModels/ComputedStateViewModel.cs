using Avalonia.Media;
using Avalonia.Media.Imaging;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.ImageFilters;
using SkiaSharp;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace KeepWalletDesktop.ViewModels;

public partial class MainViewModel
{
    public bool IsAuthPage => CurrentPage == AuthPage;
    public bool IsHomePage => CurrentPage == HomePage;
    public bool IsBankAccountsPage => CurrentPage == BankAccountsPage;
    public bool IsSavingsPage => CurrentPage == SavingsPage;
    public bool IsGroupsPage => CurrentPage == GroupsPage;
    public bool IsScheduledPaymentsPage => CurrentPage == ScheduledPaymentsPage;
    public bool IsCategoriesPage => CurrentPage == CategoriesPage;
    public bool IsAllTransactionsPage => CurrentPage == AllTransactionsPage;
    public bool IsReportingPage => CurrentPage == ReportingPage;
    public bool IsSettingsPage => CurrentPage == SettingsPage;
    public bool IsLogsPage => CurrentPage == LogsPage;
    public bool IsCurrentUserAdmin => string.Equals(CurrentUserRole, "admin", StringComparison.OrdinalIgnoreCase);
    public IReadOnlyList<string> AdminRoleOptions => ["admin", "user"];
    public string ApiConnectionStatus => IsApiAuthorized ? Texts.ConnectedStatus : Texts.DisconnectedStatus;
    public bool CanOpenAppPages => IsApiAuthorized;

    public string SelectedBankAccountTitle => SelectedBankAccount?.Name ?? Texts.BankAccountsTitle;
    public string SelectedBankAccountBalance => SelectedBankAccount?.BalanceDisplay ?? "0.00 UAH";
    public string SelectedBankAccountCurrency => SelectedBankAccount?.Currency ?? TransactionCurrencyInput;
    public bool IsEditingBankAccount => EditingBankAccountId.HasValue;
    public string BankAccountDialogTitle => IsEditingBankAccount ? Texts.BankAccountDetailsTitle : Texts.NewBankAccountButton;
    public string SaveBankAccountButtonText => IsEditingBankAccount ? Texts.UpdateTransactionButton : Texts.SaveServerSettingsButton;
    public bool HasBankAccounts => BankAccounts.Count > 0;
    public bool HasNoBankAccounts => !HasBankAccounts;

    public bool HasSavings => Savings.Count > 0;
    public bool HasNoSavings => !HasSavings;
    public IEnumerable<SavingOptionViewModel> ActiveSavings => Savings.Where(s => !s.IsCompleted);
    public IEnumerable<SavingOptionViewModel> CompletedSavings => Savings.Where(s => s.IsCompleted);
    public bool HasActiveSavings => ActiveSavings.Any();
    public bool HasCompletedSavings => CompletedSavings.Any();
    public bool HasSavingTransactions => SavingTransactions.Count > 0;
    public bool HasNoSavingTransactions => !HasSavingTransactions;

    public bool HasGroups => Groups.Count > 0;
    public bool HasNoGroups => !HasGroups;
    public IEnumerable<TransactionItemViewModel> SharedTransactions => AllTransactions.Where(t => t.IsShared).Take(5);
    public bool HasSharedTransactions => AllTransactions.Any(t => t.IsShared);
    public IEnumerable<BankAccountItemViewModel> SelectedGroupBankAccounts =>
        SelectedAccessGroup is null ? [] : BankAccounts.Where(a => a.GroupIds.Contains(SelectedAccessGroup.Id));
    public IEnumerable<ScheduledPaymentItemViewModel> SelectedGroupScheduledPayments =>
        SelectedAccessGroup is null ? [] : ScheduledPayments.Where(p => p.GroupIds.Contains(SelectedAccessGroup.Id));
    public IEnumerable<TransactionItemViewModel> SelectedGroupTransactions =>
        SelectedAccessGroup is null ? [] : AllTransactions.Where(t => t.GroupIds.Contains(SelectedAccessGroup.Id));
    public IEnumerable<SavingOptionViewModel> SelectedGroupSavings =>
        SelectedAccessGroup is null ? [] : Savings.Where(s => s.GroupIds.Contains(SelectedAccessGroup.Id));
    public bool HasSelectedGroupBankAccounts => SelectedGroupBankAccounts.Any();
    public bool HasSelectedGroupScheduledPayments => SelectedGroupScheduledPayments.Any();
    public bool HasSelectedGroupTransactions => SelectedGroupTransactions.Any();
    public bool HasSelectedGroupSavings => SelectedGroupSavings.Any();
    public bool HasSelectedGroupAccessContent => HasSelectedGroupBankAccounts || HasSelectedGroupScheduledPayments || HasSelectedGroupTransactions || HasSelectedGroupSavings;
    public IReadOnlyList<CategoryOptionViewModel> GroupAccessResourceTypeOptions =>
    [
        new("account", Texts.GroupAccessAccountsTitle),
        new("scheduled", Texts.GroupAccessScheduledPaymentsTitle),
        new("saving", Texts.GroupAccessSavingsTitle)
    ];
    public IEnumerable<GroupAccessResourceItemViewModel> GroupAccessResourceOptions =>
        SelectedGroupAccessResourceType?.Code switch
        {
            "account" => BankAccounts.Where(a => SelectedAccessGroup is null || !a.GroupIds.Contains(SelectedAccessGroup.Id)).OrderBy(a => a.Name).Select(a => new GroupAccessResourceItemViewModel("account", a.Id.ToString(), a.Name, a.BalanceDisplay, a)),
            "scheduled" => ScheduledPayments.Where(p => SelectedAccessGroup is null || !p.GroupIds.Contains(SelectedAccessGroup.Id)).OrderBy(p => p.Name).Select(p => new GroupAccessResourceItemViewModel("scheduled", p.Id.ToString(CultureInfo.InvariantCulture), p.Name, p.DueDateDisplay, p)),
            "saving" => Savings.Where(s => SelectedAccessGroup is null || !s.GroupIds.Contains(SelectedAccessGroup.Id)).OrderBy(s => s.Name).Select(s => new GroupAccessResourceItemViewModel("saving", s.Id.ToString(CultureInfo.InvariantCulture), s.Name, s.AmountDisplay, s)),
            _ => []
        };
    public bool HasGroupAccessResourceOptions => GroupAccessResourceOptions.Any();

    public double TransactionCategoryPillWidth => Math.Clamp((FilteredTransactions.Concat(VisibleTransactions).Select(t => t.CategoryDisplay?.Length ?? 0).DefaultIfEmpty(12).Max() * 8) + 28, 100, 180);
    public double ScheduledPaymentCategoryPillWidth => Math.Clamp((ScheduledPayments.Select(t => t.CategoryDisplay?.Length ?? 0).DefaultIfEmpty(12).Max() * 8) + 28, 110, 170);
    public double TransactionGroupPillWidth => Math.Clamp((FilteredTransactions.Concat(VisibleTransactions).Where(t => t.IsShared).Select(t => t.GroupDisplay?.Length ?? 0).DefaultIfEmpty(12).Max() * 8) + 28, 100, 260);
    public double ScheduledPaymentGroupPillWidth => Math.Clamp((FilteredScheduledPayments.Where(t => t.IsShared).Select(t => t.GroupDisplay?.Length ?? 0).DefaultIfEmpty(12).Max() * 8) + 28, 100, 260);
    public double BankAccountGroupPillWidth => Math.Clamp((BankAccounts.Where(a => a.IsShared).Select(a => a.GroupDisplay?.Length ?? 0).DefaultIfEmpty(12).Max() * 8) + 28, 100, 320);

    public double RemainingAmount => Math.Max(0, GoalAmount - SavedAmount);
    public string ApiUserDisplay => $"{Texts.ApiUserPrefix}: {CurrentUsername}";
    public string StatusDisplay => $"{Texts.StatusLabel}: {ApiConnectionStatus}";
    public string UserDisplay => $"{Texts.UserLabel}: {CurrentUsername}";
    public string EmailDisplay => $"{Texts.EmailLabel}: {CurrentUserEmail}";
    public string RoleDisplay => $"{Texts.RoleLabel}: {CurrentUserRole}";
    public string AdminUsersCountDisplay => $"{Texts.UsersTitle}: {AdminUsers.Count}";
    public bool AdminUsersEditingAny => AdminUsers.Any(user => user.IsEditing);
    public IEnumerable<AuditLogItemViewModel> VisibleUserLogs => UserLogs.Take(5);
    public IEnumerable<string> LogUserFilterOptions => new[] { Texts.LogsAllUsersFilter }.Concat(UserLogs.Select(log => log.Username).Where(value => !string.IsNullOrWhiteSpace(value) && !string.Equals(value, "-", StringComparison.OrdinalIgnoreCase) && !string.Equals(value, "System", StringComparison.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value));
    public IEnumerable<string> LogActionFilterOptions => new[] { Texts.LogsAllActionsFilter }.Concat(UserLogs.Select(log => log.Action).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value));
    public IEnumerable<AuditLogItemViewModel> FilteredUserLogs => UserLogs
        .Where(log => string.IsNullOrWhiteSpace(SelectedLogUserFilter) || string.Equals(SelectedLogUserFilter, Texts.LogsAllUsersFilter, StringComparison.OrdinalIgnoreCase) || string.Equals(log.Username, SelectedLogUserFilter, StringComparison.OrdinalIgnoreCase))
        .Where(log => string.IsNullOrWhiteSpace(SelectedLogActionFilter) || string.Equals(SelectedLogActionFilter, Texts.LogsAllActionsFilter, StringComparison.OrdinalIgnoreCase) || string.Equals(log.Action, SelectedLogActionFilter, StringComparison.OrdinalIgnoreCase))
        .Where(log => !LogDateFromFilter.HasValue || log.CreatedAt.Date >= LogDateFromFilter.Value.Date)
        .Where(log => !LogDateToFilter.HasValue || log.CreatedAt.Date <= LogDateToFilter.Value.Date)
        .OrderByDescending(log => log.CreatedAt);

    public string ReportSelectedCategoryExpenseTotalDisplay => $"{Texts.ReportExpenseSumPrefix}: {ReportSelectedCategoryExpenseTotal:N2}";
    public string ReportSelectedCategoryTransactionCountDisplay => $"{Texts.ReportTransactionCountPrefix}: {ReportSelectedCategoryTransactionCount}";
    public decimal ReportIncomeTotal => AllTransactions.Where(t => t.IsIncome).Sum(t => t.Amount);
    public decimal ReportExpenseTotal => AllTransactions.Where(t => !t.IsIncome).Sum(t => t.Amount);
    public decimal ReportBalanceTotal => ReportIncomeTotal - ReportExpenseTotal;
    public bool IsCategoryPeriodReportView => string.Equals(SelectedReportView?.Code, "category_period", StringComparison.OrdinalIgnoreCase);
    public bool IsMonthlyPlanReportView => string.Equals(SelectedReportView?.Code, "monthly_plan", StringComparison.OrdinalIgnoreCase);
    public bool IsMonthlyExpensesReportView => string.Equals(SelectedReportView?.Code, "monthly_expenses", StringComparison.OrdinalIgnoreCase);
    public SolidColorPaint ChartTooltipBackgroundPaint => new(GetChartTooltipBackgroundColor()) { ImageFilter = new DropShadow(0, 0, 1, 1, GetChartTooltipBorderColor()) };
    public SolidColorPaint ChartTooltipTextPaint => new(GetChartTooltipTextColor());

    public string SavedAmountDisplay => $"{SavedAmount:N2}";
    public string RemainingAmountDisplay => $"{Texts.RemainingAmountPrefix}: {RemainingAmount:N2} {ActiveSavingCurrency}";
    public string ActiveSavingAmountGoalDisplay => $"{SavedAmount:N2}/{GoalAmount:N2} {ActiveSavingCurrency}";
    public bool HasAddAmountError => !string.IsNullOrWhiteSpace(AddAmountError);
    public bool HasSavingItemAmountError => !string.IsNullOrWhiteSpace(SavingItemAmountError);
    public string ActiveSavingDeadlineDisplay => BuildSavingDeadlineDisplay(ActiveSavingDeadline);
    public SavingOptionViewModel? ActiveSavingOption => ActiveSavingId.HasValue ? Savings.FirstOrDefault(s => s.Id == ActiveSavingId.Value) : null;
    public string ActiveSavingOwnerDisplay => ActiveSavingOption?.OwnerDisplayText ?? "-";
    public string ActiveSavingGroupDisplay
    {
        get
        {
            var groupIds = ActiveSavingOption?.GroupIds ?? [];
            var groupNames = groupIds.Select(id => Groups.FirstOrDefault(g => g.Id == id)?.Name).Where(name => !string.IsNullOrWhiteSpace(name)).ToArray();
            return groupNames.Length == 0 ? "-" : string.Join(", ", groupNames);
        }
    }
    public double ProgressPercent => GoalAmount <= 0 ? 0 : Math.Min(100, SavedAmount / GoalAmount * 100);
    public string SavingsDialogIconGlyph => CategoryIconCatalog.GetGlyph(SavingsDialogIconKey, "expense");
    public string ActiveSavingIconGlyph => CategoryIconCatalog.GetGlyph(ActiveSavingIconKey, "expense");
    public IBrush ActiveSavingColorBrush => new SolidColorBrush(Color.Parse(ActiveSavingColor));
    public IEnumerable<BankAccountItemViewModel> CompatibleSavingsBankAccounts => BankAccounts.Where(a => string.Equals(a.Currency, ActiveSavingCurrency, StringComparison.OrdinalIgnoreCase));

    public bool IsOnboardingAccountSetupVisible => !IsCategorySettingsMode;
    public ObservableCollection<CategoryDetailItemViewModel> CategoryDetails { get; private set; } = [];
    public IEnumerable<OnboardingCategoryOptionViewModel> OnboardingExpenseCategories => OnboardingCategories.Where(c => string.Equals(c.Type, "expense", StringComparison.OrdinalIgnoreCase));
    public IEnumerable<OnboardingCategoryOptionViewModel> OnboardingIncomeCategories => OnboardingCategories.Where(c => string.Equals(c.Type, "income", StringComparison.OrdinalIgnoreCase));
    public IEnumerable<CategoryManagementItemViewModel> ExpenseCategories => Categories.Where(c => string.Equals(c.Type, "expense", StringComparison.OrdinalIgnoreCase));
    public IEnumerable<CategoryManagementItemViewModel> IncomeCategories => Categories.Where(c => string.Equals(c.Type, "income", StringComparison.OrdinalIgnoreCase));
    public IEnumerable<CategoryManagementItemViewModel> MergeTargetCategories => Categories.Where(c => SelectedMergeSourceCategory is null || (c.Id != SelectedMergeSourceCategory.Id && string.Equals(c.Type, SelectedMergeSourceCategory.Type, StringComparison.OrdinalIgnoreCase)));
    public IReadOnlyList<CategoryIconOptionViewModel> CategoryIconOptions => CategoryIconCatalog.Options;
    public IReadOnlyList<string> CategoryColorOptions => SavingColorOptions;
    public IReadOnlyList<string> SavingColorOptions { get; } = ["#EF4444", "#F97316", "#F59E0B", "#EAB308", "#84CC16", "#22C55E", "#10B981", "#14B8A6", "#06B6D4", "#0EA5E9", "#3B82F6", "#6366F1", "#8B5CF6", "#EC4899", "#6B7280", "#111827"];
    public string CategoryNewIconGlyph => CategoryIconCatalog.GetGlyph(CategoryNewIconKey, CategoryNewType);
    public Bitmap? CategoryNewIconSampleImage => CategoryIconCatalog.GetSampleImage(CategoryNewIconKey);
    public bool HasCategoryNewIconSampleImage => CategoryNewIconSampleImage is not null;
    public bool IsNewCategoryExpenseTypeSelected => string.Equals(CategoryNewType, "expense", StringComparison.OrdinalIgnoreCase);
    public bool IsNewCategoryIncomeTypeSelected => string.Equals(CategoryNewType, "income", StringComparison.OrdinalIgnoreCase);
    public string NewGroupIconGlyph => CategoryIconCatalog.GetGlyph(NewGroupIconKey, "expense");
    public Bitmap? NewGroupIconSampleImage => CategoryIconCatalog.GetSampleImage(NewGroupIconKey);
    public bool HasNewGroupIconSampleImage => NewGroupIconSampleImage is not null;

    public bool IsEditingScheduledPayment => EditingScheduledPaymentId.HasValue;
    public bool IsEditingTransaction => EditingTransactionId.HasValue && !IsEditingScheduledPayment;
    public bool IsExpenseTransactionTypeSelected => string.Equals(TransactionType, "expense", StringComparison.OrdinalIgnoreCase);
    public bool IsIncomeTransactionTypeSelected => string.Equals(TransactionType, "income", StringComparison.OrdinalIgnoreCase);
    public bool IsPlannedTransactionMode => IsEditingScheduledPayment || (!IsEditingTransaction && TryBuildTransactionDateTime(TransactionSelectedDate, TransactionSelectedTime, out var transactionDate) && transactionDate.Date > DateTimeOffset.Now.Date);
    public string TransactionDialogTitle => IsEditingScheduledPayment ? Texts.NewPlannedTransactionTitle : IsEditingTransaction ? Texts.TransactionDetailsTitle : IsPlannedTransactionMode ? Texts.NewPlannedTransactionTitle : Texts.NewTransactionButton;
    public string SaveTransactionButtonText => IsEditingTransaction || IsEditingScheduledPayment ? Texts.UpdateTransactionButton : Texts.SaveTransactionButton;
    public bool CanConfirmScheduledPaymentFromDialog => IsEditingScheduledPayment;
    public string TransactionTypeDisplay => TransactionType == "income" ? Texts.TransactionIncomeType : Texts.TransactionExpenseType;
    public ObservableCollection<CategoryOptionViewModel> TransactionTypeFilterOptions { get; private set; } = [];
    public ObservableCollection<int> TransactionDayOptions { get; } = new(Enumerable.Range(1, 31));
    public ObservableCollection<int> TransactionMonthOptions { get; } = new(Enumerable.Range(1, 12));
    public ObservableCollection<int> TransactionYearOptions { get; } = new(Enumerable.Range(2000, 151));
    public bool HasTransactionAdvancedFilter => TransactionGroupFilterOptions.Any(option => option.IsSelected) || TransactionUserFilterOptions.Any(option => option.IsSelected);

    public bool IsAllScheduledPaymentsFilterSelected => string.Equals(ScheduledPaymentsFilter, "all", StringComparison.OrdinalIgnoreCase);
    public bool IsFutureScheduledPaymentsFilterSelected => string.Equals(ScheduledPaymentsFilter, "future", StringComparison.OrdinalIgnoreCase);
    public bool IsOverdueScheduledPaymentsFilterSelected => string.Equals(ScheduledPaymentsFilter, "overdue", StringComparison.OrdinalIgnoreCase);
    public bool HasScheduledPaymentTypeFilter => ScheduledPaymentTypeFilterOptions.Count > 2;
    public bool HasScheduledPaymentAdvancedFilter => ScheduledPaymentGroupFilterOptions.Any(option => option.IsSelected) || ScheduledPaymentUserFilterOptions.Any(option => option.IsSelected);
    public bool IsCustomScheduledRepeat => string.Equals(SelectedScheduledRepeatOption?.Code, "custom", StringComparison.OrdinalIgnoreCase);
    public ObservableCollection<CategoryOptionViewModel> ScheduledCustomSecondaryUnitOptions
    {
        get => ScheduledCustomRepeatSecondaryUnitOptions;
        set => ScheduledCustomRepeatSecondaryUnitOptions = value;
    }
    public ObservableCollection<CategoryOptionViewModel> ScheduledCustomTertiaryUnitOptions
    {
        get => ScheduledCustomRepeatTertiaryUnitOptions;
        set => ScheduledCustomRepeatTertiaryUnitOptions = value;
    }
    public ObservableCollection<CategoryOptionViewModel> ScheduledCustomQuaternaryUnitOptions
    {
        get => ScheduledCustomRepeatQuaternaryUnitOptions;
        set => ScheduledCustomRepeatQuaternaryUnitOptions = value;
    }
    public bool HasScheduledCustomSecondaryUnit => ScheduledCustomRepeatSecondaryUnitOptions.Count > 0;
    public bool HasScheduledCustomTertiaryUnit => ScheduledCustomRepeatTertiaryUnitOptions.Count > 0;
    public bool HasScheduledCustomQuaternaryUnit => ScheduledCustomRepeatQuaternaryUnitOptions.Count > 0;
    public IEnumerable<ScheduledPaymentItemViewModel> FilteredScheduledPayments => ScheduledPayments
        .Where(MatchesScheduledPaymentsFilter)
        .Where(MatchesScheduledPaymentsCurrencyFilter)
        .Where(MatchesScheduledPaymentsTypeFilter)
        .Where(MatchesScheduledPaymentsCategoryFilter)
        .Where(MatchesScheduledPaymentsGroupFilter)
        .Where(MatchesScheduledPaymentsUserFilter)
        .OrderBy(p => p.DueDate);
    public int ScheduledPaymentsTotalCount => FilteredScheduledPayments.Count();
    public string ScheduledPaymentsTotalAmountDisplay => FormatScheduledPaymentTotals(FilteredScheduledPayments);
    public int ScheduledPaymentsPaidThisMonthCount => AllTransactions.Count(IsPaidScheduledPaymentThisMonth);
    public string ScheduledPaymentsPaidThisMonthAmountDisplay => FormatTransactionTotals(AllTransactions.Where(IsPaidScheduledPaymentThisMonth));
    public int ScheduledPaymentsUnpaidCount => FilteredScheduledPayments.Count(p => p.IsActive);
    public string ScheduledPaymentsUnpaidAmountDisplay => FormatScheduledPaymentTotals(FilteredScheduledPayments.Where(p => p.IsActive));
    public int ScheduledPaymentsOverdueCount => FilteredScheduledPayments.Count(IsOverdueScheduledPayment);
    public string ScheduledPaymentsOverdueAmountDisplay => FormatScheduledPaymentTotals(FilteredScheduledPayments.Where(IsOverdueScheduledPayment));
    public int ScheduledPaymentsUpcomingCount => FilteredScheduledPayments.Count(p => p.IsActive && !IsOverdueScheduledPayment(p));
    public string ScheduledPaymentConfirmTitle => Texts.ConfirmScheduledPaymentButton;
    public bool CanEditPendingScheduledPaymentConfirmation =>
        PendingScheduledPaymentConfirmation is not null && IsOwnScheduledPayment(PendingScheduledPaymentConfirmation);

    private bool MatchesScheduledPaymentsFilter(ScheduledPaymentItemViewModel payment) =>
        ScheduledPaymentsFilter switch
        {
            "future" => payment.IsActive && !IsOverdueScheduledPayment(payment),
            "overdue" => IsOverdueScheduledPayment(payment),
            _ => true
        };

    private bool MatchesScheduledPaymentsCurrencyFilter(ScheduledPaymentItemViewModel payment) =>
        SelectedScheduledPaymentCurrencyFilter is null ||
        string.Equals(SelectedScheduledPaymentCurrencyFilter.Code, "all", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(payment.Currency, SelectedScheduledPaymentCurrencyFilter.Code, StringComparison.OrdinalIgnoreCase);

    private bool MatchesScheduledPaymentsTypeFilter(ScheduledPaymentItemViewModel payment) =>
        SelectedScheduledPaymentTypeFilter is null ||
        string.Equals(SelectedScheduledPaymentTypeFilter.Code, "all", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(payment.Type, SelectedScheduledPaymentTypeFilter.Code, StringComparison.OrdinalIgnoreCase);

    private bool MatchesScheduledPaymentsCategoryFilter(ScheduledPaymentItemViewModel payment) =>
        SelectedScheduledPaymentCategoryFilter is null ||
        string.Equals(SelectedScheduledPaymentCategoryFilter.Code, "all", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(payment.CategoryId.ToString(CultureInfo.InvariantCulture), SelectedScheduledPaymentCategoryFilter.Code, StringComparison.OrdinalIgnoreCase);

    private bool MatchesScheduledPaymentsGroupFilter(ScheduledPaymentItemViewModel payment)
    {
        if (ScheduledPaymentGroupFilterOptions.Count == 0 || ScheduledPaymentGroupFilterOptions.All(option => !option.IsSelected))
        {
            return true;
        }

        var selectedGroupIds = ScheduledPaymentGroupFilterOptions
            .Where(option => option.IsSelected)
            .Select(option => option.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return payment.GroupIds.Any(id => selectedGroupIds.Contains(id.ToString()));
    }

    private bool MatchesScheduledPaymentsUserFilter(ScheduledPaymentItemViewModel payment)
    {
        if (ScheduledPaymentUserFilterOptions.Count == 0 || ScheduledPaymentUserFilterOptions.All(option => !option.IsSelected))
        {
            return true;
        }

        var selectedUsers = ScheduledPaymentUserFilterOptions
            .Where(option => option.IsSelected)
            .Select(option => option.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return selectedUsers.Contains(GetScheduledPaymentOwnerFilterCode(payment));
    }

    public ObservableCollection<string> GroupMemberCreateRoles { get; } = ["member", "viewer"];
    public ObservableCollection<string> GroupMemberRoles { get; } = ["owner", "member", "viewer"];
    public IReadOnlyList<CategoryOptionViewModel> GroupMemberCreateRoleOptions => [new("member", Texts.GroupRoleMember), new("viewer", Texts.GroupRoleViewer)];
    public IReadOnlyList<CategoryOptionViewModel> GroupMemberRoleOptions => [new("owner", Texts.GroupRoleOwner), new("member", Texts.GroupRoleMember), new("viewer", Texts.GroupRoleViewer)];
}
