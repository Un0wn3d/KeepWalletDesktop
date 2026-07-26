using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeepWalletDesktop.Models;
using KeepWalletDesktop.Services;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.ImageFilters;
using SkiaSharp;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace KeepWalletDesktop.ViewModels;

public partial class MainViewModel
{
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
        OnPropertyChanged(nameof(IsLogsPage));
    }

    partial void OnUserLogsChanged(ObservableCollection<AuditLogItemViewModel> value) => RefreshLogFilters();
    partial void OnSelectedLogUserFilterChanged(string value) => RefreshPagedLogResults(resetVisibleCount: true);
    partial void OnSelectedLogActionFilterChanged(string value) => RefreshPagedLogResults(resetVisibleCount: true);
    partial void OnLogDateFromFilterChanged(DateTime? value) => RefreshPagedLogResults(resetVisibleCount: true);
    partial void OnLogDateToFilterChanged(DateTime? value) => RefreshPagedLogResults(resetVisibleCount: true);
    partial void OnVisibleLogCountChanged(int value) { }

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
        RefreshGroupAccessProperties();
        OnPropertyChanged(nameof(CompatibleSavingsBankAccounts));
        OnPropertyChanged(nameof(BankAccountGroupPillWidth));
        RefreshTransactionFilterOptions();
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
        OnPropertyChanged(nameof(TransactionGroupPillWidth));
        OnPropertyChanged(nameof(ScheduledPaymentsPaidThisMonthCount));
        OnPropertyChanged(nameof(ScheduledPaymentsPaidThisMonthAmountDisplay));
        RefreshTransactionTypeFilterOptions();
        RefreshTransactionFilterOptions();
        ApplyTransactionFilters();
        RefreshGroupAccessProperties();
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
        OnPropertyChanged(nameof(ActiveSavings));
        OnPropertyChanged(nameof(CompletedSavings));
        OnPropertyChanged(nameof(HasActiveSavings));
        OnPropertyChanged(nameof(HasCompletedSavings));
        RefreshActiveSavingShareProperties();
        RefreshGroupAccessProperties();
    }

    partial void OnGroupsChanged(ObservableCollection<GroupItemViewModel> value)
    {
        OnPropertyChanged(nameof(HasGroups));
        OnPropertyChanged(nameof(HasNoGroups));
        RefreshActiveSavingShareProperties();
        RefreshGroupAccessProperties();
    }

    partial void OnActiveSavingIdChanged(int? value) => RefreshActiveSavingShareProperties();

    partial void OnVisibleTransactionsChanged(ObservableCollection<TransactionItemViewModel> value)
    {
        OnPropertyChanged(nameof(TransactionCategoryPillWidth));
        OnPropertyChanged(nameof(TransactionGroupPillWidth));
    }

    partial void OnFilteredTransactionsChanged(ObservableCollection<TransactionItemViewModel> value)
    {
        OnPropertyChanged(nameof(TransactionCategoryPillWidth));
        OnPropertyChanged(nameof(TransactionGroupPillWidth));
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
        OnPropertyChanged(nameof(CanShowAuthFooterAction));
    }

    partial void OnIsRegistrationEnabledChanged(bool value)
    {
        if (!value && IsRegisterMode)
        {
            IsRegisterMode = false;
        }

        OnPropertyChanged(nameof(AuthFooterPrompt));
        OnPropertyChanged(nameof(AuthFooterAction));
    }

    partial void OnIsCategorySettingsModeChanged(bool value) => OnPropertyChanged(nameof(IsOnboardingAccountSetupVisible));

    partial void OnCategoryNewTypeChanged(string value)
    {
        OnPropertyChanged(nameof(CategoryNewIconGlyph));
        OnPropertyChanged(nameof(CategoryNewIconSampleImage));
        OnPropertyChanged(nameof(HasCategoryNewIconSampleImage));
        OnPropertyChanged(nameof(IsNewCategoryExpenseTypeSelected));
        OnPropertyChanged(nameof(IsNewCategoryIncomeTypeSelected));
    }

    partial void OnEditingTransactionIdChanged(int? value) => RefreshTransactionDialogStateProperties();
    partial void OnEditingScheduledPaymentIdChanged(int? value) => RefreshTransactionDialogStateProperties();

    partial void OnGoalAmountChanged(double value) => RefreshSavingsProperties();
    partial void OnSavedAmountChanged(double value) => RefreshSavingsProperties();

    partial void OnTransactionTypeChanged(string value)
    {
        UpdateTransactionCategoryOptions(SelectedTransactionCategory?.Code);
        OnPropertyChanged(nameof(TransactionTypeDisplay));
        OnPropertyChanged(nameof(IsExpenseTransactionTypeSelected));
        OnPropertyChanged(nameof(IsIncomeTransactionTypeSelected));
    }

    partial void OnTransactionAmountInputChanged(string value)
    {
        if (isNormalizingTransactionAmountInput || string.IsNullOrEmpty(value) || !value.Contains(','))
        {
            return;
        }

        try
        {
            isNormalizingTransactionAmountInput = true;
            TransactionAmountInput = value.Replace(',', '.');
        }
        finally
        {
            isNormalizingTransactionAmountInput = false;
        }
    }

    partial void OnTransactionSearchTextChanged(string value) => ScheduleTransactionFilters();

    partial void OnSelectedTransactionTypeFilterChanged(CategoryOptionViewModel? value)
    {
        RefreshTransactionFilterOptions();
        ScheduleTransactionFilters();
    }

    partial void OnSelectedTransactionAccountFilterChanged(BankAccountFilterOptionViewModel? value) => ScheduleTransactionFilters();
    partial void OnSelectedTransactionCategoryFilterChanged(CategoryOptionViewModel? value) => ScheduleTransactionFilters();
    partial void OnSelectedTransactionCurrencyFilterChanged(CategoryOptionViewModel? value) => ScheduleTransactionFilters();

    private void ScheduleTransactionFilters()
    {
        transactionFilterDebounceTimer ??= new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        transactionFilterDebounceTimer.Stop();
        transactionFilterDebounceTimer.Tick -= TransactionFilterDebounceTimerOnTick;
        transactionFilterDebounceTimer.Tick += TransactionFilterDebounceTimerOnTick;
        transactionFilterDebounceTimer.Start();
    }

    private void TransactionFilterDebounceTimerOnTick(object? sender, EventArgs e)
    {
        transactionFilterDebounceTimer?.Stop();
        if (transactionFilterDebounceTimer is not null)
        {
            transactionFilterDebounceTimer.Tick -= TransactionFilterDebounceTimerOnTick;
        }

        ApplyTransactionFilters();
    }

    partial void OnSelectedReportViewChanged(CategoryOptionViewModel? value)
    {
        if (isRefreshingReportingSelections)
        {
            return;
        }

        RefreshReportingSelections();
    }

    partial void OnSelectedReportCategoryChanged(CategoryOptionViewModel? value) => RefreshReportingMetricsOnly();
    partial void OnReportDateFromChanged(DateTime? value) => RefreshReportingMetricsOnly();
    partial void OnReportDateToChanged(DateTime? value) => RefreshReportingMetricsOnly();

    partial void OnScheduledPaymentTypeChanged(string value) => UpdateScheduledPaymentCategoryOptions(SelectedScheduledPaymentCategory?.Code);

    partial void OnCurrentUserRoleChanged(string value)
    {
        OnPropertyChanged(nameof(IsCurrentUserAdmin));
        OnPropertyChanged(nameof(RoleDisplay));
    }

    partial void OnSelectedScheduledRepeatOptionChanged(CategoryOptionViewModel? value)
    {
        if (value is null)
        {
            return;
        }

        ScheduledPaymentFrequencyInput = value.Code;
        if (!string.Equals(value.Code, "custom", StringComparison.OrdinalIgnoreCase))
        {
            ResetScheduledCustomRepeatBuilder();
        }
    }

    partial void OnSelectedScheduledCustomPrimaryUnitChanged(CategoryOptionViewModel? value)
    {
        ScheduledCustomRepeatSecondaryUnitOptions = new ObservableCollection<CategoryOptionViewModel>(BuildScheduledCustomRepeatUnitOptions(value?.Code));
        SelectedScheduledCustomSecondaryUnit = ScheduledCustomRepeatSecondaryUnitOptions.FirstOrDefault();
        CoerceScheduledCustomRepeatSelection(1);
    }

    partial void OnSelectedScheduledCustomSecondaryUnitChanged(CategoryOptionViewModel? value)
    {
        ScheduledCustomRepeatTertiaryUnitOptions = new ObservableCollection<CategoryOptionViewModel>(BuildScheduledCustomRepeatUnitOptions(value?.Code));
        SelectedScheduledCustomTertiaryUnit = ScheduledCustomRepeatTertiaryUnitOptions.FirstOrDefault();
        CoerceScheduledCustomRepeatSelection(2);
    }

    partial void OnSelectedScheduledCustomTertiaryUnitChanged(CategoryOptionViewModel? value)
    {
        ScheduledCustomRepeatQuaternaryUnitOptions = new ObservableCollection<CategoryOptionViewModel>(BuildScheduledCustomRepeatUnitOptions(value?.Code));
        SelectedScheduledCustomQuaternaryUnit = ScheduledCustomRepeatQuaternaryUnitOptions.FirstOrDefault();
        CoerceScheduledCustomRepeatSelection(3);
    }

    partial void OnSelectedScheduledCustomQuaternaryUnitChanged(CategoryOptionViewModel? value) => CoerceScheduledCustomRepeatSelection(4);

    partial void OnTransactionSelectedDateChanged(DateTimeOffset? value)
    {
        if (isSyncingTransactionDateState || !value.HasValue)
        {
            return;
        }

        isSyncingTransactionDateState = true;
        try
        {
            TransactionDate = value.Value.DateTime;
            TransactionSelectedDay = value.Value.Day;
            TransactionSelectedMonth = value.Value.Month;
            TransactionSelectedYear = value.Value.Year;
            TransactionDateInput = value.Value.ToString(DateDisplayFormat);
        }
        finally
        {
            isSyncingTransactionDateState = false;
        }
    }

    partial void OnTransactionDateChanged(DateTime? value)
    {
        if (isSyncingTransactionDateState || !value.HasValue)
        {
            return;
        }

        isSyncingTransactionDateState = true;
        try
        {
            TransactionSelectedDate = new DateTimeOffset(value.Value);
        }
        finally
        {
            isSyncingTransactionDateState = false;
        }
    }

    partial void OnTransactionSelectedTimeChanged(TimeSpan? value)
    {
        if (value.HasValue)
        {
            TransactionTimeInput = value.Value.ToString(@"hh\:mm");
        }

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
        var settings = UserSettingsStore.Load();
        settings.LanguageCode = value.Code;
        UserSettingsStore.Save(settings);
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
        TransactionShareGroups = BuildGroupSelections([]);
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

    private static bool TryParseOptionalPositiveDecimal(string rawValue, out decimal value)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            value = 0;
            return true;
        }

        return decimal.TryParse(rawValue.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out value) && value >= 0;
    }

    private static bool TryParseOptionalSavingsDeadline(string rawValue, out DateOnly? deadline)
    {
        deadline = null;
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return true;
        }

        if (!TryParseDateOnly(rawValue, out var parsed))
        {
            return false;
        }

        deadline = parsed;
        return true;
    }

    private void ResetTransactionForm()
    {
        TransactionFormStatusMessage = string.Empty;
        ActiveTransaction = null;
        EditingTransactionId = null;
        EditingScheduledPaymentId = null;
        TransactionType = "expense";
        TransactionAmountInput = string.Empty;
        TransactionCurrencyInput = "UAH";
        TransactionDescriptionInput = string.Empty;
        IsTransactionSharedInput = false;
        CanEditTransactionShareGroups = true;
        SelectedTransactionShareGroup = null;
        TransactionShareGroups = BuildGroupSelections([]);
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

    private void RefreshTransactionDialogStateProperties()
    {
        OnPropertyChanged(nameof(IsEditingTransaction));
        OnPropertyChanged(nameof(IsEditingScheduledPayment));
        OnPropertyChanged(nameof(IsPlannedTransactionMode));
        OnPropertyChanged(nameof(TransactionDialogTitle));
        OnPropertyChanged(nameof(SaveTransactionButtonText));
    }
}
