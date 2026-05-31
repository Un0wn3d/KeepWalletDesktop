using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeepWalletDesktop.Models;
using KeepWalletDesktop.Services;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.IO;
using Serilog;
using QuestPDF.Fluent;
using QuestPDF.Helpers;

namespace KeepWalletDesktop.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private const int AllTransactionsPageSize = 30;
    private const int HomeTransactionsPageSize = 10;
    private const string DateDisplayFormat = "dd.MM.yyyy";
    private const string DateTimeDisplayFormat = "dd.MM.yyyy HH:mm";
    private const string AuthPage = "Auth";
    private const string HomePage = "Home";
    private const string BankAccountsPage = "BankAccounts";
    private const string SavingsPage = "Savings";
    private const string GroupsPage = "Groups";
    private const string ScheduledPaymentsPage = "ScheduledPayments";
    private const string CategoriesPage = "Categories";
    private const string AllTransactionsPage = "AllTransactions";
    private const string ReportingPage = "Reporting";
    private const string SettingsPage = "Settings";
    private static readonly string[] ExpenseChartPalette =
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
    private static readonly string[] Theme2ExpenseChartPalette =
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

    private KeepWalletApiClient? _apiClient;
    private IReadOnlyList<ApiCategoryDto> apiCategories = [];
    private IReadOnlySet<int>? selectedCategoryIds;
    private IReadOnlyList<TransactionItemViewModel> homeTransactionSource = [];
    private IReadOnlyList<TransactionItemViewModel> filteredTransactionSource = [];
    private readonly Dictionary<string, decimal> categoryLimits = new(StringComparer.OrdinalIgnoreCase);
    private bool isRefreshingReportingSelections;
    private int filteredTransactionTakeCount = AllTransactionsPageSize;
    private int homeTransactionTakeCount = HomeTransactionsPageSize;
    private DateTimeOffset homeDateRangeFrom = DateTimeOffset.Now.AddMonths(-1);
    private DateTimeOffset homeDateRangeTo = DateTimeOffset.Now;
    private static readonly ILogger ViewModelLog = Log.ForContext<MainViewModel>();
    private DispatcherTimer? transactionFilterDebounceTimer;
    private DispatcherTimer? homeRangeDebounceTimer;
    private DateTimeOffset? pendingHomeRangeFrom;
    private DateTimeOffset? pendingHomeRangeTo;
    private bool isApplyingHomeRange;
    private bool isLoadingTransactions;
    private DateTimeOffset lastTransactionsLoadedAt = DateTimeOffset.MinValue;
    private CancellationTokenSource? transactionsLoadCts;

    [ObservableProperty]
    private ILocalizedText texts = LocalizedText.Load("ua");

    [ObservableProperty]
    private ObservableCollection<LanguageOptionViewModel> languageOptions = [];

    [ObservableProperty]
    private LanguageOptionViewModel? selectedLanguage;

    [ObservableProperty]
    private ObservableCollection<CategoryOptionViewModel> themeOptions = [];

    [ObservableProperty]
    private CategoryOptionViewModel? selectedTheme;

    [ObservableProperty]
    private string currentPage = AuthPage;

    [ObservableProperty]
    private string apiBaseUrl = string.Empty;

    [ObservableProperty]
    private string serverHost = string.Empty;

    [ObservableProperty]
    private string serverPort = string.Empty;

    [ObservableProperty]
    private string apiLogin = string.Empty;

    [ObservableProperty]
    private string apiPassword = string.Empty;

    [ObservableProperty]
    private string registerUsername = string.Empty;

    [ObservableProperty]
    private string registerEmail = string.Empty;

    [ObservableProperty]
    private string registerPassword = string.Empty;

    [ObservableProperty]
    private string registerFullName = string.Empty;

    [ObservableProperty]
    private string apiStatusMessage = string.Empty;

    [ObservableProperty]
    private string currentUsername = string.Empty;

    [ObservableProperty]
    private string currentUserEmail = string.Empty;

    [ObservableProperty]
    private string currentUserRole = string.Empty;

    [ObservableProperty]
    private bool isApiAuthorized;

    [ObservableProperty]
    private bool isApiBusy;

    [ObservableProperty]
    private bool isRegisterMode;

    [ObservableProperty]
    private bool isServerDialogOpen;

    [ObservableProperty]
    private string transactionType = "expense";

    [ObservableProperty]
    private string transactionAmountInput = string.Empty;

    [ObservableProperty]
    private string transactionCurrencyInput = "UAH";

    [ObservableProperty]
    private string transactionDateInput = DateTimeOffset.Now.ToString(DateTimeDisplayFormat);

    [ObservableProperty]
    private DateTimeOffset? transactionSelectedDate = DateTimeOffset.Now;

    [ObservableProperty]
    private DateTime? transactionDate = DateTime.Now;

    [ObservableProperty]
    private int transactionSelectedDay = DateTime.Now.Day;

    [ObservableProperty]
    private int transactionSelectedMonth = DateTime.Now.Month;

    [ObservableProperty]
    private int transactionSelectedYear = DateTime.Now.Year;

    [ObservableProperty]
    private TimeSpan? transactionSelectedTime = DateTime.Now.TimeOfDay;

    [ObservableProperty]
    private string transactionTimeInput = DateTimeOffset.Now.ToString("HH:mm");

    [ObservableProperty]
    private string transactionDescriptionInput = string.Empty;

    [ObservableProperty]
    private int? editingTransactionId;

    [ObservableProperty]
    private int? editingScheduledPaymentId;

    private bool isSyncingTransactionDateState;

    private Guid? defaultAccountId;

    [ObservableProperty]
    private ObservableCollection<CategoryOptionViewModel> transactionCategoryOptions = [];

    [ObservableProperty]
    private CategoryOptionViewModel? selectedTransactionCategory;

    [ObservableProperty]
    private bool isTransactionDialogOpen;

    [ObservableProperty]
    private bool isAllTransactionsDialogOpen;

    [ObservableProperty]
    private bool isCategoryDetailsOpen;

    [ObservableProperty]
    private string reportStatusMessage = string.Empty;

    [ObservableProperty]
    private ObservableCollection<CategoryOptionViewModel> reportViewOptions = [];

    [ObservableProperty]
    private CategoryOptionViewModel? selectedReportView;

    [ObservableProperty]
    private DateTime? reportDateFrom = DateTime.Now.Date;

    [ObservableProperty]
    private DateTime? reportDateTo = DateTime.Now.Date;

    [ObservableProperty]
    private ObservableCollection<CategoryOptionViewModel> reportCategoryOptions = [];

    [ObservableProperty]
    private CategoryOptionViewModel? selectedReportCategory;

    [ObservableProperty]
    private decimal reportSelectedCategoryExpenseTotal;

    [ObservableProperty]
    private int reportSelectedCategoryTransactionCount;

    [ObservableProperty]
    private ObservableCollection<CategoryLegendItemViewModel> reportMonthlyExpenses = [];

    [ObservableProperty]
    private string transactionSearchText = string.Empty;

    [ObservableProperty]
    private string selectedTransactionTypeFilter = "all";

    [ObservableProperty]
    private BankAccountFilterOptionViewModel? selectedTransactionAccountFilter;

    [ObservableProperty]
    private ObservableCollection<BankAccountFilterOptionViewModel> transactionAccountFilterOptions = [];

    [ObservableProperty]
    private CategoryOptionViewModel? selectedTransactionCategoryFilter;

    [ObservableProperty]
    private ObservableCollection<CategoryOptionViewModel> transactionCategoryFilterOptions = [];

    [ObservableProperty]
    private ObservableCollection<TransactionItemViewModel> visibleTransactions = [];

    [ObservableProperty]
    private ObservableCollection<TransactionItemViewModel> allTransactions = [];

    [ObservableProperty]
    private ObservableCollection<TransactionItemViewModel> filteredTransactions = [];

    [ObservableProperty]
    private ObservableCollection<TransactionItemViewModel> savingTransactions = [];

    [ObservableProperty]
    private TransactionItemViewModel? activeTransaction;

    [ObservableProperty]
    private double goalAmount;

    [ObservableProperty]
    private double savedAmount;

    [ObservableProperty]
    private double addAmount;

    [ObservableProperty]
    private double availableBalance;

    [ObservableProperty]
    private int? activeSavingId;

    [ObservableProperty]
    private string savingNameInput = "Savings";

    [ObservableProperty]
    private DateOnly? activeSavingDeadline;

    [ObservableProperty]
    private bool isEditingSavingDetails;

    [ObservableProperty]
    private string savingNameEditInput = string.Empty;

    [ObservableProperty]
    private string savingGoalAmountEditInput = "0";

    [ObservableProperty]
    private string savingCurrentAmountEditInput = "0";

    [ObservableProperty]
    private string savingCurrencyEditInput = "UAH";

    [ObservableProperty]
    private DateTime? savingDeadlineEditDate;

    [ObservableProperty]
    private bool isEditingSavingNameField;

    [ObservableProperty]
    private bool isEditingSavingGoalField;

    [ObservableProperty]
    private bool isEditingSavingCurrentField;

    [ObservableProperty]
    private bool isEditingSavingCurrencyField;

    [ObservableProperty]
    private bool isEditingSavingDeadlineField;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveSavingIconGlyph))]
    private string activeSavingIconKey = "other";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveSavingColorBrush))]
    private string activeSavingColor = "#3B82F6";

    [ObservableProperty]
    private string activeSavingCurrency = "UAH";

    [ObservableProperty]
    private double interestRate;

    [ObservableProperty]
    private string savingItemNameInput = string.Empty;

    [ObservableProperty]
    private double savingItemAmount;

    [ObservableProperty]
    private bool isSavingsDialogOpen;

    [ObservableProperty]
    private string savingsDialogNameInput = string.Empty;

    [ObservableProperty]
    private string savingsDialogTargetAmountInput = string.Empty;

    [ObservableProperty]
    private string savingsDialogInitialAmountInput = string.Empty;

    [ObservableProperty]
    private string savingsDialogDeadlineInput = string.Empty;

    [ObservableProperty]
    private string savingsDialogCurrency = "UAH";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SavingsDialogIconGlyph))]
    private string savingsDialogIconKey = "other";

    [ObservableProperty]
    private string savingsDialogColor = "#3B82F6";

    [ObservableProperty]
    private BankAccountItemViewModel? selectedSavingsBankAccount;

    [ObservableProperty]
    private ObservableCollection<SavingOptionViewModel> savings = [];

    [ObservableProperty]
    private ObservableCollection<SavingRateHistoryItemViewModel> savingRateHistory = [];

    [ObservableProperty]
    private ObservableCollection<SavingItemViewModel> savingItems = [];

    [ObservableProperty]
    private string savingsStatusMessage = string.Empty;

    [ObservableProperty]
    private ObservableCollection<TransactionItemViewModel> latestExpenses = [];

    [ObservableProperty]
    private ObservableCollection<BankAccountItemViewModel> bankAccounts = [];

    [ObservableProperty]
    private BankAccountItemViewModel? selectedBankAccount;

    [ObservableProperty]
    private ObservableCollection<GroupItemViewModel> groups = [];

    [ObservableProperty]
    private string newGroupNameInput = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NewGroupIconGlyph))]
    [NotifyPropertyChangedFor(nameof(NewGroupIconSampleImage))]
    [NotifyPropertyChangedFor(nameof(HasNewGroupIconSampleImage))]
    private string newGroupIconKey = "other";

    [ObservableProperty]
    private GroupItemViewModel? selectedAccessGroup;

    [ObservableProperty]
    private string selectedGroupNameInput = string.Empty;

    [ObservableProperty]
    private bool isEditingSelectedGroupName;

    [ObservableProperty]
    private bool isTransactionSharedInput;

    [ObservableProperty]
    private GroupItemViewModel? selectedTransactionShareGroup;

    [ObservableProperty]
    private string groupMemberLoginInput = string.Empty;

    [ObservableProperty]
    private ObservableCollection<UserListItemViewModel> groupUserSearchResults = [];

    [ObservableProperty]
    private UserListItemViewModel? selectedGroupUser;

    [ObservableProperty]
    private ObservableCollection<GroupMemberItemViewModel> selectedGroupMembers = [];

    [ObservableProperty]
    private string selectedGroupMemberRole = "member";

    [ObservableProperty]
    private BankAccountItemViewModel? selectedSharedBankAccount;

    [ObservableProperty]
    private SavingOptionViewModel? selectedSharedSaving;

    [ObservableProperty]
    private ObservableCollection<UserListItemViewModel> adminUsers = [];

    [ObservableProperty]
    private ObservableCollection<AuditLogItemViewModel> userLogs = [];

    [ObservableProperty]
    private bool isBankAccountDialogOpen;

    [ObservableProperty]
    private Guid? editingBankAccountId;

    [ObservableProperty]
    private string bankAccountNameInput = string.Empty;

    [ObservableProperty]
    private string bankAccountCurrencyInput = "UAH";

    [ObservableProperty]
    private string bankAccountBalanceInput = "0";

    [ObservableProperty]
    private bool isBankAccountDefaultInput;

    [ObservableProperty]
    private bool isBankAccountSharedInput;

    [ObservableProperty]
    private GroupItemViewModel? selectedBankAccountShareGroup;

    [ObservableProperty]
    private string bankAccountStatusMessage = string.Empty;

    [ObservableProperty]
    private bool isMissingBankAccountPromptOpen;

    [ObservableProperty]
    private string missingBankAccountPromptMessage = string.Empty;

    [ObservableProperty]
    private bool isDeleteCategoryPromptOpen;

    [ObservableProperty]
    private string deleteCategoryPromptMessage = string.Empty;

    private CategoryManagementItemViewModel? pendingDeleteCategory;

    [ObservableProperty]
    private ObservableCollection<ScheduledPaymentItemViewModel> scheduledPayments = [];

    [ObservableProperty]
    private bool isScheduledPaymentConfirmDialogOpen;

    [ObservableProperty]
    private ScheduledPaymentItemViewModel? pendingScheduledPaymentConfirmation;

    [ObservableProperty]
    private ObservableCollection<BankAccountItemViewModel> scheduledPaymentConfirmAccounts = [];

    [ObservableProperty]
    private BankAccountItemViewModel? selectedScheduledPaymentConfirmAccount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScheduledPaymentConfirmTitle))]
    private string scheduledPaymentConfirmCurrency = string.Empty;

    [ObservableProperty]
    private string scheduledPaymentNameInput = string.Empty;

    [ObservableProperty]
    private string scheduledPaymentAmountInput = string.Empty;

    [ObservableProperty]
    private string scheduledPaymentDateInput = DateTimeOffset.Now.ToString(DateTimeDisplayFormat);

    [ObservableProperty]
    private string scheduledPaymentFrequencyInput = "once";

    [ObservableProperty]
    private ObservableCollection<CategoryOptionViewModel> scheduledRepeatOptions = [];

    [ObservableProperty]
    private CategoryOptionViewModel? selectedScheduledRepeatOption;

    [ObservableProperty]
    private ObservableCollection<CategoryOptionViewModel> scheduledCustomRepeatUnitOptions = [];

    [ObservableProperty]
    private CategoryOptionViewModel? selectedScheduledCustomPrimaryUnit;

    [ObservableProperty]
    private CategoryOptionViewModel? selectedScheduledCustomSecondaryUnit;

    [ObservableProperty]
    private CategoryOptionViewModel? selectedScheduledCustomTertiaryUnit;

    [ObservableProperty]
    private CategoryOptionViewModel? selectedScheduledCustomQuaternaryUnit;

    [ObservableProperty]
    private string scheduledCustomPrimaryValueInput = "1";

    [ObservableProperty]
    private string scheduledCustomSecondaryValueInput = string.Empty;

    [ObservableProperty]
    private string scheduledCustomTertiaryValueInput = string.Empty;

    [ObservableProperty]
    private string scheduledCustomQuaternaryValueInput = string.Empty;

    [ObservableProperty]
    private string scheduledPaymentsFilter = "all";

    [ObservableProperty]
    private ObservableCollection<CategoryOptionViewModel> scheduledPaymentCurrencyFilterOptions = [];

    [ObservableProperty]
    private CategoryOptionViewModel? selectedScheduledPaymentCurrencyFilter;

    [ObservableProperty]
    private ObservableCollection<CategoryOptionViewModel> scheduledPaymentCategoryFilterOptions = [];

    [ObservableProperty]
    private CategoryOptionViewModel? selectedScheduledPaymentCategoryFilter;

    [ObservableProperty]
    private ObservableCollection<CategoryOptionViewModel> scheduledPaymentTypeFilterOptions = [];

    [ObservableProperty]
    private CategoryOptionViewModel? selectedScheduledPaymentTypeFilter;

    public bool IsCustomScheduledRepeat => string.Equals(SelectedScheduledRepeatOption?.Code, "custom", StringComparison.OrdinalIgnoreCase);
    public IEnumerable<CategoryOptionViewModel> ScheduledCustomSecondaryUnitOptions => BuildScheduledCustomRepeatUnitOptions(SelectedScheduledCustomPrimaryUnit?.Code);
    public IEnumerable<CategoryOptionViewModel> ScheduledCustomTertiaryUnitOptions => BuildScheduledCustomRepeatUnitOptions(SelectedScheduledCustomSecondaryUnit?.Code);
    public IEnumerable<CategoryOptionViewModel> ScheduledCustomQuaternaryUnitOptions => BuildScheduledCustomRepeatUnitOptions(SelectedScheduledCustomTertiaryUnit?.Code);
    public bool HasScheduledCustomSecondaryUnit => IsCustomScheduledRepeat && !string.Equals(SelectedScheduledCustomPrimaryUnit?.Code, "day", StringComparison.OrdinalIgnoreCase);
    public bool HasScheduledCustomTertiaryUnit => HasScheduledCustomSecondaryUnit && !string.Equals(SelectedScheduledCustomSecondaryUnit?.Code, "day", StringComparison.OrdinalIgnoreCase);
    public bool HasScheduledCustomQuaternaryUnit => HasScheduledCustomTertiaryUnit && !string.Equals(SelectedScheduledCustomTertiaryUnit?.Code, "day", StringComparison.OrdinalIgnoreCase);
    public int ScheduledPaymentsTotalCount => ScheduledPayments.Count;
    public int ScheduledPaymentsPaidThisMonthCount => AllTransactions.Count(IsPaidScheduledPaymentThisMonth);
    public int ScheduledPaymentsUnpaidCount => ScheduledPayments.Count(p =>
        p.IsActive && p.DueDate.ToLocalTime().Date >= DateTime.Today);
    public int ScheduledPaymentsOverdueCount => ScheduledPayments.Count(p =>
        p.IsActive && p.DueDate.ToLocalTime().Date < DateTime.Today);
    public int ScheduledPaymentsUpcomingCount => ScheduledPayments.Count(p =>
        p.IsActive && p.DueDate.ToLocalTime().Date >= DateTime.Today);
    public string ScheduledPaymentConfirmTitle => $"{Texts.ConfirmScheduledPaymentButton} · {ScheduledPaymentConfirmCurrency}";
    public IEnumerable<ScheduledPaymentItemViewModel> FilteredScheduledPayments => ScheduledPayments
        .Where(MatchesScheduledPaymentsFilter)
        .Where(MatchesScheduledPaymentsTypeFilter)
        .Where(MatchesScheduledPaymentsCurrencyFilter)
        .Where(MatchesScheduledPaymentsCategoryFilter)
        .OrderBy(p => p.DueDate);
    public bool IsAllScheduledPaymentsFilterSelected => string.Equals(ScheduledPaymentsFilter, "all", StringComparison.OrdinalIgnoreCase);
    public bool IsFutureScheduledPaymentsFilterSelected => string.Equals(ScheduledPaymentsFilter, "future", StringComparison.OrdinalIgnoreCase);
    public bool IsOverdueScheduledPaymentsFilterSelected => string.Equals(ScheduledPaymentsFilter, "overdue", StringComparison.OrdinalIgnoreCase);
    public bool HasScheduledPaymentTypeFilter => ScheduledPaymentTypeFilterOptions.Count > 2;

    private bool MatchesScheduledPaymentsFilter(ScheduledPaymentItemViewModel payment) =>
        ScheduledPaymentsFilter switch
        {
            "future" => payment.IsActive && payment.DueDate.ToLocalTime().Date >= DateTime.Today,
            "overdue" => payment.IsActive && payment.DueDate.ToLocalTime().Date < DateTime.Today,
            _ => true
        };

    private bool MatchesScheduledPaymentsCurrencyFilter(ScheduledPaymentItemViewModel payment) =>
        SelectedScheduledPaymentCurrencyFilter is null ||
        SelectedScheduledPaymentCurrencyFilter.Code == "all" ||
        string.Equals(payment.Currency, SelectedScheduledPaymentCurrencyFilter.Code, StringComparison.OrdinalIgnoreCase);

    private bool MatchesScheduledPaymentsTypeFilter(ScheduledPaymentItemViewModel payment) =>
        SelectedScheduledPaymentTypeFilter is null ||
        SelectedScheduledPaymentTypeFilter.Code == "all" ||
        string.Equals(payment.Type, SelectedScheduledPaymentTypeFilter.Code, StringComparison.OrdinalIgnoreCase);

    private bool MatchesScheduledPaymentsCategoryFilter(ScheduledPaymentItemViewModel payment) =>
        SelectedScheduledPaymentCategoryFilter is null ||
        SelectedScheduledPaymentCategoryFilter.Code == "all" ||
        payment.CategoryId.ToString(CultureInfo.InvariantCulture) == SelectedScheduledPaymentCategoryFilter.Code;


    [ObservableProperty]
    private string scheduledPaymentType = "expense";

    [ObservableProperty]
    private string scheduledPaymentDescriptionInput = string.Empty;

    [ObservableProperty]
    private ObservableCollection<CategoryOptionViewModel> scheduledPaymentCategoryOptions = [];

    [ObservableProperty]
    private CategoryOptionViewModel? selectedScheduledPaymentCategory;

    [ObservableProperty]
    private bool isOnboardingDialogOpen;

    [ObservableProperty]
    private bool isCategorySettingsMode;

    [ObservableProperty]
    private string onboardingAccountName = "Main";

    [ObservableProperty]
    private string onboardingSelectedCurrency = "UAH";

    [ObservableProperty]
    private ObservableCollection<string> onboardingCurrencyOptions = ["UAH", "USD", "EUR", "PLN", "SEK"];

    [ObservableProperty]
    private ObservableCollection<OnboardingCategoryOptionViewModel> onboardingCategories = [];

    [ObservableProperty]
    private string onboardingNewCategoryName = string.Empty;

    [ObservableProperty]
    private string onboardingNewCategoryType = "expense";

    [ObservableProperty]
    private string onboardingStatusMessage = string.Empty;

    [ObservableProperty]
    private ObservableCollection<CategoryManagementItemViewModel> categories = [];

    [ObservableProperty]
    private string categoryNewName = string.Empty;

    [ObservableProperty]
    private string categoryNewType = "expense";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CategoryNewIconGlyph))]
    [NotifyPropertyChangedFor(nameof(CategoryNewIconSampleImage))]
    [NotifyPropertyChangedFor(nameof(HasCategoryNewIconSampleImage))]
    private string categoryNewIconKey = "other";

    [ObservableProperty]
    private string categoryNewColor = "#E5E7EB";

    [ObservableProperty]
    private CategoryManagementItemViewModel? selectedMergeSourceCategory;

    [ObservableProperty]
    private CategoryManagementItemViewModel? selectedMergeTargetCategory;

    [ObservableProperty]
    private string categoryStatusMessage = string.Empty;

    public MainViewModel()
    {
        var apiOptions = AppConfiguration.GetApiOptions();
        var userSettings = UserSettingsStore.Load();
        ApiBaseUrl = string.IsNullOrWhiteSpace(userSettings.ApiBaseUrl)
            ? apiOptions.BaseUrl
            : userSettings.ApiBaseUrl;
        ApiLogin = userSettings.LastLogin;
        InitializeLanguages();
        InitializeThemes(userSettings.ThemeName);
        InitializeScheduledRepeatOptions();
        InitializeScheduledCustomRepeatOptions();
        InitializeReportingOptions();
        LoadServerSettingsFromBaseUrl();
        ResetAuthorizedUser();
        LoadApiOnlyPlaceholders();
    }

    public void StartBackgroundInitialization()
    {
        _ = EnsureEmojiCatalogIfMissingAsync();
    }

    private async Task EnsureEmojiCatalogIfMissingAsync()
    {
        if (CategoryIconCatalog.HasCachedCatalog())
        {
            return;
        }

        await Task.Delay(1500);
        await CategoryIconCatalog.PreloadAsync();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            OnPropertyChanged(nameof(CategoryIconOptions));
            OnPropertyChanged(nameof(CategoryNewIconGlyph));
            OnPropertyChanged(nameof(CategoryNewIconSampleImage));
            OnPropertyChanged(nameof(HasCategoryNewIconSampleImage));
            OnPropertyChanged(nameof(NewGroupIconGlyph));
            OnPropertyChanged(nameof(NewGroupIconSampleImage));
            OnPropertyChanged(nameof(HasNewGroupIconSampleImage));
        });
    }

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
    public bool IsCurrentUserAdmin => string.Equals(CurrentUserRole, "admin", StringComparison.OrdinalIgnoreCase);
    public string ApiConnectionStatus => IsApiAuthorized ? Texts.ConnectedStatus : Texts.DisconnectedStatus;
    public bool CanOpenAppPages => IsApiAuthorized;
    public string Income => FormatTransactionTotals(LatestExpenses.Where(t => t.IsIncome));
    public string Expense => FormatTransactionTotals(LatestExpenses.Where(t => !t.IsIncome));
    public string Money => FormatTransactionBalance();
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
    public bool HasTransactions => LatestExpenses.Count > 0;
    public bool HasNoTransactions => !HasTransactions;
    public bool HasSavingTransactions => SavingTransactions.Count > 0;
    public bool HasNoSavingTransactions => !HasSavingTransactions;
    public IEnumerable<TransactionItemViewModel> SharedTransactions => AllTransactions.Where(t => t.IsShared).Take(5);
    public bool HasSharedTransactions => AllTransactions.Any(t => t.IsShared);
    public bool HasAllTransactions => LatestExpenses.Count > 0;
    public IEnumerable<BankAccountItemViewModel> SelectedGroupBankAccounts =>
        SelectedAccessGroup is null ? [] : BankAccounts.Where(a => a.GroupId == SelectedAccessGroup.Id);
    public IEnumerable<TransactionItemViewModel> SelectedGroupTransactions =>
        SelectedAccessGroup is null ? [] : AllTransactions.Where(t => t.GroupId == SelectedAccessGroup.Id);
    public IEnumerable<SavingOptionViewModel> SelectedGroupSavings =>
        SelectedAccessGroup is null ? [] : Savings.Where(s => s.GroupId == SelectedAccessGroup.Id);
    public double TransactionCategoryPillWidth
    {
        get
        {
            var longest = FilteredTransactions.Concat(VisibleTransactions)
                .Select(t => t.CategoryDisplay?.Length ?? 0)
                .DefaultIfEmpty(12)
                .Max();
            return Math.Clamp((longest * 8) + 28, 100, 180);
        }
    }
    public double ScheduledPaymentCategoryPillWidth
    {
        get
        {
            var longest = ScheduledPayments
                .Select(t => t.CategoryDisplay?.Length ?? 0)
                .DefaultIfEmpty(12)
                .Max();
            return Math.Clamp((longest * 8) + 28, 120, 260);
        }
    }
    public bool HasExpenseTransactions => LatestExpenses.Any(t => !t.IsIncome);
    public bool HasNoExpenseTransactions => !HasExpenseTransactions;
    public double RemainingAmount => Math.Max(0, GoalAmount - SavedAmount);
    public string ApiUserDisplay => $"{Texts.ApiUserPrefix}: {CurrentUsername}";
    public string StatusDisplay => $"{Texts.StatusLabel}: {ApiConnectionStatus}";
    public string UserDisplay => $"{Texts.UserLabel}: {CurrentUsername}";
    public string EmailDisplay => $"{Texts.EmailLabel}: {CurrentUserEmail}";
    public string RoleDisplay => $"{Texts.RoleLabel}: {CurrentUserRole}";
    public string AdminUsersCountDisplay => $"{Texts.UsersTitle}: {AdminUsers.Count}";
    public string SavedAmountDisplay => $"{SavedAmount:N2}";
    public string RemainingAmountDisplay => $"{Texts.RemainingAmountPrefix}: {RemainingAmount:N2} {ActiveSavingCurrency}";
    public string ActiveSavingAmountGoalDisplay => $"{SavedAmount:N2}/{GoalAmount:N2} {ActiveSavingCurrency}";
    public string ActiveSavingDeadlineDisplay => BuildSavingDeadlineDisplay(ActiveSavingDeadline);
    public decimal ReportIncomeTotal => AllTransactions.Where(t => t.IsIncome).Sum(t => t.Amount);
    public decimal ReportExpenseTotal => AllTransactions.Where(t => !t.IsIncome).Sum(t => t.Amount);
    public decimal ReportBalanceTotal => ReportIncomeTotal - ReportExpenseTotal;
    public bool IsCategoryPeriodReportView => string.Equals(SelectedReportView?.Code, "category_period", StringComparison.OrdinalIgnoreCase);
    public bool IsMonthlyPlanReportView => string.Equals(SelectedReportView?.Code, "monthly_plan", StringComparison.OrdinalIgnoreCase);
    public bool IsMonthlyExpensesReportView => string.Equals(SelectedReportView?.Code, "monthly_expenses", StringComparison.OrdinalIgnoreCase);
    public string AuthCardTitle => IsRegisterMode ? Texts.RegisterTitle : Texts.LoginTitle;
    public bool IsLoginMode => !IsRegisterMode;
    public bool IsOnboardingAccountSetupVisible => !IsCategorySettingsMode;
    public string AuthFooterPrompt => IsRegisterMode ? Texts.AlreadyRegisteredPrompt : Texts.NotRegisteredPrompt;
    public string AuthFooterAction => IsRegisterMode ? Texts.SwitchToLoginLink : Texts.SwitchToRegisterLink;
    public bool IsEditingScheduledPayment => EditingScheduledPaymentId.HasValue;
    public bool IsEditingTransaction => EditingTransactionId.HasValue && !IsEditingScheduledPayment;
    public bool IsExpenseTransactionTypeSelected => string.Equals(TransactionType, "expense", StringComparison.OrdinalIgnoreCase);
    public bool IsIncomeTransactionTypeSelected => string.Equals(TransactionType, "income", StringComparison.OrdinalIgnoreCase);
    public bool IsPlannedTransactionMode =>
        IsEditingScheduledPayment ||
        (!IsEditingTransaction &&
         TryBuildTransactionDateTime(TransactionSelectedDate, TransactionSelectedTime, out var transactionDate) &&
         transactionDate.Date > DateTimeOffset.Now.Date);
    public string TransactionDialogTitle => IsEditingScheduledPayment
        ? Texts.NewPlannedTransactionTitle
        : IsEditingTransaction
        ? Texts.TransactionDetailsTitle
        : IsPlannedTransactionMode
            ? Texts.NewPlannedTransactionTitle
            : Texts.NewTransactionButton;
    public string SaveTransactionButtonText => IsEditingTransaction || IsEditingScheduledPayment ? Texts.UpdateTransactionButton : Texts.SaveTransactionButton;
    public bool CanConfirmScheduledPaymentFromDialog => IsEditingScheduledPayment;
    public string TransactionTypeDisplay => TransactionType == "income" ? Texts.TransactionIncomeType : Texts.TransactionExpenseType;
    public string TransactionTimelineTitle => Texts.TransactionTimelineTitle;

    public double ProgressPercent
    {
        get
        {
            if (GoalAmount <= 0)
            {
                return 0;
            }

            return Math.Min(100, SavedAmount / GoalAmount * 100);
        }
    }

    public ISeries[] Series { get; private set; } = [];
    public ISeries[] Chart { get; private set; } = [];
    public Axis[] TransactionTimelineXAxes { get; private set; } = [];
    public Axis[] TransactionTimelineYAxes { get; private set; } = [];
    public ObservableCollection<CategoryLegendItemViewModel> ExpenseCategoryLegend { get; private set; } = [];
    public ObservableCollection<CategoryDetailItemViewModel> CategoryDetails { get; private set; } = [];
    public ObservableCollection<string> GroupMemberCreateRoles { get; } = ["member", "viewer"];
    public ObservableCollection<string> GroupMemberRoles { get; } = ["owner", "member", "viewer"];
    public ObservableCollection<string> TransactionTypeFilterOptions { get; } = ["all", "income", "expense"];
    public ObservableCollection<int> TransactionDayOptions { get; } = new(Enumerable.Range(1, 31));
    public ObservableCollection<int> TransactionMonthOptions { get; } = new(Enumerable.Range(1, 12));
    public ObservableCollection<int> TransactionYearOptions { get; } = new(Enumerable.Range(2000, 151));
    public IEnumerable<OnboardingCategoryOptionViewModel> OnboardingExpenseCategories =>
        OnboardingCategories.Where(c => string.Equals(c.Type, "expense", StringComparison.OrdinalIgnoreCase));
    public IEnumerable<OnboardingCategoryOptionViewModel> OnboardingIncomeCategories =>
        OnboardingCategories.Where(c => string.Equals(c.Type, "income", StringComparison.OrdinalIgnoreCase));
    public IEnumerable<CategoryManagementItemViewModel> ExpenseCategories =>
        Categories.Where(c => string.Equals(c.Type, "expense", StringComparison.OrdinalIgnoreCase));
    public IEnumerable<CategoryManagementItemViewModel> IncomeCategories =>
        Categories.Where(c => string.Equals(c.Type, "income", StringComparison.OrdinalIgnoreCase));
    public IEnumerable<CategoryManagementItemViewModel> MergeTargetCategories =>
        Categories.Where(c => SelectedMergeSourceCategory is null ||
            (c.Id != SelectedMergeSourceCategory.Id &&
             string.Equals(c.Type, SelectedMergeSourceCategory.Type, StringComparison.OrdinalIgnoreCase)));
    public IReadOnlyList<CategoryIconOptionViewModel> CategoryIconOptions => CategoryIconCatalog.Options;
    public IReadOnlyList<string> CategoryColorOptions => SavingColorOptions;
    public IReadOnlyList<string> SavingColorOptions { get; } =
    [
        "#EF4444", "#F97316", "#F59E0B", "#EAB308",
        "#84CC16", "#22C55E", "#10B981", "#14B8A6",
        "#06B6D4", "#0EA5E9", "#3B82F6", "#6366F1",
        "#8B5CF6", "#EC4899", "#6B7280", "#111827"
    ];
    public string CategoryNewIconGlyph => CategoryIconCatalog.GetGlyph(CategoryNewIconKey, CategoryNewType);
    public Bitmap? CategoryNewIconSampleImage => CategoryIconCatalog.GetSampleImage(CategoryNewIconKey);
    public bool HasCategoryNewIconSampleImage => CategoryNewIconSampleImage is not null;
    public bool IsNewCategoryExpenseTypeSelected => string.Equals(CategoryNewType, "expense", StringComparison.OrdinalIgnoreCase);
    public bool IsNewCategoryIncomeTypeSelected => string.Equals(CategoryNewType, "income", StringComparison.OrdinalIgnoreCase);
    public string SavingsDialogIconGlyph => CategoryIconCatalog.GetGlyph(SavingsDialogIconKey, "expense");
    public string ActiveSavingIconGlyph => CategoryIconCatalog.GetGlyph(ActiveSavingIconKey, "expense");
    public IBrush ActiveSavingColorBrush => new SolidColorBrush(Color.Parse(ActiveSavingColor));
    public IEnumerable<BankAccountItemViewModel> CompatibleSavingsBankAccounts =>
        BankAccounts.Where(a => string.Equals(a.Currency, ActiveSavingCurrency, StringComparison.OrdinalIgnoreCase));
    public string NewGroupIconGlyph => CategoryIconCatalog.GetGlyph(NewGroupIconKey, "expense");
    public Bitmap? NewGroupIconSampleImage => CategoryIconCatalog.GetSampleImage(NewGroupIconKey);
    public bool HasNewGroupIconSampleImage => NewGroupIconSampleImage is not null;

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

        // Prevent UI storm: identical range should not trigger full recalculation.
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

    partial void OnCurrentPageChanged(string value)
    {
        OnPropertyChanged(nameof(IsAuthPage));
        OnPropertyChanged(nameof(IsHomePage));
        OnPropertyChanged(nameof(IsBankAccountsPage));
        OnPropertyChanged(nameof(IsSavingsPage));
        OnPropertyChanged(nameof(IsGroupsPage));
        OnPropertyChanged(nameof(IsScheduledPaymentsPage));
        OnPropertyChanged(nameof(IsCategoriesPage));
        OnPropertyChanged(nameof(IsAllTransactionsPage));
        OnPropertyChanged(nameof(IsReportingPage));
        OnPropertyChanged(nameof(IsSettingsPage));
    }

    partial void OnSelectedBankAccountChanged(BankAccountItemViewModel? value)
    {
        if (value is not null)
        {
            defaultAccountId = value.Id;
            TransactionCurrencyInput = value.Currency;
        }

        OnPropertyChanged(nameof(SelectedBankAccountTitle));
        OnPropertyChanged(nameof(SelectedBankAccountBalance));
        OnPropertyChanged(nameof(SelectedBankAccountCurrency));
    }

    partial void OnBankAccountsChanged(ObservableCollection<BankAccountItemViewModel> value)
    {
        OnPropertyChanged(nameof(HasBankAccounts));
        OnPropertyChanged(nameof(HasNoBankAccounts));
        OnPropertyChanged(nameof(SelectedGroupBankAccounts));
        OnPropertyChanged(nameof(CompatibleSavingsBankAccounts));
        UpdateTransactionAccountFilterOptions();
        SelectedSavingsBankAccount ??= value.FirstOrDefault(a => a.IsDefault) ?? value.FirstOrDefault();
    }

    partial void OnActiveSavingCurrencyChanged(string value)
    {
        OnPropertyChanged(nameof(ActiveSavingAmountGoalDisplay));
        OnPropertyChanged(nameof(CompatibleSavingsBankAccounts));
        SelectedSavingsBankAccount = CompatibleSavingsBankAccounts.FirstOrDefault();
    }

    partial void OnAllTransactionsChanged(ObservableCollection<TransactionItemViewModel> value)
    {
        OnPropertyChanged(nameof(SelectedGroupTransactions));
        OnPropertyChanged(nameof(TransactionCategoryPillWidth));
        OnPropertyChanged(nameof(ScheduledPaymentsPaidThisMonthCount));
        RefreshSavingTransactions();
        RefreshReportingSelections();
    }

    partial void OnSavingTransactionsChanged(ObservableCollection<TransactionItemViewModel> value)
    {
        OnPropertyChanged(nameof(HasSavingTransactions));
        OnPropertyChanged(nameof(HasNoSavingTransactions));
    }

    partial void OnSavingsChanged(ObservableCollection<SavingOptionViewModel> value)
    {
        OnPropertyChanged(nameof(SelectedGroupSavings));
        OnPropertyChanged(nameof(HasSavings));
        OnPropertyChanged(nameof(HasNoSavings));
    }

    partial void OnVisibleTransactionsChanged(ObservableCollection<TransactionItemViewModel> value)
    {
        OnPropertyChanged(nameof(TransactionCategoryPillWidth));
    }

    partial void OnFilteredTransactionsChanged(ObservableCollection<TransactionItemViewModel> value)
    {
        OnPropertyChanged(nameof(TransactionCategoryPillWidth));
    }

    partial void OnEditingBankAccountIdChanged(Guid? value)
    {
        OnPropertyChanged(nameof(IsEditingBankAccount));
        OnPropertyChanged(nameof(BankAccountDialogTitle));
        OnPropertyChanged(nameof(SaveBankAccountButtonText));
    }

    partial void OnIsApiAuthorizedChanged(bool value)
    {
        OnPropertyChanged(nameof(ApiConnectionStatus));
        OnPropertyChanged(nameof(CanOpenAppPages));
        OnPropertyChanged(nameof(StatusDisplay));
        OnPropertyChanged(nameof(UserDisplay));
        OnPropertyChanged(nameof(EmailDisplay));
        OnPropertyChanged(nameof(RoleDisplay));
        OnPropertyChanged(nameof(IsCurrentUserAdmin));
        OnPropertyChanged(nameof(HasTransactions));
        OnPropertyChanged(nameof(HasNoTransactions));
        OnPropertyChanged(nameof(SharedTransactions));
        OnPropertyChanged(nameof(HasSharedTransactions));
        OnPropertyChanged(nameof(HasAllTransactions));
        OnPropertyChanged(nameof(HasExpenseTransactions));
        OnPropertyChanged(nameof(HasNoExpenseTransactions));
    }

    partial void OnIsRegisterModeChanged(bool value)
    {
        OnPropertyChanged(nameof(IsLoginMode));
        OnPropertyChanged(nameof(AuthCardTitle));
        OnPropertyChanged(nameof(AuthFooterPrompt));
        OnPropertyChanged(nameof(AuthFooterAction));
    }

    partial void OnIsCategorySettingsModeChanged(bool value)
    {
        OnPropertyChanged(nameof(IsOnboardingAccountSetupVisible));
    }

    partial void OnCategoryNewTypeChanged(string value)
    {
        OnPropertyChanged(nameof(CategoryNewIconGlyph));
        OnPropertyChanged(nameof(CategoryNewIconSampleImage));
        OnPropertyChanged(nameof(HasCategoryNewIconSampleImage));
        OnPropertyChanged(nameof(IsNewCategoryExpenseTypeSelected));
        OnPropertyChanged(nameof(IsNewCategoryIncomeTypeSelected));
    }

    partial void OnEditingTransactionIdChanged(int? value)
    {
        OnPropertyChanged(nameof(IsEditingTransaction));
        OnPropertyChanged(nameof(IsEditingScheduledPayment));
        OnPropertyChanged(nameof(IsPlannedTransactionMode));
        OnPropertyChanged(nameof(TransactionDialogTitle));
        OnPropertyChanged(nameof(SaveTransactionButtonText));
    }

    partial void OnEditingScheduledPaymentIdChanged(int? value)
    {
        OnPropertyChanged(nameof(IsEditingTransaction));
        OnPropertyChanged(nameof(IsEditingScheduledPayment));
        OnPropertyChanged(nameof(IsPlannedTransactionMode));
        OnPropertyChanged(nameof(TransactionDialogTitle));
        OnPropertyChanged(nameof(SaveTransactionButtonText));
    }

    partial void OnGoalAmountChanged(double value)
    {
        RefreshSavingsProperties();
    }

    partial void OnSavedAmountChanged(double value)
    {
        RefreshSavingsProperties();
    }

    partial void OnTransactionTypeChanged(string value)
    {
        UpdateTransactionCategoryOptions(SelectedTransactionCategory?.Code);
        OnPropertyChanged(nameof(TransactionTypeDisplay));
        OnPropertyChanged(nameof(IsExpenseTransactionTypeSelected));
        OnPropertyChanged(nameof(IsIncomeTransactionTypeSelected));
    }

    partial void OnTransactionSearchTextChanged(string value)
    {
        ScheduleTransactionFilters();
    }

    partial void OnSelectedTransactionTypeFilterChanged(string value)
    {
        UpdateTransactionCategoryFilterOptions();
        ScheduleTransactionFilters();
    }

    partial void OnSelectedTransactionAccountFilterChanged(BankAccountFilterOptionViewModel? value)
    {
        ScheduleTransactionFilters();
    }

    partial void OnSelectedTransactionCategoryFilterChanged(CategoryOptionViewModel? value)
    {
        ScheduleTransactionFilters();
    }

    private void ScheduleTransactionFilters()
    {
        transactionFilterDebounceTimer ??= new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(150)
        };

        transactionFilterDebounceTimer.Stop();
        transactionFilterDebounceTimer.Tick -= TransactionFilterDebounceTimerOnTick;
        transactionFilterDebounceTimer.Tick += TransactionFilterDebounceTimerOnTick;
        transactionFilterDebounceTimer.Start();
    }

    private void TransactionFilterDebounceTimerOnTick(object? sender, EventArgs e)
    {
        if (transactionFilterDebounceTimer is null)
        {
            return;
        }

        transactionFilterDebounceTimer.Stop();
        transactionFilterDebounceTimer.Tick -= TransactionFilterDebounceTimerOnTick;
        ApplyTransactionFilters();
    }

    partial void OnSelectedReportViewChanged(CategoryOptionViewModel? value)
    {
        OnPropertyChanged(nameof(IsCategoryPeriodReportView));
        OnPropertyChanged(nameof(IsMonthlyPlanReportView));
        OnPropertyChanged(nameof(IsMonthlyExpensesReportView));
    }

    partial void OnSelectedReportCategoryChanged(CategoryOptionViewModel? value) => RefreshReportingMetricsOnly();
    partial void OnReportDateFromChanged(DateTime? value) => RefreshReportingMetricsOnly();
    partial void OnReportDateToChanged(DateTime? value) => RefreshReportingMetricsOnly();

    partial void OnScheduledPaymentTypeChanged(string value)
    {
        UpdateScheduledPaymentCategoryOptions(SelectedScheduledPaymentCategory?.Code);
    }

    partial void OnCurrentUserRoleChanged(string value)
    {
        OnPropertyChanged(nameof(IsCurrentUserAdmin));
    }

    partial void OnSelectedScheduledRepeatOptionChanged(CategoryOptionViewModel? value)
    {
        OnPropertyChanged(nameof(IsCustomScheduledRepeat));
        OnPropertyChanged(nameof(HasScheduledCustomSecondaryUnit));
        OnPropertyChanged(nameof(HasScheduledCustomTertiaryUnit));
        OnPropertyChanged(nameof(HasScheduledCustomQuaternaryUnit));

        if (string.Equals(value?.Code, "custom", StringComparison.OrdinalIgnoreCase) &&
            SelectedScheduledCustomPrimaryUnit is null)
        {
            ResetScheduledCustomRepeatBuilder();
        }
    }

    partial void OnSelectedScheduledCustomPrimaryUnitChanged(CategoryOptionViewModel? value)
    {
        CoerceScheduledCustomRepeatSelection(1);
        OnPropertyChanged(nameof(ScheduledCustomSecondaryUnitOptions));
        OnPropertyChanged(nameof(HasScheduledCustomSecondaryUnit));
        OnPropertyChanged(nameof(HasScheduledCustomTertiaryUnit));
        OnPropertyChanged(nameof(HasScheduledCustomQuaternaryUnit));
    }

    partial void OnSelectedScheduledCustomSecondaryUnitChanged(CategoryOptionViewModel? value)
    {
        CoerceScheduledCustomRepeatSelection(2);
        OnPropertyChanged(nameof(ScheduledCustomTertiaryUnitOptions));
        OnPropertyChanged(nameof(HasScheduledCustomTertiaryUnit));
        OnPropertyChanged(nameof(HasScheduledCustomQuaternaryUnit));
    }

    partial void OnSelectedScheduledCustomTertiaryUnitChanged(CategoryOptionViewModel? value)
    {
        CoerceScheduledCustomRepeatSelection(3);
        OnPropertyChanged(nameof(ScheduledCustomQuaternaryUnitOptions));
        OnPropertyChanged(nameof(HasScheduledCustomQuaternaryUnit));
    }

    partial void OnSelectedScheduledCustomQuaternaryUnitChanged(CategoryOptionViewModel? value)
    {
        CoerceScheduledCustomRepeatSelection(4);
    }

    partial void OnTransactionSelectedDateChanged(DateTimeOffset? value)
    {
        if (isSyncingTransactionDateState)
        {
            OnPropertyChanged(nameof(IsPlannedTransactionMode));
            OnPropertyChanged(nameof(TransactionDialogTitle));
            return;
        }

        if (value.HasValue)
        {
            isSyncingTransactionDateState = true;
            try
            {
                TransactionDate = value.Value.LocalDateTime;
                TransactionDateInput = value.Value.ToString(DateDisplayFormat, CultureInfo.InvariantCulture);
                TransactionSelectedDay = value.Value.Day;
                TransactionSelectedMonth = value.Value.Month;
                TransactionSelectedYear = value.Value.Year;
            }
            finally
            {
                isSyncingTransactionDateState = false;
            }
        }

        OnPropertyChanged(nameof(IsPlannedTransactionMode));
        OnPropertyChanged(nameof(TransactionDialogTitle));
    }

    partial void OnTransactionDateChanged(DateTime? value)
    {
        if (!value.HasValue || isSyncingTransactionDateState)
        {
            return;
        }

        isSyncingTransactionDateState = true;
        try
        {
            var localDate = value.Value;
            var offset = TransactionSelectedDate?.Offset ?? TimeZoneInfo.Local.GetUtcOffset(localDate);
            TransactionSelectedDate = new DateTimeOffset(DateTime.SpecifyKind(localDate, DateTimeKind.Unspecified), offset);
        }
        finally
        {
            isSyncingTransactionDateState = false;
        }
    }

    partial void OnTransactionSelectedTimeChanged(TimeSpan? value)
    {
        OnPropertyChanged(nameof(IsPlannedTransactionMode));
        OnPropertyChanged(nameof(TransactionDialogTitle));
    }

    partial void OnTransactionSelectedDayChanged(int value) => UpdateTransactionSelectedDateFromParts();
    partial void OnTransactionSelectedMonthChanged(int value) => UpdateTransactionSelectedDateFromParts();
    partial void OnTransactionSelectedYearChanged(int value) => UpdateTransactionSelectedDateFromParts();

    partial void OnSelectedLanguageChanged(LanguageOptionViewModel? value)
    {
        if (value is null)
        {
            return;
        }

        Texts = LocalizedText.Load(value.Code);
        UpdateLanguageOptionTitles();
        UpdateThemeOptionTitles();
        UpdateScheduledRepeatOptionTitles();
        RefreshLocalizedProperties();
    }

    partial void OnOnboardingCategoriesChanged(ObservableCollection<OnboardingCategoryOptionViewModel> value)
    {
        RefreshOnboardingCategoryColumns();
    }

    partial void OnCategoriesChanged(ObservableCollection<CategoryManagementItemViewModel> value)
    {
        RefreshCategoryManagementViews();
    }

    partial void OnSelectedMergeSourceCategoryChanged(CategoryManagementItemViewModel? value)
    {
        OnPropertyChanged(nameof(MergeTargetCategories));
        if (SelectedMergeTargetCategory is not null &&
            (value is null ||
             SelectedMergeTargetCategory.Id == value.Id ||
             !string.Equals(SelectedMergeTargetCategory.Type, value.Type, StringComparison.OrdinalIgnoreCase)))
        {
            SelectedMergeTargetCategory = MergeTargetCategories.FirstOrDefault();
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
    private async Task OpenSavingsAsync()
    {
        if (!EnsureAuthorizedNavigation())
        {
            return;
        }

        CurrentPage = SavingsPage;
        await LoadBankAccountsAsync();
        await LoadSavingsAsync();
    }

    [RelayCommand]
    private async Task OpenBankAccountsAsync()
    {
        if (!EnsureAuthorizedNavigation())
        {
            return;
        }

        CurrentPage = BankAccountsPage;
        await LoadBankAccountsAsync();
    }

    [RelayCommand]
    private async Task OpenGroupsAsync()
    {
        if (!EnsureAuthorizedNavigation())
        {
            return;
        }

        CurrentPage = GroupsPage;
        await LoadGroupsAsync();
        await LoadBankAccountsAsync();
        await LoadSavingsAsync();
    }

    [RelayCommand]
    private async Task OpenScheduledPaymentsAsync()
    {
        if (!EnsureAuthorizedNavigation())
        {
            return;
        }

        CurrentPage = ScheduledPaymentsPage;
        await LoadScheduledPaymentsAsync();
    }

    [RelayCommand]
    private async Task OpenCategoriesAsync()
    {
        if (!EnsureAuthorizedNavigation())
        {
            return;
        }

        CurrentPage = CategoriesPage;
        IsCategoryDetailsOpen = false;
        IsCategorySettingsMode = true;
        await LoadCategoryManagementAsync();
        await LoadBudgetsAsync();
        UpdateCategoryDetails();
    }

    [RelayCommand]
    private void OpenAllTransactionsTab()
    {
        if (!EnsureAuthorizedNavigation())
        {
            return;
        }

        ApplyTransactionFilters();
        CurrentPage = AllTransactionsPage;
    }

    [RelayCommand]
    private async Task OpenSettingsAsync()
    {
        if (!EnsureAuthorizedNavigation())
        {
            return;
        }

        CurrentPage = SettingsPage;
        await LoadSettingsDataAsync();
    }

    [RelayCommand]
    private async Task OpenReportingAsync()
    {
        if (!EnsureAuthorizedNavigation())
        {
            return;
        }

        CurrentPage = ReportingPage;
        await LoadReportingDataAsync();
    }

    private async Task LoadReportingDataAsync()
    {
        await LoadBankAccountsAsync();
        await LoadTransactionsAsync();
        await LoadSavingsAsync();
        await LoadBudgetsAsync();
        RefreshReportingSelections();
        OnPropertyChanged(nameof(ReportIncomeTotal));
        OnPropertyChanged(nameof(ReportExpenseTotal));
        OnPropertyChanged(nameof(ReportBalanceTotal));
    }

    [RelayCommand]
    private async Task ExportReportExcelAsync()
    {
        if (IsApiBusy)
        {
            return;
        }

        try
        {
            IsApiBusy = true;
            await LoadReportingDataAsync();
            var reportDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "KeepWalletReports");
            Directory.CreateDirectory(reportDir);
            var filePath = Path.Combine(reportDir, $"report-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
            var sb = new StringBuilder();
            sb.AppendLine("Type,Date,Description,Category,Amount,Currency,AccountId");
            foreach (var t in AllTransactions.OrderByDescending(x => x.TransactionDate))
            {
                sb.Append('"').Append(t.Type).Append("\",");
                sb.Append('"').Append(t.TransactionDateDisplay).Append("\",");
                sb.Append('"').Append((t.DescriptionDisplay ?? string.Empty).Replace("\"", "\"\"")).Append("\",");
                sb.Append('"').Append((t.CategoryDisplay ?? string.Empty).Replace("\"", "\"\"")).Append("\",");
                sb.Append(t.Amount.ToString(CultureInfo.InvariantCulture)).Append(',');
                sb.Append('"').Append(t.Currency).Append("\",");
                sb.Append('"').Append(t.AccountId).Append('"').AppendLine();
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
            ReportStatusMessage = $"Excel export: {filePath}";
        }
        catch (Exception ex)
        {
            ReportStatusMessage = $"{Texts.RequestErrorPrefix}: {ex.Message}";
        }
        finally
        {
            IsApiBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExportReportPdfAsync()
    {
        if (IsApiBusy)
        {
            return;
        }

        try
        {
            IsApiBusy = true;
            await LoadReportingDataAsync();
            var reportDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "KeepWalletReports");
            Directory.CreateDirectory(reportDir);
            var filePath = Path.Combine(reportDir, $"report-{DateTime.Now:yyyyMMdd-HHmmss}.pdf");
            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);
                    page.Header().Text("KeepWallet Profile Report").SemiBold().FontSize(18);
                    page.Content().Column(col =>
                    {
                        col.Spacing(8);
                        col.Item().Text($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}");
                        col.Item().Text($"Bank Accounts: {BankAccounts.Count}");
                        col.Item().Text($"Savings: {Savings.Count}");
                        col.Item().Text($"Transactions: {AllTransactions.Count}");
                        col.Item().Text($"Income: {ReportIncomeTotal:N2}");
                        col.Item().Text($"Expense: {ReportExpenseTotal:N2}");
                        col.Item().Text($"Balance: {ReportBalanceTotal:N2}");
                        col.Item().LineHorizontal(1);
                        foreach (var t in AllTransactions.OrderByDescending(x => x.TransactionDate).Take(200))
                        {
                            col.Item().Text($"{t.TransactionDateDisplay} | {t.Type} | {t.Amount:N2} {t.Currency} | {t.CategoryDisplay} | {t.DescriptionDisplay}");
                        }
                    });
                });
            }).GeneratePdf(filePath);

            ReportStatusMessage = $"PDF export: {filePath}";
        }
        catch (Exception ex)
        {
            ReportStatusMessage = $"{Texts.RequestErrorPrefix}: {ex.Message}";
        }
        finally
        {
            IsApiBusy = false;
        }
    }

    [RelayCommand]
    private void OpenAuth()
    {
        CurrentPage = AuthPage;
    }

    [RelayCommand]
    private void ToggleAuthMode()
    {
        IsRegisterMode = !IsRegisterMode;
        ApiStatusMessage = string.Empty;
    }

    [RelayCommand]
    private void OpenServerDialog()
    {
        LoadServerSettingsFromBaseUrl();
        IsServerDialogOpen = true;
    }

    [RelayCommand]
    private void CloseServerDialog()
    {
        IsServerDialogOpen = false;
    }

    [RelayCommand]
    private void SaveServerSettings()
    {
        var host = ServerHost.Trim();
        var port = ServerPort.Trim();

        if (string.IsNullOrWhiteSpace(host))
        {
            ApiStatusMessage = Texts.ApiBaseUrlMissing;
            return;
        }

        if (!Uri.TryCreate(host, UriKind.Absolute, out var hostUri) &&
            !Uri.TryCreate($"http://{host}", UriKind.Absolute, out hostUri))
        {
            ApiStatusMessage = Texts.ApiBaseUrlMissing;
            return;
        }

        var builder = new UriBuilder(hostUri);

        if (!string.IsNullOrWhiteSpace(port))
        {
            if (!int.TryParse(port, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedPort))
            {
                ApiStatusMessage = Texts.ApiBaseUrlMissing;
                return;
            }

            builder.Port = parsedPort;
        }

        ApiBaseUrl = builder.Uri.GetLeftPart(UriPartial.Authority);
        _apiClient?.Dispose();
        _apiClient = null;
        IsServerDialogOpen = false;
        SaveLocalUserSettings();
        ApiStatusMessage = $"{Texts.ApiConnectionTitle}: {ApiBaseUrl}";
    }

    [RelayCommand]
    private async Task LoginToApiAsync()
    {
        if (IsApiBusy)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(ApiLogin) || string.IsNullOrWhiteSpace(ApiPassword))
        {
            ApiStatusMessage = Texts.LoginPasswordRequired;
            CurrentPage = AuthPage;
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();
            ApiStatusMessage = Texts.ConnectingToApi;

            var auth = await _apiClient!.LoginAsync(ApiLogin.Trim(), ApiPassword);
            ApplyAuthorizedUser(auth.Username, auth.Email, auth.RoleName);
            SaveLocalUserSettings();
            ApiPassword = string.Empty;
            ApiStatusMessage = $"{Texts.LoginSuccessPrefix}: {auth.Username}.";
            await LoadCategoriesAsync();
            await LoadUserCategoryPreferencesAsync();
            await LoadGroupsAsync();
            await LoadBankAccountsAsync();
            if (BankAccounts.Count == 0)
            {
                IsCategorySettingsMode = false;
                await PrepareOnboardingAsync();
                return;
            }

            await LoadTransactionsAsync();
        }
        catch (HttpRequestException ex)
        {
            ResetAuthorizedUser();
            ApiStatusMessage = GetApiErrorMessage(ex);
        }
        catch (Exception ex)
        {
            ResetAuthorizedUser();
            ApiStatusMessage = $"{Texts.ApiConnectionErrorPrefix}: {ex.Message}";
        }
        finally
        {
            IsApiBusy = false;
        }
    }

    [RelayCommand]
    private async Task RegisterInApiAsync()
    {
        if (IsApiBusy)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(RegisterUsername) ||
            string.IsNullOrWhiteSpace(RegisterEmail) ||
            string.IsNullOrWhiteSpace(RegisterPassword))
        {
            ApiStatusMessage = Texts.RegisterRequired;
            CurrentPage = AuthPage;
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();
            ApiStatusMessage = Texts.RegisteringToApi;

            var auth = await _apiClient!.RegisterAsync(
                RegisterUsername.Trim(),
                RegisterEmail.Trim(),
                RegisterPassword,
                string.IsNullOrWhiteSpace(RegisterFullName) ? null : RegisterFullName.Trim());

            ApplyAuthorizedUser(auth.Username, auth.Email, auth.RoleName);
            ApiLogin = RegisterUsername.Trim();
            SaveLocalUserSettings();
            ApiPassword = string.Empty;
            RegisterPassword = string.Empty;
            ApiStatusMessage = $"{Texts.RegisterSuccessPrefix}: {auth.Username}.";
            await LoadCategoriesAsync();
            await PrepareOnboardingAsync();
        }
        catch (HttpRequestException ex)
        {
            ResetAuthorizedUser();
            ApiStatusMessage = GetApiErrorMessage(ex);
        }
        catch (Exception ex)
        {
            ResetAuthorizedUser();
            ApiStatusMessage = $"{Texts.RegisterErrorPrefix}: {ex.Message}";
        }
        finally
        {
            IsApiBusy = false;
        }
    }

    [RelayCommand]
    private async Task RefreshApiProfileAsync()
    {
        if (IsApiBusy)
        {
            return;
        }

        await RestoreApiSessionAsync();
    }

    [RelayCommand]
    private async Task LogoutFromApiAsync()
    {
        if (IsApiBusy)
        {
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();
            await _apiClient!.LogoutAsync();
            ResetAuthorizedUser();
            ApiStatusMessage = Texts.ApiSessionClosed;
        }
        catch (Exception ex)
        {
            ApiStatusMessage = $"{Texts.ApiLogoutErrorPrefix}: {ex.Message}";
        }
        finally
        {
            IsApiBusy = false;
        }
    }

    [RelayCommand]
    private async Task CreateTransactionAsync()
    {
        if (!await EnsureBankAccountExistsAsync(Texts.MissingBankAccountTransactionPrompt))
        {
            return;
        }

        BeginCreateTransaction();
        await LoadCategoriesAsync();
        IsAllTransactionsDialogOpen = false;
        IsTransactionDialogOpen = true;
    }

    [RelayCommand]
    private async Task AddOnboardingCategoryAsync()
    {
        if (IsApiBusy)
        {
            return;
        }

        var name = OnboardingNewCategoryName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            OnboardingStatusMessage = Texts.OnboardingCategoryNameRequired;
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();

            var category = await _apiClient!.CreateCategoryAsync(new CreateCategoryRequestDto(
                name,
                string.Equals(OnboardingNewCategoryType, "income", StringComparison.OrdinalIgnoreCase)
                    ? "income"
                    : "expense"));

            if (OnboardingCategories.All(c => c.Id != category.Id))
            {
                OnboardingCategories.Add(new OnboardingCategoryOptionViewModel(
                    category.Id,
                    category.Name,
                    category.Type,
                    GetOnboardingCategoryTypeDisplay(category.Type),
                    true));
            }
            else
            {
                var existing = OnboardingCategories.First(c => c.Id == category.Id);
                existing.IsSelected = true;
            }

            OnboardingNewCategoryName = string.Empty;
            OnboardingStatusMessage = string.Empty;
            RefreshOnboardingCategoryColumns();
            await LoadCategoriesAsync();
        }
        catch (HttpRequestException ex)
        {
            OnboardingStatusMessage = GetApiErrorMessage(ex);
        }
        catch (Exception ex)
        {
            OnboardingStatusMessage = $"{Texts.RequestErrorPrefix}: {ex.Message}";
        }
        finally
        {
            IsApiBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddCategoryAsync()
    {
        if (IsApiBusy)
        {
            return;
        }

        var name = CategoryNewName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            CategoryStatusMessage = Texts.OnboardingCategoryNameRequired;
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();
            var iconKey = CategoryNewIconKey;
            var color = CategoryNewColor;

            var category = await _apiClient!.CreateCategoryAsync(new CreateCategoryRequestDto(
                name,
                string.Equals(CategoryNewType, "income", StringComparison.OrdinalIgnoreCase) ? "income" : "expense",
                iconKey,
                color));

            var selectedIds = Categories
                .Where(c => c.IsSelected)
                .Select(c => c.Id)
                .Append(category.Id)
                .Distinct()
                .ToArray();
            var preferences = Categories
                .Select(c => new UserCategoryPreferenceUpdateDto(c.Id, c.SelectedIconKey, c.SelectedColor, c.IsSelected))
                .Append(new UserCategoryPreferenceUpdateDto(category.Id, iconKey, color, true))
                .ToArray();
            await _apiClient.UpdateUserCategoriesAsync(new UpdateUserCategoryPreferencesRequestDto(selectedIds, preferences));
            selectedCategoryIds = selectedIds.ToHashSet();

            CategoryNewName = string.Empty;
            CategoryNewColor = "#E5E7EB";
            CategoryStatusMessage = string.Empty;
            await LoadCategoryManagementAsync();
            await LoadCategoriesAsync();
        }
        catch (HttpRequestException ex)
        {
            CategoryStatusMessage = GetApiErrorMessage(ex);
        }
        catch (Exception ex)
        {
            CategoryStatusMessage = $"{Texts.RequestErrorPrefix}: {ex.Message}";
        }
        finally
        {
            IsApiBusy = false;
        }
    }

    [RelayCommand]
    private void SelectCategoryType(string type)
    {
        CategoryNewType = string.Equals(type, "income", StringComparison.OrdinalIgnoreCase)
            ? "income"
            : "expense";
    }

    [RelayCommand]
    private async Task SaveCategoryIconAsync(CategoryManagementItemViewModel? category)
    {
        if (IsApiBusy || category is null)
        {
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();
            await _apiClient!.UpdateCategoryAsync(category.Id, new UpdateCategoryRequestDto(
                IconKey: category.SelectedIconKey,
                Color: category.SelectedColor));
            CategoryStatusMessage = string.Empty;
            await LoadCategoryManagementAsync();
            await LoadCategoriesAsync();
            await LoadUserCategoryPreferencesAsync();
            await LoadTransactionsAsync();
        }
        catch (HttpRequestException ex)
        {
            CategoryStatusMessage = GetApiErrorMessage(ex);
        }
        catch (Exception ex)
        {
            CategoryStatusMessage = $"{Texts.RequestErrorPrefix}: {ex.Message}";
        }
        finally
        {
            IsApiBusy = false;
        }
    }

    [RelayCommand]
    private void EditCategoryName(CategoryManagementItemViewModel? category)
    {
        if (category is null)
        {
            return;
        }

        category.NameInput = category.Name;
        category.IsEditingName = true;
    }

    [RelayCommand]
    private async Task FinishCategoryNameEditAsync(CategoryManagementItemViewModel? category)
    {
        if (category is null || !category.IsEditingName)
        {
            return;
        }

        var name = category.NameInput.Trim();
        if (string.IsNullOrWhiteSpace(name) || string.Equals(name, category.Name, StringComparison.Ordinal))
        {
            category.NameInput = category.Name;
            category.IsEditingName = false;
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();
            var updated = await _apiClient!.UpdateCategoryAsync(category.Id, new UpdateCategoryRequestDto(Name: name));
            category.Name = updated.Name;
            category.NameInput = updated.Name;
            category.IsEditingName = false;
            await LoadCategoriesAsync();
        }
        catch (HttpRequestException ex)
        {
            ApiStatusMessage = GetApiErrorMessage(ex);
        }
        catch (Exception ex)
        {
            ApiStatusMessage = $"{Texts.RequestErrorPrefix}: {ex.Message}";
        }
        finally
        {
            IsApiBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveCategoriesAsync()
    {
        if (IsApiBusy)
        {
            return;
        }

        var selectedIds = Categories
            .Where(c => c.IsSelected)
            .Select(c => c.Id)
            .Distinct()
            .ToArray();

        if (selectedIds.Length == 0)
        {
            CategoryStatusMessage = Texts.OnboardingCategorySelectionRequired;
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();
            await _apiClient!.UpdateUserCategoriesAsync(CreateCategoryPreferencesRequest(selectedIds));
            selectedCategoryIds = selectedIds.ToHashSet();
            CategoryStatusMessage = string.Empty;
            await LoadCategoriesAsync();
            await LoadUserCategoryPreferencesAsync();
            await LoadTransactionsAsync();
        }
        catch (HttpRequestException ex)
        {
            CategoryStatusMessage = GetApiErrorMessage(ex);
        }
        catch (Exception ex)
        {
            CategoryStatusMessage = $"{Texts.RequestErrorPrefix}: {ex.Message}";
        }
        finally
        {
            IsApiBusy = false;
        }
    }

    private UpdateUserCategoryPreferencesRequestDto CreateCategoryPreferencesRequest(IReadOnlyList<int> selectedIds)
    {
        var preferences = Categories
            .Select(category => new UserCategoryPreferenceUpdateDto(
                category.Id,
                category.SelectedIconKey,
                category.SelectedColor,
                category.IsSelected))
            .ToArray();

        return new UpdateUserCategoryPreferencesRequestDto(selectedIds, preferences);
    }

    [RelayCommand]
    private void DeleteCategory(CategoryManagementItemViewModel? category)
    {
        if (IsApiBusy || category is null)
        {
            return;
        }

        pendingDeleteCategory = category;
        DeleteCategoryPromptMessage = string.Format(CultureInfo.CurrentCulture, Texts.DeleteCategoryPromptMessage, category.Name);
        IsDeleteCategoryPromptOpen = true;
    }

    [RelayCommand]
    private void CancelDeleteCategory()
    {
        pendingDeleteCategory = null;
        DeleteCategoryPromptMessage = string.Empty;
        IsDeleteCategoryPromptOpen = false;
    }

    [RelayCommand]
    private async Task ConfirmDeleteCategoryAsync()
    {
        if (IsApiBusy || pendingDeleteCategory is null)
        {
            return;
        }

        var category = pendingDeleteCategory;
        try
        {
            IsApiBusy = true;
            IsDeleteCategoryPromptOpen = false;
            EnsureApiClient();
            await _apiClient!.DeleteCategoryAsync(category.Id);
            pendingDeleteCategory = null;
            DeleteCategoryPromptMessage = string.Empty;
            CategoryStatusMessage = string.Empty;
            await LoadCategoryManagementAsync();
            await LoadCategoriesAsync();
            await LoadTransactionsAsync();
        }
        catch (HttpRequestException ex)
        {
            CategoryStatusMessage = GetApiErrorMessage(ex);
        }
        catch (Exception ex)
        {
            CategoryStatusMessage = $"{Texts.RequestErrorPrefix}: {ex.Message}";
        }
        finally
        {
            IsApiBusy = false;
        }
    }

    [RelayCommand]
    private async Task MergeCategoriesAsync()
    {
        if (IsApiBusy || SelectedMergeSourceCategory is null || SelectedMergeTargetCategory is null)
        {
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();
            await _apiClient!.MergeCategoryAsync(
                SelectedMergeSourceCategory.Id,
                new MergeCategoryRequestDto(SelectedMergeTargetCategory.Id));

            SelectedMergeSourceCategory = null;
            SelectedMergeTargetCategory = null;
            CategoryStatusMessage = string.Empty;
            await LoadCategoryManagementAsync();
            await LoadCategoriesAsync();
            await LoadBudgetsAsync();
            await LoadTransactionsAsync();
        }
        catch (HttpRequestException ex)
        {
            CategoryStatusMessage = GetApiErrorMessage(ex);
        }
        catch (Exception ex)
        {
            CategoryStatusMessage = $"{Texts.RequestErrorPrefix}: {ex.Message}";
        }
        finally
        {
            IsApiBusy = false;
        }
    }

    [RelayCommand]
    private void SelectOnboardingCategoryType(string type)
    {
        OnboardingNewCategoryType = string.Equals(type, "income", StringComparison.OrdinalIgnoreCase)
            ? "income"
            : "expense";
    }

    [RelayCommand]
    private async Task FinishOnboardingAsync()
    {
        if (IsApiBusy)
        {
            return;
        }

        var selectedIds = OnboardingCategories
            .Where(c => c.IsSelected)
            .Select(c => c.Id)
            .Distinct()
            .ToArray();

        if (!IsCategorySettingsMode && string.IsNullOrWhiteSpace(OnboardingAccountName))
        {
            OnboardingStatusMessage = Texts.OnboardingAccountNameRequired;
            return;
        }

        if (selectedIds.Length == 0)
        {
            OnboardingStatusMessage = Texts.OnboardingCategorySelectionRequired;
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();

            if (IsCategorySettingsMode)
            {
                await _apiClient!.UpdateUserCategoriesAsync(new UpdateUserCategoryPreferencesRequestDto(selectedIds));
                selectedCategoryIds = selectedIds.ToHashSet();
                IsOnboardingDialogOpen = false;
                IsCategorySettingsMode = false;
                OnboardingStatusMessage = string.Empty;

                await LoadCategoriesAsync();
                await LoadTransactionsAsync();
                return;
            }

            var account = await _apiClient!.CreateBankAccountAsync(new CreateBankAccountRequestDto(
                OnboardingAccountName.Trim(),
                OnboardingSelectedCurrency.Trim().ToUpperInvariant(),
                0,
                true));

            await _apiClient.UpdateUserCategoriesAsync(new UpdateUserCategoryPreferencesRequestDto(selectedIds));

            defaultAccountId = account.Id;
            TransactionCurrencyInput = account.Currency;
            selectedCategoryIds = selectedIds.ToHashSet();
            IsOnboardingDialogOpen = false;
            OnboardingStatusMessage = string.Empty;

            await LoadCategoriesAsync();
            await LoadBankAccountsAsync();
            await LoadTransactionsAsync();
        }
        catch (HttpRequestException ex)
        {
            OnboardingStatusMessage = GetApiErrorMessage(ex);
        }
        catch (Exception ex)
        {
            OnboardingStatusMessage = $"{Texts.RequestErrorPrefix}: {ex.Message}";
        }
        finally
        {
            IsApiBusy = false;
        }
    }

    [RelayCommand]
    private void ConfirmCreateBankAccountFromPrompt()
    {
        IsMissingBankAccountPromptOpen = false;
        MissingBankAccountPromptMessage = string.Empty;
        BeginCreateBankAccount();
    }

    [RelayCommand]
    private void CancelCreateBankAccountFromPrompt()
    {
        IsMissingBankAccountPromptOpen = false;
        MissingBankAccountPromptMessage = string.Empty;
    }

    [RelayCommand]
    private void CreateNewSavings()
    {
        SavingsDialogNameInput = string.Empty;
        SavingsDialogTargetAmountInput = string.Empty;
        SavingsDialogDeadlineInput = string.Empty;
        SavingsDialogCurrency = OnboardingSelectedCurrency;
        SavingsDialogIconKey = "other";
        SavingsDialogColor = "#3B82F6";
        SavingsStatusMessage = string.Empty;
        IsSavingsDialogOpen = true;
    }

    [RelayCommand]
    private void CloseSavingsDialog()
    {
        IsSavingsDialogOpen = false;
    }

    [RelayCommand]
    private async Task SaveNewSavingsAsync()
    {
        if (IsApiBusy)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(SavingsDialogNameInput) ||
            !TryParseOptionalPositiveDecimal(SavingsDialogTargetAmountInput, out var targetAmount) ||
            !TryParseOptionalSavingsDeadline(SavingsDialogDeadlineInput, out var deadline))
        {
            SavingsStatusMessage = Texts.TransactionFormValidationText;
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();

            var saving = await _apiClient!.CreateSavingAsync(new CreateSavingRequestDto(
                SavingsDialogNameInput.Trim(),
                targetAmount > 0 ? targetAmount : null,
                0,
                deadline,
                SavingsDialogCurrency,
                SavingsDialogIconKey,
                SavingsDialogColor));

            ApplySaving(saving);
            await LoadSavingsAsync();
            await LoadSavingDetailsAsync(saving.Id);
            await LoadBankAccountsAsync();
            await LoadTransactionsAsync();
            IsSavingsDialogOpen = false;
            SavingsStatusMessage = Texts.TransactionSavedText;
        }
        catch (HttpRequestException ex)
        {
            SavingsStatusMessage = GetApiErrorMessage(ex);
        }
        catch (Exception ex)
        {
            SavingsStatusMessage = $"{Texts.RequestErrorPrefix}: {ex.Message}";
        }
        finally
        {
            IsApiBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveGoalAsync()
    {
        if (IsApiBusy)
        {
            return;
        }

        if (!IsEditingSavingDetails)
        {
            IsEditingSavingDetails = true;
            IsEditingSavingNameField = true;
            IsEditingSavingGoalField = true;
            IsEditingSavingCurrentField = true;
            IsEditingSavingCurrencyField = true;
            IsEditingSavingDeadlineField = true;
            return;
        }

        if (!decimal.TryParse(SavingGoalAmountEditInput, NumberStyles.Number, CultureInfo.InvariantCulture, out var goalAmountValue) ||
            goalAmountValue < 0 ||
            !decimal.TryParse(SavingCurrentAmountEditInput, NumberStyles.Number, CultureInfo.InvariantCulture, out var currentAmountValue) ||
            currentAmountValue < 0 ||
            string.IsNullOrWhiteSpace(SavingNameEditInput))
        {
            SavingsStatusMessage = Texts.TransactionFormValidationText;
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();

            decimal? targetAmount = goalAmountValue > 0 ? goalAmountValue : null;
            var normalizedName = SavingNameEditInput.Trim();
            var deadline = SavingDeadlineEditDate.HasValue
                ? DateOnly.FromDateTime(SavingDeadlineEditDate.Value.Date)
                : (DateOnly?)null;
            ApiSavingDto saving;

            if (ActiveSavingId.HasValue)
            {
                saving = await _apiClient!.UpdateSavingAsync(
                        ActiveSavingId.Value,
                        new UpdateSavingRequestDto(
                        string.IsNullOrWhiteSpace(normalizedName) ? "Savings" : normalizedName,
                        targetAmount,
                        currentAmountValue,
                        deadline,
                        targetAmount.HasValue && currentAmountValue >= targetAmount.Value,
                        string.IsNullOrWhiteSpace(SavingCurrencyEditInput) ? "UAH" : SavingCurrencyEditInput.Trim().ToUpperInvariant(),
                        ActiveSavingIconKey,
                        ActiveSavingColor));
            }
            else
            {
                saving = await _apiClient!.CreateSavingAsync(new CreateSavingRequestDto(
                    string.IsNullOrWhiteSpace(normalizedName) ? "Savings" : normalizedName,
                    targetAmount,
                    currentAmountValue,
                    deadline,
                    string.IsNullOrWhiteSpace(SavingCurrencyEditInput) ? "UAH" : SavingCurrencyEditInput.Trim().ToUpperInvariant(),
                    ActiveSavingIconKey,
                    ActiveSavingColor));
            }

            ApplySaving(saving);
            await LoadSavingDetailsAsync(saving.Id);
            IsEditingSavingDetails = false;
            IsEditingSavingNameField = false;
            IsEditingSavingGoalField = false;
            IsEditingSavingCurrentField = false;
            IsEditingSavingCurrencyField = false;
            IsEditingSavingDeadlineField = false;
            SavingsStatusMessage = Texts.TransactionUpdatedText;
        }
        catch (HttpRequestException ex)
        {
            SavingsStatusMessage = GetApiErrorMessage(ex);
        }
        catch (Exception ex)
        {
            SavingsStatusMessage = $"{Texts.RequestErrorPrefix}: {ex.Message}";
        }
        finally
        {
            IsApiBusy = false;
        }
    }

    [RelayCommand]
    private void BeginEditSavingName()
    {
        IsEditingSavingDetails = true;
        IsEditingSavingNameField = true;
    }

    [RelayCommand]
    private async Task SaveSavingNameOnBlurAsync()
    {
        if (!IsEditingSavingNameField || IsApiBusy || !ActiveSavingId.HasValue)
        {
            IsEditingSavingNameField = false;
            return;
        }

        var normalizedName = SavingNameEditInput?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            SavingNameEditInput = SavingNameInput;
            IsEditingSavingNameField = false;
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();
            var targetAmount = GoalAmount > 0 ? (decimal?)GoalAmount : null;
            var deadline = SavingDeadlineEditDate.HasValue ? DateOnly.FromDateTime(SavingDeadlineEditDate.Value.Date) : ActiveSavingDeadline;
            var updated = await _apiClient!.UpdateSavingAsync(
                ActiveSavingId.Value,
                new UpdateSavingRequestDto(
                    normalizedName,
                    targetAmount,
                    (decimal)SavedAmount,
                    deadline,
                    targetAmount.HasValue && (decimal)SavedAmount >= targetAmount.Value,
                    string.IsNullOrWhiteSpace(ActiveSavingCurrency) ? "UAH" : ActiveSavingCurrency,
                    ActiveSavingIconKey,
                    ActiveSavingColor));
            ApplySaving(updated);
        }
        catch (Exception ex)
        {
            SavingsStatusMessage = ex.Message;
        }
        finally
        {
            IsEditingSavingNameField = false;
            IsApiBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveSavingGoalOnBlurAsync()
    {
        await SaveSavingInlineAsync(closeGoal: true, closeCurrent: false, closeCurrency: false);
    }

    [RelayCommand]
    private async Task SaveSavingCurrentOnBlurAsync()
    {
        await SaveSavingInlineAsync(closeGoal: false, closeCurrent: true, closeCurrency: false);
    }

    [RelayCommand]
    private async Task SaveSavingCurrencyOnSelectAsync()
    {
        await SaveSavingInlineAsync(closeGoal: false, closeCurrent: false, closeCurrency: true);
    }

    private async Task SaveSavingInlineAsync(bool closeGoal, bool closeCurrent, bool closeCurrency)
    {
        if (IsApiBusy || !ActiveSavingId.HasValue)
        {
            if (closeGoal) IsEditingSavingGoalField = false;
            if (closeCurrent) IsEditingSavingCurrentField = false;
            if (closeCurrency) IsEditingSavingCurrencyField = false;
            return;
        }

        if (!decimal.TryParse(SavingGoalAmountEditInput, NumberStyles.Number, CultureInfo.InvariantCulture, out var goalAmountValue) ||
            goalAmountValue < 0 ||
            !decimal.TryParse(SavingCurrentAmountEditInput, NumberStyles.Number, CultureInfo.InvariantCulture, out var currentAmountValue) ||
            currentAmountValue < 0)
        {
            if (closeGoal) IsEditingSavingGoalField = false;
            if (closeCurrent) IsEditingSavingCurrentField = false;
            if (closeCurrency) IsEditingSavingCurrencyField = false;
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();
            var targetAmount = goalAmountValue > 0 ? (decimal?)goalAmountValue : null;
            var deadline = SavingDeadlineEditDate.HasValue ? DateOnly.FromDateTime(SavingDeadlineEditDate.Value.Date) : ActiveSavingDeadline;
            var updated = await _apiClient!.UpdateSavingAsync(
                ActiveSavingId.Value,
                new UpdateSavingRequestDto(
                    string.IsNullOrWhiteSpace(SavingNameEditInput) ? "Savings" : SavingNameEditInput.Trim(),
                    targetAmount,
                    currentAmountValue,
                    deadline,
                    targetAmount.HasValue && currentAmountValue >= targetAmount.Value,
                    string.IsNullOrWhiteSpace(SavingCurrencyEditInput) ? "UAH" : SavingCurrencyEditInput.Trim().ToUpperInvariant(),
                    ActiveSavingIconKey,
                    ActiveSavingColor));
            ApplySaving(updated);
            await LoadSavingDetailsAsync(updated.Id);
        }
        finally
        {
            if (closeGoal) IsEditingSavingGoalField = false;
            if (closeCurrent) IsEditingSavingCurrentField = false;
            if (closeCurrency) IsEditingSavingCurrencyField = false;
            IsApiBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveSavingIconColorImmediatelyAsync()
    {
        if (IsApiBusy || !ActiveSavingId.HasValue)
        {
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();
            var targetAmount = GoalAmount > 0 ? (decimal?)GoalAmount : null;
            var deadline = ActiveSavingDeadline;
            var updated = await _apiClient!.UpdateSavingAsync(
                ActiveSavingId.Value,
                new UpdateSavingRequestDto(
                    string.IsNullOrWhiteSpace(SavingNameInput) ? "Savings" : SavingNameInput.Trim(),
                    targetAmount,
                    (decimal)SavedAmount,
                    deadline,
                    targetAmount.HasValue && (decimal)SavedAmount >= targetAmount.Value,
                    string.IsNullOrWhiteSpace(ActiveSavingCurrency) ? "UAH" : ActiveSavingCurrency,
                    ActiveSavingIconKey,
                    ActiveSavingColor));
            ApplySaving(updated);
        }
        finally
        {
            IsApiBusy = false;
        }
    }

    [RelayCommand]
    private void BeginEditSavingGoalAmount()
    {
        IsEditingSavingDetails = true;
        IsEditingSavingGoalField = true;
    }

    [RelayCommand]
    private void BeginEditSavingCurrentAmount()
    {
        IsEditingSavingDetails = true;
        IsEditingSavingCurrentField = true;
    }

    [RelayCommand]
    private void BeginEditSavingCurrency()
    {
        IsEditingSavingDetails = true;
        IsEditingSavingCurrencyField = true;
    }

    [RelayCommand]
    private void BeginEditSavingDeadline()
    {
        IsEditingSavingDetails = true;
        IsEditingSavingDeadlineField = true;
    }

    [RelayCommand]
    private async Task AddToSavingsAsync()
    {
        await TransferSavingsAsync(isDeposit: true);
    }

    [RelayCommand]
    private async Task WithdrawFromSavingsAsync()
    {
        await TransferSavingsAsync(isDeposit: false);
    }

    private async Task TransferSavingsAsync(bool isDeposit)
    {
        if (IsApiBusy || AddAmount <= 0)
        {
            return;
        }

        if (SelectedSavingsBankAccount is null)
        {
            SavingsStatusMessage = Texts.TransactionFormValidationText;
            return;
        }

        if (!string.Equals(SelectedSavingsBankAccount.Currency, ActiveSavingCurrency, StringComparison.OrdinalIgnoreCase))
        {
            SavingsStatusMessage = Texts.TransactionFormValidationText;
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();

            var transferAmount = (decimal)AddAmount;
            var newCurrentAmount = isDeposit
                ? (decimal)SavedAmount + transferAmount
                : Math.Max(0, (decimal)SavedAmount - transferAmount);
            decimal? targetAmount = GoalAmount > 0 ? (decimal)GoalAmount : null;
            ApiSavingDto saving;

            if (ActiveSavingId.HasValue)
            {
                saving = await _apiClient!.UpdateSavingAsync(
                        ActiveSavingId.Value,
                        new UpdateSavingRequestDto(
                        string.IsNullOrWhiteSpace(SavingNameInput) ? "Savings" : SavingNameInput.Trim(),
                        targetAmount,
                        newCurrentAmount,
                        null,
                        targetAmount.HasValue && newCurrentAmount >= targetAmount.Value,
                        ActiveSavingCurrency,
                        ActiveSavingIconKey,
                        ActiveSavingColor));
            }
            else
            {
                saving = await _apiClient!.CreateSavingAsync(new CreateSavingRequestDto(
                    string.IsNullOrWhiteSpace(SavingNameInput) ? "Savings" : SavingNameInput.Trim(),
                    targetAmount,
                    newCurrentAmount,
                    null,
                    ActiveSavingCurrency,
                    ActiveSavingIconKey,
                    ActiveSavingColor));
            }

            await CreateSavingsAccountTransactionAsync(
                transferAmount,
                SelectedSavingsBankAccount,
                isDeposit,
                saving.Id,
                saving.Name);

            AddAmount = 0;
            ApplySaving(saving);
            await LoadTransactionsAsync();
            await LoadSavingsAsync(saving.Id);
            await LoadBankAccountsAsync();
            SavingsStatusMessage = Texts.TransactionUpdatedText;
        }
        catch (HttpRequestException ex)
        {
            SavingsStatusMessage = GetApiErrorMessage(ex);
        }
        catch (Exception ex)
        {
            SavingsStatusMessage = $"{Texts.RequestErrorPrefix}: {ex.Message}";
        }
        finally
        {
            IsApiBusy = false;
        }
    }

    private async Task CreateSavingsAccountTransactionAsync(
        decimal amount,
        BankAccountItemViewModel account,
        bool isDeposit,
        int savingId,
        string savingName)
    {
        if (apiCategories.Count == 0)
        {
            await LoadCategoriesAsync();
        }

        var transactionType = isDeposit ? "expense" : "income";
        var categoryId = await ResolveSavingsTransferCategoryIdAsync(transactionType);

        await _apiClient!.CreateTransactionAsync(new CreateTransactionRequestDto(
            account.Id,
            account.GroupId,
            categoryId,
            savingId,
            null,
            amount,
            savingName,
            DateTimeOffset.Now.ToUniversalTime()));
    }

    private async Task<int> ResolveSavingsTransferCategoryIdAsync(string transactionType)
    {
        var categoryNames = new[]
            {
                Texts.SavingsTransferCategoryName,
                "Накопичення",
                "Savings"
            }
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (apiCategories.Count > 0)
        {
            var candidates = apiCategories
                .Where(c => string.Equals(c.Type, transactionType, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var preferred = candidates.FirstOrDefault(c =>
                categoryNames.Any(name => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)));

            if (preferred is not null)
            {
                return preferred.Id;
            }
        }

        var created = await _apiClient!.CreateCategoryAsync(new CreateCategoryRequestDto(
            Texts.SavingsTransferCategoryName,
            transactionType,
            "savings",
            "#3B82F6"));

        await LoadCategoriesAsync();
        return created.Id;
    }

    private static bool TryParseOptionalPositiveDecimal(string rawValue, out decimal value)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            value = 0;
            return true;
        }

        return decimal.TryParse(rawValue, NumberStyles.Number, CultureInfo.InvariantCulture, out value) && value >= 0;
    }

    private static bool TryParseOptionalSavingsDeadline(string rawValue, out DateOnly? deadline)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            deadline = null;
            return true;
        }

        if (DateOnly.TryParseExact(rawValue.Trim(), "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            deadline = parsed;
            return true;
        }

        deadline = null;
        return false;
    }

    private async Task RestoreApiSessionAsync()
    {
        try
        {
            IsApiBusy = true;
            EnsureApiClient();

            var user = await _apiClient!.GetCurrentUserAsync();
            if (user is null)
            {
                ResetAuthorizedUser();
                ApiStatusMessage = string.Format(Texts.ApiAvailableLoginRequired, ApiBaseUrl);
                return;
            }

            ApplyAuthorizedUser(user.Username, user.Email, user.RoleName);
            ApiStatusMessage = $"{Texts.SessionActivePrefix}: {user.Username}.";
            await LoadCategoriesAsync();
            await LoadUserCategoryPreferencesAsync();
            await LoadTransactionsAsync();
            await LoadSavingsAsync();
        }
        catch (HttpRequestException ex)
        {
            ResetAuthorizedUser();
            ApiStatusMessage = GetApiErrorMessage(ex);
        }
        catch (Exception ex)
        {
            ResetAuthorizedUser();
            ApiStatusMessage = $"{Texts.ApiUnavailablePrefix}: {ex.Message}";
        }
        finally
        {
            IsApiBusy = false;
        }
    }

    private bool EnsureAuthorizedNavigation()
    {
        if (IsApiAuthorized)
        {
            return true;
        }

        CurrentPage = AuthPage;
        ApiStatusMessage = Texts.AuthorizationRequired;
        return false;
    }

    private void ApplyAuthorizedUser(string username, string email, string? roleName)
    {
        CurrentUsername = username;
        CurrentUserEmail = email;
        CurrentUserRole = roleName ?? Texts.DefaultRole;
        IsApiAuthorized = true;
        IsRegisterMode = false;
        CurrentPage = HomePage;
        LoadApiOnlyPlaceholders();
        RefreshLocalizedProperties();
    }

    private void ResetAuthorizedUser()
    {
        CurrentUsername = Texts.GuestLabel;
        CurrentUserEmail = string.Empty;
        CurrentUserRole = Texts.DefaultRole;
        IsApiAuthorized = false;
        CurrentPage = AuthPage;
        LoadApiOnlyPlaceholders();
        SavingsStatusMessage = Texts.SavingsOperationsDisabled;
        RefreshLocalizedProperties();
    }

    private void EnsureApiClient()
    {
        var normalizedBaseUrl = ApiBaseUrl.Trim().TrimEnd('/');

        if (string.IsNullOrWhiteSpace(normalizedBaseUrl))
        {
            throw new InvalidOperationException(Texts.ApiBaseUrlMissing);
        }

        if (_apiClient is not null &&
            string.Equals(_apiClient.BaseUrl, normalizedBaseUrl, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _apiClient?.Dispose();
        _apiClient = new KeepWalletApiClient(normalizedBaseUrl);
    }

    private void LoadApiOnlyPlaceholders()
    {
        GoalAmount = 0;
        SavedAmount = 0;
        AddAmount = 0;
        ActiveSavingId = null;
        SavingNameInput = "Savings";
        InterestRate = 0;
        SavingItemNameInput = string.Empty;
        SavingItemAmount = 0;
        Savings = [];
        SavingRateHistory = [];
        SavingItems = [];
        Groups = [];
        NewGroupNameInput = string.Empty;
        AdminUsers = [];
        UserLogs = [];
        ScheduledPayments = [];
        Categories = [];
        BankAccounts = [];
        SelectedBankAccount = null;
        ResetBankAccountForm();
        OnPropertyChanged(nameof(AdminUsersCountDisplay));
        ScheduledPaymentNameInput = string.Empty;
        ScheduledPaymentAmountInput = string.Empty;
        ScheduledPaymentDescriptionInput = string.Empty;
        ScheduledPaymentDateInput = DateTimeOffset.Now.ToString(DateTimeDisplayFormat);
        ScheduledPaymentFrequencyInput = "once";
        ScheduledPaymentType = "expense";
        AvailableBalance = 0;
        LatestExpenses = [];
        homeTransactionSource = [];
        VisibleTransactions = [];
        AllTransactions = [];
        FilteredTransactions = [];
        TransactionSearchText = string.Empty;
        SelectedTransactionTypeFilter = "all";
        SelectedTransactionAccountFilter = null;
        TransactionAccountFilterOptions = [];
        SelectedTransactionCategoryFilter = null;
        TransactionCategoryFilterOptions = [];
        categoryLimits.Clear();
        apiCategories = [];
        selectedCategoryIds = null;
        IsAllTransactionsDialogOpen = false;
        IsCategoryDetailsOpen = false;
        IsTransactionDialogOpen = false;
        IsBankAccountDialogOpen = false;
        IsOnboardingDialogOpen = false;
        OnboardingStatusMessage = string.Empty;
        CategoryStatusMessage = string.Empty;
        ResetTransactionForm();
        if (!IsApiAuthorized)
        {
            Series = [];
            Chart = [];
            ExpenseCategoryLegend = [];
            OnPropertyChanged(nameof(Series));
            OnPropertyChanged(nameof(Chart));
            OnPropertyChanged(nameof(ExpenseCategoryLegend));
        }
        OnPropertyChanged(nameof(RemainingAmount));
        OnPropertyChanged(nameof(ProgressPercent));
        OnPropertyChanged(nameof(Money));
        OnPropertyChanged(nameof(Income));
        OnPropertyChanged(nameof(Expense));
    }

    private string GetApiErrorMessage(HttpRequestException ex)
    {
        return ex.StatusCode switch
        {
            HttpStatusCode.Unauthorized => Texts.UnauthorizedError,
            HttpStatusCode.Conflict => Texts.ConflictError,
            HttpStatusCode.BadRequest => $"{Texts.ValidationErrorPrefix}: {ex.Message}",
            _ => $"{Texts.RequestErrorPrefix}: {ex.Message}"
        };
    }

    private void RefreshLocalizedProperties()
    {
        OnPropertyChanged(nameof(ApiConnectionStatus));
        OnPropertyChanged(nameof(ApiUserDisplay));
        OnPropertyChanged(nameof(StatusDisplay));
        OnPropertyChanged(nameof(UserDisplay));
        OnPropertyChanged(nameof(EmailDisplay));
        OnPropertyChanged(nameof(RoleDisplay));
        OnPropertyChanged(nameof(SavedAmountDisplay));
        OnPropertyChanged(nameof(RemainingAmountDisplay));
        OnPropertyChanged(nameof(AuthCardTitle));
        OnPropertyChanged(nameof(AuthFooterPrompt));
        OnPropertyChanged(nameof(AuthFooterAction));
        OnPropertyChanged(nameof(TransactionTypeDisplay));
        OnPropertyChanged(nameof(TransactionDialogTitle));
        OnPropertyChanged(nameof(IsPlannedTransactionMode));
        OnPropertyChanged(nameof(IsExpenseTransactionTypeSelected));
        OnPropertyChanged(nameof(IsIncomeTransactionTypeSelected));
        OnPropertyChanged(nameof(TransactionTimelineTitle));
        OnPropertyChanged(nameof(SaveTransactionButtonText));
        RefreshOnboardingCategoryTypeDisplays();
        RefreshCategoryTypeDisplays();
        UpdateTransactionAccountFilterOptions();
        UpdateTransactionCategoryOptions(SelectedTransactionCategory?.Code);
        UpdateTransactionCategoryFilterOptions();
        RefreshTransactionItems();
        UpdateCharts();
    }

    partial void OnSelectedThemeChanged(CategoryOptionViewModel? value)
    {
        if (value is null)
        {
            return;
        }

        ThemeManager.Apply(value.Code);
        SaveLocalUserSettings();
        UpdateCharts();
    }

    private void RefreshOnboardingCategoryTypeDisplays()
    {
        if (OnboardingCategories.Count == 0)
        {
            return;
        }

        OnboardingCategories = new ObservableCollection<OnboardingCategoryOptionViewModel>(
            OnboardingCategories.Select(c => new OnboardingCategoryOptionViewModel(
                c.Id,
                c.Name,
                c.Type,
                GetOnboardingCategoryTypeDisplay(c.Type),
                c.IsSelected)));
    }

    private void RefreshCategoryTypeDisplays()
    {
        if (Categories.Count == 0)
        {
            return;
        }

        Categories = new ObservableCollection<CategoryManagementItemViewModel>(
            Categories.Select(c => new CategoryManagementItemViewModel(
                c.Id,
                c.Name,
                c.Type,
                GetOnboardingCategoryTypeDisplay(c.Type),
                c.SelectedIconKey,
                c.SelectedColor,
                c.IsSelected)));
    }

    private void RefreshOnboardingCategoryColumns()
    {
        OnPropertyChanged(nameof(OnboardingExpenseCategories));
        OnPropertyChanged(nameof(OnboardingIncomeCategories));
    }

    private void RefreshCategoryManagementViews()
    {
        OnPropertyChanged(nameof(ExpenseCategories));
        OnPropertyChanged(nameof(IncomeCategories));
        OnPropertyChanged(nameof(MergeTargetCategories));
    }

    private string GetOnboardingCategoryTypeDisplay(string type) =>
        string.Equals(type, "income", StringComparison.OrdinalIgnoreCase)
            ? Texts.TransactionIncomeType
            : Texts.TransactionExpenseType;

    private async Task LoadCategoriesAsync()
    {
        try
        {
            EnsureApiClient();
            if (IsApiAuthorized)
            {
                var userCategories = await _apiClient!.GetUserCategoriesAsync();
                apiCategories = userCategories
                    .Select(c => new ApiCategoryDto(c.Id, c.Name, c.Type, c.IconKey, c.Color))
                    .ToArray();
                selectedCategoryIds = userCategories
                    .Where(c => c.IsSelected)
                    .Select(c => c.Id)
                    .ToHashSet();
            }
            else
            {
                apiCategories = await _apiClient!.GetCategoriesAsync();
            }
        }
        catch
        {
            apiCategories = [];
            selectedCategoryIds = null;
        }

        UpdateTransactionCategoryOptions(SelectedTransactionCategory?.Code);
        UpdateScheduledPaymentCategoryOptions(SelectedScheduledPaymentCategory?.Code);
        RefreshTransactionItems();
    }

    private async Task LoadCategoryManagementAsync()
    {
        try
        {
            EnsureApiClient();
            var categories = await _apiClient!.GetUserCategoriesAsync();
            Categories = new ObservableCollection<CategoryManagementItemViewModel>(
                categories.Select(c => new CategoryManagementItemViewModel(
                    c.Id,
                    c.Name,
                    c.Type,
                    GetOnboardingCategoryTypeDisplay(c.Type),
                    c.IconKey,
                    c.Color ?? "#E5E7EB",
                    c.IsSelected)));
            SelectedMergeSourceCategory = Categories.FirstOrDefault();
            SelectedMergeTargetCategory = MergeTargetCategories.FirstOrDefault();
            CategoryStatusMessage = string.Empty;
        }
        catch (HttpRequestException ex)
        {
            CategoryStatusMessage = GetApiErrorMessage(ex);
        }
        catch (Exception ex)
        {
            CategoryStatusMessage = $"{Texts.RequestErrorPrefix}: {ex.Message}";
        }
    }

    private async Task LoadUserCategoryPreferencesAsync()
    {
        if (!IsApiAuthorized)
        {
            selectedCategoryIds = null;
            return;
        }

        try
        {
            EnsureApiClient();
            var categories = await _apiClient!.GetUserCategoriesAsync();
            selectedCategoryIds = categories
                .Where(c => c.IsSelected)
                .Select(c => c.Id)
                .ToHashSet();
            ApplyUserCategoryPreferences(categories);
        }
        catch
        {
            selectedCategoryIds = null;
        }

        UpdateTransactionCategoryOptions(SelectedTransactionCategory?.Code);
        UpdateScheduledPaymentCategoryOptions(SelectedScheduledPaymentCategory?.Code);
        RefreshTransactionItems();
    }

    private void ApplyUserCategoryPreferences(IReadOnlyList<UserCategoryPreferenceDto> categories)
    {
        if (apiCategories.Count == 0 || categories.Count == 0)
        {
            return;
        }

        var preferencesById = categories.ToDictionary(c => c.Id);
        apiCategories = apiCategories
            .Select(category =>
            {
                if (!preferencesById.TryGetValue(category.Id, out var preference))
                {
                    return category;
                }

                return category with
                {
                    IconKey = string.IsNullOrWhiteSpace(preference.IconKey) ? category.IconKey : preference.IconKey,
                    Color = string.IsNullOrWhiteSpace(preference.Color) ? category.Color : preference.Color
                };
            })
            .ToArray();
    }

    private async Task LoadBudgetsAsync()
    {
        if (!IsApiAuthorized)
        {
            categoryLimits.Clear();
            return;
        }

        try
        {
            EnsureApiClient();
            var budgets = await _apiClient!.GetBudgetsAsync();
            var activeBudgets = budgets
                .Where(b => b.IsActive)
                .GroupBy(b => b.CategoryId)
                .Select(g => g
                    .OrderByDescending(b => b.GroupId == SelectedBankAccount?.GroupId)
                    .ThenBy(b => b.GroupId.HasValue)
                    .ThenByDescending(b => b.StartDate)
                    .First());

            categoryLimits.Clear();
            foreach (var budget in activeBudgets)
            {
                categoryLimits[budget.CategoryId.ToString(CultureInfo.InvariantCulture)] = budget.Amount;
            }
        }
        catch (HttpRequestException ex)
        {
            ApiStatusMessage = GetApiErrorMessage(ex);
        }
        catch (Exception ex)
        {
            ApiStatusMessage = $"{Texts.RequestErrorPrefix}: {ex.Message}";
        }
    }

    private async Task PrepareOnboardingAsync()
    {
        if (!IsCategorySettingsMode)
        {
            OnboardingAccountName = "Main";
            OnboardingSelectedCurrency = "UAH";
        }
        OnboardingNewCategoryName = string.Empty;
        OnboardingNewCategoryType = "expense";
        OnboardingStatusMessage = string.Empty;

        try
        {
            EnsureApiClient();
            var categories = await _apiClient!.GetUserCategoriesAsync();
            OnboardingCategories = new ObservableCollection<OnboardingCategoryOptionViewModel>(
                categories.Select(c => new OnboardingCategoryOptionViewModel(
                    c.Id,
                    c.Name,
                    c.Type,
                    GetOnboardingCategoryTypeDisplay(c.Type),
                    c.IsSelected)));
        }
        catch
        {
            OnboardingCategories = new ObservableCollection<OnboardingCategoryOptionViewModel>(
                apiCategories.Select(c => new OnboardingCategoryOptionViewModel(
                    c.Id,
                    c.Name,
                    c.Type,
                    GetOnboardingCategoryTypeDisplay(c.Type),
                    true)));
        }

        IsOnboardingDialogOpen = true;
    }

    private void InitializeLanguages()
    {
        LanguageOptions =
        [
            new LanguageOptionViewModel("ua", "Українська"),
            new LanguageOptionViewModel("en", "English")
        ];

        SelectedLanguage = LanguageOptions.FirstOrDefault(x => x.Code == "ua");
        UpdateLanguageOptionTitles();
    }

    private void InitializeThemes(string? selectedThemeName)
    {
        ThemeOptions =
        [
            new CategoryOptionViewModel(ThemeManager.Theme1, Texts.Theme1Name),
            new CategoryOptionViewModel(ThemeManager.Theme2, Texts.Theme2Name)
        ];

        var normalizedTheme = ThemeManager.NormalizeThemeName(selectedThemeName);
        SelectedTheme = ThemeOptions.FirstOrDefault(x => x.Code == normalizedTheme)
            ?? ThemeOptions.FirstOrDefault();
    }

    private void InitializeScheduledRepeatOptions()
    {
        ScheduledRepeatOptions =
        [
            new CategoryOptionViewModel("once", Texts.ScheduledRepeatOnce),
            new CategoryOptionViewModel("daily", Texts.ScheduledRepeatDaily),
            new CategoryOptionViewModel("weekly", Texts.ScheduledRepeatWeekly),
            new CategoryOptionViewModel("monthly", Texts.ScheduledRepeatMonthly),
            new CategoryOptionViewModel("yearly", Texts.ScheduledRepeatYearly),
            new CategoryOptionViewModel("custom", Texts.ScheduledRepeatCustom)
        ];

        SelectedScheduledRepeatOption = ScheduledRepeatOptions.FirstOrDefault(x => x.Code == "once");
    }

    private void InitializeScheduledCustomRepeatOptions()
    {
        ScheduledCustomRepeatUnitOptions =
        [
            new CategoryOptionViewModel("year", "Years"),
            new CategoryOptionViewModel("month", "Months"),
            new CategoryOptionViewModel("week", "Weeks"),
            new CategoryOptionViewModel("day", "Days")
        ];

        ResetScheduledCustomRepeatBuilder();
    }

    private void InitializeReportingOptions()
    {
        var now = DateTimeOffset.Now;
        ReportDateFrom = new DateTime(now.Year, now.Month, 1);
        ReportDateTo = now.DateTime;

        ReportViewOptions =
        [
            new CategoryOptionViewModel("category_period", "Звітність по обраній категорії за період часу"),
            new CategoryOptionViewModel("monthly_plan", "Планові витрати за обраний місяць"),
            new CategoryOptionViewModel("monthly_expenses", "Помісячні витрати за обраний період")
        ];
        SelectedReportView = ReportViewOptions.FirstOrDefault();
    }

    private void RefreshReportingSelections()
    {
        if (isRefreshingReportingSelections)
        {
            return;
        }

        isRefreshingReportingSelections = true;
        try
        {
            var expenseCategoryOptions = BuildCategoryOptions("expense").ToArray();
            ReportCategoryOptions = new ObservableCollection<CategoryOptionViewModel>(expenseCategoryOptions);
            SelectedReportCategory = expenseCategoryOptions.FirstOrDefault(c => c.Code == SelectedReportCategory?.Code)
                ?? expenseCategoryOptions.FirstOrDefault();
            RefreshReportingMetricsCore();
        }
        finally
        {
            isRefreshingReportingSelections = false;
        }
    }

    private void RefreshReportingMetricsOnly()
    {
        if (isRefreshingReportingSelections)
        {
            return;
        }

        isRefreshingReportingSelections = true;
        try
        {
            RefreshReportingMetricsCore();
        }
        finally
        {
            isRefreshingReportingSelections = false;
        }
    }

    private void RefreshReportingMetricsCore()
    {
        if (!ReportDateFrom.HasValue || !ReportDateTo.HasValue)
        {
            var now = DateTimeOffset.Now;
            ReportDateFrom = new DateTime(now.Year, now.Month, 1);
            ReportDateTo = now.DateTime;
        }

        var from = new DateTimeOffset(ReportDateFrom!.Value.Date);
        var to = new DateTimeOffset(ReportDateTo!.Value.Date.AddDays(1).AddTicks(-1));
        if (from > to)
        {
            (from, to) = (to, from);
            ReportDateFrom = from.Date;
            ReportDateTo = to.Date;
        }

        var categoryCode = SelectedReportCategory?.Code;
        var categoryTransactions = AllTransactions
            .Where(t => !t.IsIncome)
            .Where(t => t.TransactionDate >= from && t.TransactionDate <= to)
            .Where(t => string.Equals(t.CategoryCode, categoryCode, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        ReportSelectedCategoryExpenseTotal = categoryTransactions.Sum(t => Math.Abs(t.Amount));
        ReportSelectedCategoryTransactionCount = categoryTransactions.Length;

        var monthly = AllTransactions
            .Where(t => !t.IsIncome)
            .Where(t => t.TransactionDate >= from && t.TransactionDate <= to)
            .GroupBy(t => new { t.TransactionDate.Year, t.TransactionDate.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select((g, index) =>
            {
                var amount = g.Sum(t => Math.Abs(t.Amount));
                var monthLabel = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MM.yyyy", CultureInfo.InvariantCulture);
                var palette = GetExpenseChartPalette();
                var color = palette[index % palette.Length];
                return new CategoryLegendItemViewModel(
                    monthLabel,
                    amount,
                    new SolidColorBrush(Color.Parse(color)),
                    $"{amount:N2}");
            })
            .ToArray();
        ReportMonthlyExpenses = new ObservableCollection<CategoryLegendItemViewModel>(monthly);
    }

    private void UpdateLanguageOptionTitles()
    {
        foreach (var option in LanguageOptions)
        {
            option.DisplayName = option.Code == "ua"
                ? Texts.UkrainianLanguage
                : Texts.EnglishLanguage;
        }
    }

    private void UpdateThemeOptionTitles()
    {
        foreach (var option in ThemeOptions)
        {
            option.DisplayName = option.Code == ThemeManager.Theme1
                ? Texts.Theme1Name
                : Texts.Theme2Name;
        }
    }

    private void UpdateScheduledRepeatOptionTitles()
    {
        foreach (var option in ScheduledRepeatOptions)
        {
            option.DisplayName = option.Code switch
            {
                "daily" => Texts.ScheduledRepeatDaily,
                "weekly" => Texts.ScheduledRepeatWeekly,
                "monthly" => Texts.ScheduledRepeatMonthly,
                "yearly" => Texts.ScheduledRepeatYearly,
                "custom" => Texts.ScheduledRepeatCustom,
                _ => Texts.ScheduledRepeatOnce
            };
        }
    }

    private void ResetScheduledCustomRepeatBuilder()
    {
        SelectedScheduledCustomPrimaryUnit = ScheduledCustomRepeatUnitOptions.FirstOrDefault(x => x.Code == "day");
        SelectedScheduledCustomSecondaryUnit = null;
        SelectedScheduledCustomTertiaryUnit = null;
        SelectedScheduledCustomQuaternaryUnit = null;
        ScheduledCustomPrimaryValueInput = "1";
        ScheduledCustomSecondaryValueInput = string.Empty;
        ScheduledCustomTertiaryValueInput = string.Empty;
        ScheduledCustomQuaternaryValueInput = string.Empty;
    }

    private IEnumerable<CategoryOptionViewModel> BuildScheduledCustomRepeatUnitOptions(string? parentUnitCode)
    {
        if (string.IsNullOrWhiteSpace(parentUnitCode))
        {
            return [];
        }

        var index = GetScheduledCustomRepeatUnitOrder(parentUnitCode);
        return index < 0
            ? []
            : ScheduledCustomRepeatUnitOptions.Where(x => GetScheduledCustomRepeatUnitOrder(x.Code) > index).ToArray();
    }

    private void CoerceScheduledCustomRepeatSelection(int level)
    {
        if (level <= 1)
        {
            var secondaryOptions = ScheduledCustomSecondaryUnitOptions.ToArray();
            if (!HasScheduledCustomSecondaryUnit)
            {
                SelectedScheduledCustomSecondaryUnit = null;
                ScheduledCustomSecondaryValueInput = string.Empty;
            }
            else if (SelectedScheduledCustomSecondaryUnit is null || secondaryOptions.All(x => x.Code != SelectedScheduledCustomSecondaryUnit.Code))
            {
                SelectedScheduledCustomSecondaryUnit = secondaryOptions.FirstOrDefault();
            }
        }

        if (level <= 2)
        {
            var tertiaryOptions = ScheduledCustomTertiaryUnitOptions.ToArray();
            if (!HasScheduledCustomTertiaryUnit)
            {
                SelectedScheduledCustomTertiaryUnit = null;
                ScheduledCustomTertiaryValueInput = string.Empty;
            }
            else if (SelectedScheduledCustomTertiaryUnit is null || tertiaryOptions.All(x => x.Code != SelectedScheduledCustomTertiaryUnit.Code))
            {
                SelectedScheduledCustomTertiaryUnit = tertiaryOptions.FirstOrDefault();
            }
        }

        if (level <= 3)
        {
            var quaternaryOptions = ScheduledCustomQuaternaryUnitOptions.ToArray();
            if (!HasScheduledCustomQuaternaryUnit)
            {
                SelectedScheduledCustomQuaternaryUnit = null;
                ScheduledCustomQuaternaryValueInput = string.Empty;
            }
            else if (SelectedScheduledCustomQuaternaryUnit is null || quaternaryOptions.All(x => x.Code != SelectedScheduledCustomQuaternaryUnit.Code))
            {
                SelectedScheduledCustomQuaternaryUnit = quaternaryOptions.FirstOrDefault();
            }
        }
    }

    private void ApplyScheduledRepeatSelection(TimeSpan repeatInterval)
    {
        var normalizedDays = Math.Max(0, (int)Math.Round(repeatInterval.TotalDays));
        SelectedScheduledRepeatOption = normalizedDays switch
        {
            0 => ScheduledRepeatOptions.FirstOrDefault(x => x.Code == "once"),
            1 => ScheduledRepeatOptions.FirstOrDefault(x => x.Code == "daily"),
            7 => ScheduledRepeatOptions.FirstOrDefault(x => x.Code == "weekly"),
            >= 28 and <= 31 => ScheduledRepeatOptions.FirstOrDefault(x => x.Code == "monthly"),
            >= 365 and <= 366 => ScheduledRepeatOptions.FirstOrDefault(x => x.Code == "yearly"),
            _ => ScheduledRepeatOptions.FirstOrDefault(x => x.Code == "custom")
        };

        if (!string.Equals(SelectedScheduledRepeatOption?.Code, "custom", StringComparison.OrdinalIgnoreCase))
        {
            ResetScheduledCustomRepeatBuilder();
            return;
        }

        ApplyScheduledCustomRepeatFromDays(normalizedDays);
    }

    private void ApplyScheduledCustomRepeatFromDays(int totalDays)
    {
        var primaryUnit = totalDays >= 365 ? "year" : totalDays >= 30 ? "month" : totalDays >= 7 ? "week" : "day";
        var remainder = totalDays;

        ResetScheduledCustomRepeatBuilder();
        SelectedScheduledCustomPrimaryUnit = ScheduledCustomRepeatUnitOptions.FirstOrDefault(x => x.Code == primaryUnit);

        var allowedUnits = GetAllowedScheduledCustomRepeatUnits(primaryUnit);
        var parts = DecomposeScheduledCustomRepeatParts(remainder, allowedUnits);
        ApplyScheduledCustomRepeatParts(parts);
    }

    private bool TryBuildScheduledRepeatInterval(out TimeSpan repeatInterval)
    {
        if (!IsCustomScheduledRepeat)
        {
            repeatInterval = ParseStandardScheduledRepeatInterval(SelectedScheduledRepeatOption?.Code ?? "once");
            return true;
        }

        if (!TryBuildScheduledCustomRepeatInterval(out repeatInterval))
        {
            return false;
        }

        return true;
    }

    private bool TryBuildScheduledCustomRepeatInterval(out TimeSpan repeatInterval)
    {
        repeatInterval = TimeSpan.Zero;

        if (SelectedScheduledCustomPrimaryUnit is null ||
            !TryParseScheduledCustomRepeatValue(ScheduledCustomPrimaryValueInput, out var primaryValue) ||
            primaryValue <= 0)
        {
            return false;
        }

        var totalDays = primaryValue * GetScheduledCustomRepeatUnitDays(SelectedScheduledCustomPrimaryUnit.Code);

        if (!TryAccumulateScheduledCustomRepeatPart(SelectedScheduledCustomSecondaryUnit, ScheduledCustomSecondaryValueInput, ref totalDays) ||
            !TryAccumulateScheduledCustomRepeatPart(SelectedScheduledCustomTertiaryUnit, ScheduledCustomTertiaryValueInput, ref totalDays) ||
            !TryAccumulateScheduledCustomRepeatPart(SelectedScheduledCustomQuaternaryUnit, ScheduledCustomQuaternaryValueInput, ref totalDays))
        {
            return false;
        }

        if (totalDays <= 0)
        {
            return false;
        }

        repeatInterval = TimeSpan.FromDays(totalDays);
        ApplyNormalizedScheduledCustomRepeat(totalDays, SelectedScheduledCustomPrimaryUnit.Code);
        return true;
    }

    private void ApplyNormalizedScheduledCustomRepeat(int totalDays, string primaryUnitCode)
    {
        var parts = DecomposeScheduledCustomRepeatParts(totalDays, GetAllowedScheduledCustomRepeatUnits(primaryUnitCode));
        ApplyScheduledCustomRepeatParts(parts);
    }

    private void ApplyScheduledCustomRepeatParts(IReadOnlyList<(string UnitCode, int Value)> parts)
    {
        ResetScheduledCustomRepeatBuilder();

        if (parts.Count == 0)
        {
            return;
        }

        SelectedScheduledCustomPrimaryUnit = ScheduledCustomRepeatUnitOptions.FirstOrDefault(x => x.Code == parts[0].UnitCode);
        ScheduledCustomPrimaryValueInput = parts[0].Value.ToString(CultureInfo.InvariantCulture);

        if (parts.Count > 1)
        {
            SelectedScheduledCustomSecondaryUnit = ScheduledCustomRepeatUnitOptions.FirstOrDefault(x => x.Code == parts[1].UnitCode);
            ScheduledCustomSecondaryValueInput = parts[1].Value.ToString(CultureInfo.InvariantCulture);
        }

        if (parts.Count > 2)
        {
            SelectedScheduledCustomTertiaryUnit = ScheduledCustomRepeatUnitOptions.FirstOrDefault(x => x.Code == parts[2].UnitCode);
            ScheduledCustomTertiaryValueInput = parts[2].Value.ToString(CultureInfo.InvariantCulture);
        }

        if (parts.Count > 3)
        {
            SelectedScheduledCustomQuaternaryUnit = ScheduledCustomRepeatUnitOptions.FirstOrDefault(x => x.Code == parts[3].UnitCode);
            ScheduledCustomQuaternaryValueInput = parts[3].Value.ToString(CultureInfo.InvariantCulture);
        }
    }

    private static IReadOnlyList<string> GetAllowedScheduledCustomRepeatUnits(string primaryUnitCode)
    {
        return primaryUnitCode switch
        {
            "year" => ["year", "month", "week", "day"],
            "month" => ["month", "week", "day"],
            "week" => ["week", "day"],
            _ => ["day"]
        };
    }

    private static List<(string UnitCode, int Value)> DecomposeScheduledCustomRepeatParts(int totalDays, IReadOnlyList<string> allowedUnits)
    {
        var parts = new List<(string UnitCode, int Value)>();
        var remainder = totalDays;

        foreach (var unitCode in allowedUnits)
        {
            var unitDays = GetScheduledCustomRepeatUnitDays(unitCode);
            var value = remainder / unitDays;
            if (value <= 0)
            {
                continue;
            }

            parts.Add((unitCode, value));
            remainder %= unitDays;
        }

        if (parts.Count == 0)
        {
            parts.Add(("day", Math.Max(1, totalDays)));
        }

        return parts;
    }

    private static bool TryAccumulateScheduledCustomRepeatPart(CategoryOptionViewModel? unit, string rawValue, ref int totalDays)
    {
        if (unit is null || string.IsNullOrWhiteSpace(rawValue))
        {
            return true;
        }

        if (!TryParseScheduledCustomRepeatValue(rawValue, out var value) || value < 0)
        {
            return false;
        }

        totalDays += value * GetScheduledCustomRepeatUnitDays(unit.Code);
        return true;
    }

    private static bool TryParseScheduledCustomRepeatValue(string rawValue, out int value) =>
        int.TryParse(rawValue.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    private static int GetScheduledCustomRepeatUnitOrder(string unitCode) => unitCode switch
    {
        "year" => 0,
        "month" => 1,
        "week" => 2,
        "day" => 3,
        _ => -1
    };

    private static int GetScheduledCustomRepeatUnitDays(string unitCode) => unitCode switch
    {
        "year" => 365,
        "month" => 30,
        "week" => 7,
        _ => 1
    };

    private static TimeSpan ParseStandardScheduledRepeatInterval(string value) => value switch
    {
        "daily" => TimeSpan.FromDays(1),
        "weekly" => TimeSpan.FromDays(7),
        "monthly" => TimeSpan.FromDays(30),
        "yearly" => TimeSpan.FromDays(365),
        _ => TimeSpan.Zero
    };

    private void ResetTransactionForm()
    {
        ActiveTransaction = null;
        EditingTransactionId = null;
        EditingScheduledPaymentId = null;
        TransactionType = "expense";
        TransactionAmountInput = string.Empty;
        TransactionCurrencyInput = "UAH";
        TransactionDescriptionInput = string.Empty;
        IsTransactionSharedInput = false;
        SelectedTransactionShareGroup = null;
        var now = DateTimeOffset.Now;
        TransactionDateInput = now.ToString(DateDisplayFormat);
        TransactionSelectedDate = now;
        TransactionSelectedTime = now.TimeOfDay;
        TransactionTimeInput = now.ToString("HH:mm");
        ResetScheduledCustomRepeatBuilder();
        UpdateTransactionCategoryOptions();
    }

    private void UpdateTransactionSelectedDateFromParts()
    {
        if (isSyncingTransactionDateState || TransactionSelectedYear < 1 || TransactionSelectedMonth is < 1 or > 12)
        {
            return;
        }

        var clampedDay = Math.Min(Math.Max(TransactionSelectedDay, 1), DateTime.DaysInMonth(TransactionSelectedYear, TransactionSelectedMonth));
        if (clampedDay != TransactionSelectedDay)
        {
            TransactionSelectedDay = clampedDay;
            return;
        }

        isSyncingTransactionDateState = true;
        try
        {
            var current = TransactionSelectedDate ?? DateTimeOffset.Now;
            TransactionSelectedDate = new DateTimeOffset(
                TransactionSelectedYear,
                TransactionSelectedMonth,
                clampedDay,
                current.Hour,
                current.Minute,
                current.Second,
                current.Offset);
        }
        finally
        {
            isSyncingTransactionDateState = false;
        }
    }

    private void ResetBankAccountForm()
    {
        EditingBankAccountId = null;
        BankAccountNameInput = string.Empty;
        BankAccountCurrencyInput = "UAH";
        BankAccountBalanceInput = "0";
        IsBankAccountDefaultInput = false;
        IsBankAccountSharedInput = false;
        SelectedBankAccountShareGroup = null;
        BankAccountStatusMessage = string.Empty;
    }

    private async Task<bool> EnsureBankAccountExistsAsync(string promptMessage)
    {
        if (BankAccounts.Count == 0 && IsApiAuthorized)
        {
            await LoadBankAccountsAsync();
        }

        if (BankAccounts.Count > 0)
        {
            return true;
        }

        MissingBankAccountPromptMessage = promptMessage;
        IsMissingBankAccountPromptOpen = true;
        return false;
    }

    private void RefreshTransactionItems()
    {
        if (AllTransactions.Count == 0)
        {
            return;
        }

        AllTransactions = new ObservableCollection<TransactionItemViewModel>(
            AllTransactions.Select(t => t with
            {
                TypeDisplay = t.Type == "income" ? Texts.TransactionIncomeType : Texts.TransactionExpenseType,
                CategoryDisplay = GetCategoryDisplayName(t.CategoryCode),
                CategoryIconKey = GetCategoryIconKey(t.CategoryCode),
                CategoryColor = GetCategoryColor(t.CategoryCode)
            }));
        LatestExpenses = new ObservableCollection<TransactionItemViewModel>(
            LatestExpenses.Select(t => t with
            {
                TypeDisplay = t.Type == "income" ? Texts.TransactionIncomeType : Texts.TransactionExpenseType,
                CategoryDisplay = GetCategoryDisplayName(t.CategoryCode),
                CategoryIconKey = GetCategoryIconKey(t.CategoryCode),
                CategoryColor = GetCategoryColor(t.CategoryCode)
            }));
        homeTransactionSource = homeTransactionSource
            .Select(t => t with
            {
                TypeDisplay = t.Type == "income" ? Texts.TransactionIncomeType : Texts.TransactionExpenseType,
                CategoryDisplay = GetCategoryDisplayName(t.CategoryCode),
                CategoryIconKey = GetCategoryIconKey(t.CategoryCode),
                CategoryColor = GetCategoryColor(t.CategoryCode)
            })
            .ToArray();
        ApplyTransactionFilters();
        UpdateVisibleTransactions();
        UpdateCharts();
        OnPropertyChanged(nameof(HasTransactions));
        OnPropertyChanged(nameof(HasNoTransactions));
        OnPropertyChanged(nameof(HasAllTransactions));
    }

    [RelayCommand]
    private void OpenAllTransactions()
    {
        OpenAllTransactionsTab();
    }

    [RelayCommand]
    private void CloseAllTransactions()
    {
        CurrentPage = HomePage;
    }

    [RelayCommand]
    public void LoadMoreFilteredTransactions()
    {
        if (filteredTransactionSource.Count <= FilteredTransactions.Count)
        {
            return;
        }

        filteredTransactionTakeCount += AllTransactionsPageSize;
        RefreshFilteredTransactionsPage();
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

    [RelayCommand]
    private async Task OpenCategorySettingsAsync()
    {
        IsCategoryDetailsOpen = false;
        IsCategorySettingsMode = false;
        await OpenCategoriesAsync();
    }

    [RelayCommand]
    private void CloseCategoryDetails()
    {
        IsCategoryDetailsOpen = false;
        IsCategorySettingsMode = false;
    }

    [RelayCommand]
    private async Task UpdateCategoryLimitAsync(CategoryDetailItemViewModel? category)
    {
        if (category is null || !category.TryApplyLimit() || !int.TryParse(category.CategoryCode, NumberStyles.Integer, CultureInfo.InvariantCulture, out var categoryId))
        {
            return;
        }

        try
        {
            EnsureApiClient();
            var budget = await _apiClient!.UpsertBudgetAsync(
                categoryId,
                new UpsertBudgetRequestDto(
                    SelectedBankAccount?.GroupId,
                    category.Limit,
                    TimeSpan.FromDays(30),
                    DateOnly.FromDateTime(DateTime.Today),
                    true));

            categoryLimits[budget.CategoryId.ToString(CultureInfo.InvariantCulture)] = budget.Amount;
            UpdateCategoryDetails();
        }
        catch (HttpRequestException ex)
        {
            ApiStatusMessage = GetApiErrorMessage(ex);
        }
        catch (Exception ex)
        {
            ApiStatusMessage = $"{Texts.RequestErrorPrefix}: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SaveCategoryDetailsAsync()
    {
        if (IsApiBusy)
        {
            return;
        }

        var categories = CategoryDetails.ToArray();
        if (categories.Length == 0)
        {
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();

            foreach (var category in categories)
            {
                if (!category.TryApplyLimit() ||
                    !int.TryParse(category.CategoryCode, NumberStyles.Integer, CultureInfo.InvariantCulture, out var categoryId))
                {
                    continue;
                }

                var budget = await _apiClient!.UpsertBudgetAsync(
                    categoryId,
                    new UpsertBudgetRequestDto(
                        SelectedBankAccount?.GroupId,
                        category.Limit,
                        TimeSpan.FromDays(30),
                        DateOnly.FromDateTime(DateTime.Today),
                        true));

                categoryLimits[budget.CategoryId.ToString(CultureInfo.InvariantCulture)] = budget.Amount;
            }

            UpdateCategoryDetails();
        }
        catch (HttpRequestException ex)
        {
            ApiStatusMessage = GetApiErrorMessage(ex);
        }
        catch (Exception ex)
        {
            ApiStatusMessage = $"{Texts.RequestErrorPrefix}: {ex.Message}";
        }
        finally
        {
            IsApiBusy = false;
        }
    }

    [RelayCommand]
    private void CloseTransactionDialog()
    {
        IsTransactionDialogOpen = false;
        ResetTransactionForm();
    }

    private void LoadServerSettingsFromBaseUrl()
    {
        if (!Uri.TryCreate(ApiBaseUrl, UriKind.Absolute, out var uri))
        {
            ServerHost = "http://localhost";
            ServerPort = "5174";
            return;
        }

        ServerHost = $"{uri.Scheme}://{uri.Host}";
        ServerPort = uri.IsDefaultPort ? string.Empty : uri.Port.ToString(CultureInfo.InvariantCulture);
    }

    private void SaveLocalUserSettings()
    {
        UserSettingsStore.Save(new UserSettings
        {
            ApiBaseUrl = ApiBaseUrl.Trim().TrimEnd('/'),
            LastLogin = ApiLogin.Trim(),
            ThemeName = SelectedTheme?.Code ?? ThemeManager.Theme1
        });
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
                MinLimit = 0
            }
        ];

        OnPropertyChanged(nameof(Series));
        OnPropertyChanged(nameof(Chart));
        OnPropertyChanged(nameof(TransactionTimelineXAxes));
        OnPropertyChanged(nameof(TransactionTimelineYAxes));
        OnPropertyChanged(nameof(ExpenseCategoryLegend));
        OnPropertyChanged(nameof(CategoryDetails));
        OnPropertyChanged(nameof(HasTransactions));
        OnPropertyChanged(nameof(HasNoTransactions));
        OnPropertyChanged(nameof(HasAllTransactions));
        OnPropertyChanged(nameof(HasExpenseTransactions));
        OnPropertyChanged(nameof(HasNoExpenseTransactions));
    }

    private string[] GetExpenseChartPalette() =>
        string.Equals(SelectedTheme?.Code, ThemeManager.Theme2, StringComparison.OrdinalIgnoreCase)
            ? Theme2ExpenseChartPalette
            : ExpenseChartPalette;

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

    private void UpdateTransactionCategoryFilterOptions()
    {
        var selectedCode = SelectedTransactionCategoryFilter?.Code;
        var options = string.Equals(SelectedTransactionTypeFilter, "all", StringComparison.OrdinalIgnoreCase)
            ? BuildCategoryOptions("expense").Concat(BuildCategoryOptions("income")).ToList()
            : BuildCategoryOptions(SelectedTransactionTypeFilter == "income" ? "income" : "expense").ToList();
        options.Insert(0, new CategoryOptionViewModel("all", Texts.TransactionCategoryLabel));
        TransactionCategoryFilterOptions = new ObservableCollection<CategoryOptionViewModel>(options);
        SelectedTransactionCategoryFilter = options.FirstOrDefault(x => x.Code == selectedCode)
            ?? options.FirstOrDefault();
    }

    private void UpdateTransactionAccountFilterOptions()
    {
        var selectedAccountId = SelectedTransactionAccountFilter?.AccountId;
        var options = BankAccounts
            .Select(account => new BankAccountFilterOptionViewModel(account.Id, account.Name))
            .ToList();
        options.Insert(0, new BankAccountFilterOptionViewModel(null, Texts.AllAccountsFilter));

        TransactionAccountFilterOptions = new ObservableCollection<BankAccountFilterOptionViewModel>(options);
        SelectedTransactionAccountFilter = options.FirstOrDefault(x => x.AccountId == selectedAccountId)
            ?? options.FirstOrDefault();
    }

    private void ApplyTransactionFilters()
    {
        var search = TransactionSearchText.Trim();
        var categoryCode = SelectedTransactionCategoryFilter?.Code;
        var accountId = SelectedTransactionAccountFilter?.AccountId;
        var filtered = LatestExpenses.AsEnumerable();
        filteredTransactionTakeCount = AllTransactionsPageSize;

        if (!string.Equals(SelectedTransactionTypeFilter, "all", StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(t => string.Equals(t.Type, SelectedTransactionTypeFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(categoryCode) && categoryCode != "all")
        {
            filtered = filtered.Where(t => string.Equals(t.CategoryCode, categoryCode, StringComparison.OrdinalIgnoreCase));
        }

        if (accountId.HasValue)
        {
            filtered = filtered.Where(t => t.AccountId == accountId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            filtered = filtered.Where(t =>
                t.DescriptionDisplay.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                t.CategoryDisplay.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                t.AmountDisplay.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                t.Currency.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                t.TransactionDateDisplay.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        filteredTransactionSource = filtered
            .OrderByDescending(t => t.TransactionDate)
            .ToArray();
        RefreshFilteredTransactionsPage();
    }

    private void RefreshFilteredTransactionsPage()
    {
        FilteredTransactions = new ObservableCollection<TransactionItemViewModel>(
            filteredTransactionSource.Take(filteredTransactionTakeCount));
    }

    private void UpdateCategoryDetails()
    {
        var categoryDetails = apiCategories.Count > 0
            ? apiCategories
                .Where(c => string.Equals(c.Type, "expense", StringComparison.OrdinalIgnoreCase))
                .OrderBy(c => c.Type)
                .ThenBy(c => c.Name)
                .Select((c, index) => CreateCategoryDetail(c.Id.ToString(CultureInfo.InvariantCulture), c.Name, c.Type, index))
            : BuildCategoryOptions("expense")
                .Select((c, index) => CreateCategoryDetail(c.Code, c.DisplayName, "expense", index));

        CategoryDetails = new ObservableCollection<CategoryDetailItemViewModel>(
            categoryDetails
                .OrderBy(c => c.RiskSortRank)
                .ThenBy(c => c.RiskSortRank == 1 ? c.RemainingAmount : 0)
                .ThenByDescending(c => c.RiskSortRank == 2 ? c.Amount : 0)
                .ThenByDescending(c => c.RiskSortRank is 0 or 1 ? c.Limit : 0)
                .ThenBy(c => c.Name));
        OnPropertyChanged(nameof(CategoryDetails));
    }

    private CategoryDetailItemViewModel CreateCategoryDetail(string categoryCode, string name, string type, int index)
    {
        var transactions = LatestExpenses
            .Where(t => string.Equals(t.CategoryCode, categoryCode, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var amount = transactions.Sum(t => t.Amount);
        var typeDisplay = string.Equals(type, "income", StringComparison.OrdinalIgnoreCase)
            ? Texts.TransactionIncomeType
            : Texts.TransactionExpenseType;
        var limit = categoryLimits.TryGetValue(categoryCode, out var storedLimit)
            ? storedLimit
            : 0;
        var color = GetExpenseCategoryChartColor(transactions, categoryCode, index);

        return new CategoryDetailItemViewModel(
            categoryCode,
            name,
            typeDisplay,
            transactions.Length,
            amount,
            limit,
            new SolidColorBrush(Color.Parse(color)));
    }

    private string GetAccountCurrency(Guid accountId)
    {
        return BankAccounts.FirstOrDefault(a => a.Id == accountId)?.Currency
            ?? SelectedBankAccount?.Currency
            ?? TransactionCurrencyInput.Trim().ToUpperInvariant();
    }

    private static string FormatTransactionTotals(IEnumerable<TransactionItemViewModel> transactions)
    {
        var totals = transactions
            .GroupBy(t => t.Currency)
            .Select(g => $"{FormatMoneyAmount(g.Sum(t => t.Amount))} {g.Key}")
            .ToArray();

        return totals.Length == 0 ? "0.00" : string.Join(" / ", totals);
    }

    private string FormatTransactionBalance()
    {
        var totals = LatestExpenses
            .GroupBy(t => t.Currency)
            .Select(g => $"{FormatMoneyAmount(g.Sum(t => t.IsIncome ? t.Amount : -t.Amount))} {g.Key}")
            .ToArray();

        return totals.Length == 0 ? "0.00" : string.Join(" / ", totals);
    }

    private static string FormatMoneyAmount(decimal amount) =>
        decimal.Truncate(amount) == amount
            ? amount.ToString("#,##0", CultureInfo.InvariantCulture)
            : amount.ToString("#,##0.##", CultureInfo.InvariantCulture);

    private void UpdateTransactionCategoryOptions(string? selectedCode = null)
    {
        var options = BuildCategoryOptions(TransactionType);
        TransactionCategoryOptions = new ObservableCollection<CategoryOptionViewModel>(options);
        SelectedTransactionCategory = TransactionCategoryOptions.FirstOrDefault(x => x.Code == selectedCode)
            ?? TransactionCategoryOptions.FirstOrDefault();
    }

    private void UpdateScheduledPaymentCategoryOptions(string? selectedCode = null)
    {
        var options = BuildCategoryOptions(ScheduledPaymentType);
        ScheduledPaymentCategoryOptions = new ObservableCollection<CategoryOptionViewModel>(options);
        SelectedScheduledPaymentCategory = ScheduledPaymentCategoryOptions.FirstOrDefault(x => x.Code == selectedCode)
            ?? ScheduledPaymentCategoryOptions.FirstOrDefault();
    }

    private async Task<Guid> EnsureDefaultAccountAsync()
    {
        if (defaultAccountId.HasValue)
        {
            return defaultAccountId.Value;
        }

        EnsureApiClient();
        var accounts = await _apiClient!.GetBankAccountsAsync();
        var account = accounts.FirstOrDefault(a => a.IsDefault) ?? accounts.FirstOrDefault();

        if (account is null)
        {
            account = await _apiClient.CreateBankAccountAsync(new CreateBankAccountRequestDto(
                "Main",
                TransactionCurrencyInput.Trim().ToUpperInvariant(),
                0,
                true));
        }

        defaultAccountId = account.Id;
        TransactionCurrencyInput = account.Currency;
        return account.Id;
    }

    private IEnumerable<CategoryOptionViewModel> BuildCategoryOptions(string transactionType)
    {
        if (apiCategories.Count > 0)
        {
            return apiCategories
                .Where(c => string.Equals(c.Type, transactionType, StringComparison.OrdinalIgnoreCase))
                .Where(c => selectedCategoryIds is null || selectedCategoryIds.Contains(c.Id))
                .Select(c => new CategoryOptionViewModel(c.Id.ToString(CultureInfo.InvariantCulture), c.Name))
                .ToArray();
        }

        if (transactionType == "income")
        {
            return
            [
                new CategoryOptionViewModel("salary", Texts.CategorySalary),
                new CategoryOptionViewModel("freelance", Texts.CategoryFreelance),
                new CategoryOptionViewModel("other_income", Texts.CategoryOtherIncome)
            ];
        }

        return
        [
            new CategoryOptionViewModel("groceries", Texts.CategoryGroceries),
            new CategoryOptionViewModel("transport", Texts.CategoryTransport),
            new CategoryOptionViewModel("housing", Texts.CategoryHousing),
            new CategoryOptionViewModel("entertainment", Texts.CategoryEntertainment),
            new CategoryOptionViewModel("health", Texts.CategoryHealth),
            new CategoryOptionViewModel("shopping", Texts.CategoryShopping),
            new CategoryOptionViewModel("education", Texts.CategoryEducation),
            new CategoryOptionViewModel("other_expense", Texts.CategoryOtherExpense)
        ];
    }

    private string GetCategoryDisplayName(string? categoryCode)
    {
        if (int.TryParse(categoryCode, NumberStyles.Integer, CultureInfo.InvariantCulture, out var categoryId))
        {
            var apiCategory = apiCategories.FirstOrDefault(c => c.Id == categoryId);
            if (apiCategory is not null)
            {
                return apiCategory.Name;
            }

            categoryCode = GetCategoryCode(categoryId);
        }

        return categoryCode switch
        {
            "groceries" => Texts.CategoryGroceries,
            "transport" => Texts.CategoryTransport,
            "housing" => Texts.CategoryHousing,
            "entertainment" => Texts.CategoryEntertainment,
            "health" => Texts.CategoryHealth,
            "shopping" => Texts.CategoryShopping,
            "education" => Texts.CategoryEducation,
            "other_expense" => Texts.CategoryOtherExpense,
            "salary" => Texts.CategorySalary,
            "freelance" => Texts.CategoryFreelance,
            "other_income" => Texts.CategoryOtherIncome,
            _ => Texts.NoCategoryText
        };
    }

    private string GetCategoryIconKey(string? categoryCode)
    {
        if (int.TryParse(categoryCode, NumberStyles.Integer, CultureInfo.InvariantCulture, out var categoryId))
        {
            var apiCategory = apiCategories.FirstOrDefault(c => c.Id == categoryId);
            if (apiCategory is not null)
            {
                return apiCategory.IconKey;
            }

            categoryCode = GetCategoryCode(categoryId);
        }

        return categoryCode switch
        {
            "groceries" => "groceries",
            "transport" => "transport",
            "housing" => "home",
            "entertainment" => "entertainment",
            "health" => "health",
            "shopping" => "shopping",
            "education" => "education",
            "salary" => "income",
            "freelance" => "work",
            "other_income" => "income",
            _ => "other"
        };
    }

    private string GetCategoryColor(string? categoryCode)
    {
        if (int.TryParse(categoryCode, NumberStyles.Integer, CultureInfo.InvariantCulture, out var categoryId))
        {
            var apiCategory = apiCategories.FirstOrDefault(c => c.Id == categoryId);
            var categoryColor = apiCategory?.Color?.Trim();
            if (Color.TryParse(categoryColor, out _))
            {
                return categoryColor!;
            }
        }

        return "#E5E7EB";
    }

    private static int GetCategoryId(string categoryCode)
    {
        if (int.TryParse(categoryCode, NumberStyles.Integer, CultureInfo.InvariantCulture, out var categoryId))
        {
            return categoryId;
        }

        return categoryCode switch
        {
            "groceries" => 1,
            "transport" => 2,
            "housing" => 3,
            "entertainment" => 4,
            "health" => 5,
            "shopping" => 6,
            "education" => 7,
            "salary" => 8,
            "freelance" => 9,
            "other_income" => 10,
            _ => 11
        };
    }

    private static string GetCategoryCode(int categoryId) =>
        categoryId switch
        {
            1 => "groceries",
            2 => "transport",
            3 => "housing",
            4 => "entertainment",
            5 => "health",
            6 => "shopping",
            7 => "education",
            8 => "salary",
            9 => "freelance",
            10 => "other_income",
            _ => "other_expense"
        };

    private string GetTransactionType(int categoryId)
    {
        var apiCategory = apiCategories.FirstOrDefault(c => c.Id == categoryId);
        if (apiCategory is not null)
        {
            return string.Equals(apiCategory.Type, "income", StringComparison.OrdinalIgnoreCase)
                ? "income"
                : "expense";
        }

        return categoryId is >= 8 and <= 10 ? "income" : "expense";
    }

    private static bool TryParseTransactionDate(string rawValue, out DateTimeOffset value)
    {
        if (DateTime.TryParseExact(
                rawValue.Trim(),
                [DateTimeDisplayFormat, DateDisplayFormat, "yyyy-MM-dd HH:mm", "yyyy-MM-dd"],
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal,
                out var localDateTime) ||
            DateTime.TryParse(
                rawValue,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal,
                out localDateTime))
        {
            value = new DateTimeOffset(localDateTime);
            return true;
        }

        value = default;
        return false;
    }

    private static bool TryParseDateOnly(string rawValue, out DateOnly value)
    {
        if (DateTime.TryParseExact(
                rawValue.Trim(),
                [DateTimeDisplayFormat, "yyyy-MM-dd HH:mm"],
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal,
                out var dateTime))
        {
            value = DateOnly.FromDateTime(dateTime);
            return true;
        }

        return DateOnly.TryParseExact(rawValue.Trim(), [DateDisplayFormat, "yyyy-MM-dd"], CultureInfo.InvariantCulture, DateTimeStyles.None, out value) ||
               DateOnly.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
    }

    private static bool TryBuildTransactionDateTime(DateTimeOffset? selectedDate, TimeSpan? selectedTime, out DateTimeOffset value)
    {
        if (!selectedDate.HasValue)
        {
            value = default;
            return false;
        }

        if (!selectedTime.HasValue)
        {
            value = default;
            return false;
        }

        var date = selectedDate.Value.Date;
        var time = selectedTime.Value;
        var localDateTime = new DateTime(date.Year, date.Month, date.Day, time.Hours, time.Minutes, 0, DateTimeKind.Unspecified);
        value = new DateTimeOffset(localDateTime, TimeZoneInfo.Local.GetUtcOffset(localDateTime));
        return true;
    }

    partial void OnScheduledPaymentsChanged(ObservableCollection<ScheduledPaymentItemViewModel> value)
    {
        var hasIncome = value.Any(p => string.Equals(p.Type, "income", StringComparison.OrdinalIgnoreCase));
        var hasExpense = value.Any(p => string.Equals(p.Type, "expense", StringComparison.OrdinalIgnoreCase));

        var scheduledPaymentTypeOptions = new List<CategoryOptionViewModel>
        {
            new("all", Texts.ScheduledPaymentsFilterAll)
        };

        if (hasExpense)
        {
            scheduledPaymentTypeOptions.Add(new CategoryOptionViewModel("expense", Texts.TransactionExpenseType));
        }

        if (hasIncome)
        {
            scheduledPaymentTypeOptions.Add(new CategoryOptionViewModel("income", Texts.TransactionIncomeType));
        }

        ScheduledPaymentTypeFilterOptions = new ObservableCollection<CategoryOptionViewModel>(scheduledPaymentTypeOptions);
        SelectedScheduledPaymentTypeFilter = ScheduledPaymentTypeFilterOptions
            .FirstOrDefault(x => x.Code == (SelectedScheduledPaymentTypeFilter?.Code ?? "all"))
            ?? ScheduledPaymentTypeFilterOptions.FirstOrDefault();

        ScheduledPaymentCurrencyFilterOptions = new ObservableCollection<CategoryOptionViewModel>(
        [
            new CategoryOptionViewModel("all", Texts.ScheduledPaymentsFilterAll),
            .. value.Select(p => p.Currency)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c)
                .Select(c => new CategoryOptionViewModel(c, c))
        ]);
        SelectedScheduledPaymentCurrencyFilter = ScheduledPaymentCurrencyFilterOptions
            .FirstOrDefault(x => x.Code == (SelectedScheduledPaymentCurrencyFilter?.Code ?? "all"))
            ?? ScheduledPaymentCurrencyFilterOptions.FirstOrDefault();

        ScheduledPaymentCategoryFilterOptions = new ObservableCollection<CategoryOptionViewModel>(
        [
            new CategoryOptionViewModel("all", Texts.ScheduledPaymentsFilterAll),
            .. value.GroupBy(p => p.CategoryId)
                .OrderBy(g => g.First().CategoryDisplay)
                .Select(g => new CategoryOptionViewModel(g.Key.ToString(CultureInfo.InvariantCulture), g.First().CategoryDisplay))
        ]);
        SelectedScheduledPaymentCategoryFilter = ScheduledPaymentCategoryFilterOptions
            .FirstOrDefault(x => x.Code == (SelectedScheduledPaymentCategoryFilter?.Code ?? "all"))
            ?? ScheduledPaymentCategoryFilterOptions.FirstOrDefault();

        OnPropertyChanged(nameof(ScheduledPaymentsTotalCount));
        OnPropertyChanged(nameof(ScheduledPaymentsPaidThisMonthCount));
        OnPropertyChanged(nameof(ScheduledPaymentsUnpaidCount));
        OnPropertyChanged(nameof(ScheduledPaymentsOverdueCount));
        OnPropertyChanged(nameof(ScheduledPaymentsUpcomingCount));
        OnPropertyChanged(nameof(ScheduledPaymentCategoryPillWidth));
        OnPropertyChanged(nameof(HasScheduledPaymentTypeFilter));
        OnPropertyChanged(nameof(FilteredScheduledPayments));
    }

    partial void OnScheduledPaymentsFilterChanged(string value)
    {
        OnPropertyChanged(nameof(FilteredScheduledPayments));
        OnPropertyChanged(nameof(IsAllScheduledPaymentsFilterSelected));
        OnPropertyChanged(nameof(IsFutureScheduledPaymentsFilterSelected));
        OnPropertyChanged(nameof(IsOverdueScheduledPaymentsFilterSelected));
    }

    partial void OnSelectedScheduledPaymentCurrencyFilterChanged(CategoryOptionViewModel? value)
    {
        OnPropertyChanged(nameof(FilteredScheduledPayments));
    }

    partial void OnSelectedScheduledPaymentTypeFilterChanged(CategoryOptionViewModel? value)
    {
        OnPropertyChanged(nameof(FilteredScheduledPayments));
    }

    partial void OnSelectedScheduledPaymentCategoryFilterChanged(CategoryOptionViewModel? value)
    {
        OnPropertyChanged(nameof(FilteredScheduledPayments));
    }

    private bool IsPaidScheduledPaymentThisMonth(TransactionItemViewModel transaction)
    {
        var localTransactionDate = transaction.TransactionDate.ToLocalTime();
        var now = DateTimeOffset.Now;
        if (localTransactionDate.Year != now.Year || localTransactionDate.Month != now.Month)
        {
            return false;
        }

        return ScheduledPayments.Any(payment =>
            payment.RecurringPaymentId > 0 &&
            payment.AccountId == transaction.AccountId &&
            payment.Amount == transaction.Amount &&
            (string.Equals(payment.Name, transaction.Description, StringComparison.OrdinalIgnoreCase) ||
             (!string.IsNullOrWhiteSpace(payment.Description) &&
              string.Equals(payment.Description, transaction.Description, StringComparison.OrdinalIgnoreCase))));
    }
}
