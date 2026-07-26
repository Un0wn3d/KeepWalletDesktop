using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClosedXML.Excel;
using KeepWalletDesktop.Models;
using KeepWalletDesktop.Services;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.ImageFilters;
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
using System.IO;
using Serilog;
using QuestPDF.Fluent;
using QuestPDF.Helpers;

namespace KeepWalletDesktop.ViewModels;

public partial class MainViewModel
{    private void LoadApiOnlyPlaceholders()
    {
        GoalAmount = 0;
        SavedAmount = 0;
        AddAmount = string.Empty;
        ActiveSavingId = null;
        SavingNameInput = "Savings";
        ActiveSavingIsCompleted = false;
        InterestRate = 0;
        SavingItemNameInput = string.Empty;
        SavingItemAmount = string.Empty;
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
        RefreshTransactionTypeFilterOptions();
        SelectedTransactionTypeFilter = TransactionTypeFilterOptions.FirstOrDefault();
        SelectedTransactionAccountFilter = null;
        TransactionAccountFilterOptions = [];
        SelectedTransactionCategoryFilter = null;
        TransactionCategoryFilterOptions = [];
        SelectedTransactionCurrencyFilter = null;
        TransactionCurrencyFilterOptions = [];
        TransactionGroupFilterOptions = [];
        TransactionUserFilterOptions = [];
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

    private void RefreshLocalizedProperties()
    {
        OnPropertyChanged(nameof(ApiConnectionStatus));
        OnPropertyChanged(nameof(ApiUserDisplay));
        OnPropertyChanged(nameof(StatusDisplay));
        OnPropertyChanged(nameof(UserDisplay));
        OnPropertyChanged(nameof(EmailDisplay));
        OnPropertyChanged(nameof(RoleDisplay));
        OnPropertyChanged(nameof(AdminUsersCountDisplay));
        OnPropertyChanged(nameof(ReportSelectedCategoryExpenseTotalDisplay));
        OnPropertyChanged(nameof(ReportSelectedCategoryTransactionCountDisplay));
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
        RefreshTransactionTypeFilterOptions();
        RefreshTransactionFilterOptions();
        UpdateTransactionCategoryOptions(SelectedTransactionCategory?.Code);
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
        OnPropertyChanged(nameof(ChartTooltipBackgroundPaint));
        OnPropertyChanged(nameof(ChartTooltipTextPaint));
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

    private void InitializeLanguages(string? selectedLanguageCode)
    {
        LanguageOptions =
        [
            new LanguageOptionViewModel("ua", "Українська"),
            new LanguageOptionViewModel("en", "English")
        ];

        var normalizedLanguageCode = string.Equals(selectedLanguageCode, "en", StringComparison.OrdinalIgnoreCase)
            ? "en"
            : "ua";
        SelectedLanguage = LanguageOptions.FirstOrDefault(x => x.Code == normalizedLanguageCode)
            ?? LanguageOptions.FirstOrDefault(x => x.Code == "ua");
        UpdateLanguageOptionTitles();
    }

    private void InitializeThemes(string? selectedThemeName)
    {
        ThemeOptions =
        [
            new CategoryOptionViewModel(ThemeManager.Light, Texts.LightThemeName),
            new CategoryOptionViewModel(ThemeManager.Dark, Texts.DarkThemeName)
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
            new CategoryOptionViewModel("year", Texts.ScheduledRepeatYearOption),
            new CategoryOptionViewModel("month", Texts.ScheduledRepeatMonthOption),
            new CategoryOptionViewModel("week", Texts.ScheduledRepeatWeekOption),
            new CategoryOptionViewModel("day", Texts.ScheduledRepeatDayOption)
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
            new CategoryOptionViewModel("monthly_plan", Texts.ReportMonthlyPlanOption),
            new CategoryOptionViewModel("monthly_expenses", Texts.ReportMonthlyExpensesOption)
        ];
        ReportViewOptions[0].DisplayName = Texts.ReportCategoryPeriodOption;
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
        OnPropertyChanged(nameof(ReportSelectedCategoryExpenseTotalDisplay));
        OnPropertyChanged(nameof(ReportSelectedCategoryTransactionCountDisplay));

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
            option.DisplayName = option.Code == ThemeManager.Dark
                ? Texts.DarkThemeName
                : Texts.LightThemeName;
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

    private void UpdateReportingOptionTitles()
    {
        foreach (var option in ReportViewOptions)
        {
            option.DisplayName = option.Code switch
            {
                "category_period" => Texts.ReportCategoryPeriodOption,
                "monthly_plan" => Texts.ReportMonthlyPlanOption,
                "monthly_expenses" => Texts.ReportMonthlyExpensesOption,
                _ => option.DisplayName
            };
        }
    }

    private void UpdateScheduledCustomRepeatUnitOptionTitles()
    {
        foreach (var option in ScheduledCustomRepeatUnitOptions)
        {
            option.DisplayName = option.Code switch
            {
                "year" => Texts.ScheduledRepeatYearOption,
                "month" => Texts.ScheduledRepeatMonthOption,
                "week" => Texts.ScheduledRepeatWeekOption,
                _ => Texts.ScheduledRepeatDayOption
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

}
