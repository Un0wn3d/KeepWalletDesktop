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
{    [RelayCommand]
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

    public void ApplyAllTransactionsDateRange(DateTimeOffset from, DateTimeOffset to)
    {
        var normalizedFrom = from <= to ? from : to;
        var normalizedTo = from <= to ? to : from;

        if (normalizedFrom.UtcDateTime == allTransactionsDateRangeFrom.UtcDateTime &&
            normalizedTo.UtcDateTime == allTransactionsDateRangeTo.UtcDateTime)
        {
            return;
        }

        allTransactionsDateRangeFrom = normalizedFrom;
        allTransactionsDateRangeTo = normalizedTo;
        RefreshTransactionFilterOptions();
        ScheduleTransactionFilters();
    }

    public void RefreshAllTransactionsAdvancedFilters()
    {
        OnPropertyChanged(nameof(HasTransactionAdvancedFilter));
        ScheduleTransactionFilters();
    }

    [RelayCommand]
    private void ClearTransactionAdvancedFilters()
    {
        foreach (var option in TransactionGroupFilterOptions)
        {
            option.IsSelected = false;
        }

        foreach (var option in TransactionUserFilterOptions)
        {
            option.IsSelected = false;
        }

        OnPropertyChanged(nameof(HasTransactionAdvancedFilter));
        ScheduleTransactionFilters();
    }

    [RelayCommand]
    private void ClearTransactionFilters()
    {
        TransactionSearchText = string.Empty;
        SelectedTransactionTypeFilter = TransactionTypeFilterOptions.FirstOrDefault(x => x.Code == "all")
            ?? TransactionTypeFilterOptions.FirstOrDefault();
        SelectedTransactionAccountFilter = TransactionAccountFilterOptions.FirstOrDefault();
        SelectedTransactionCurrencyFilter = TransactionCurrencyFilterOptions.FirstOrDefault();
        SelectedTransactionCategoryFilter = TransactionCategoryFilterOptions.FirstOrDefault();

        foreach (var option in TransactionGroupFilterOptions)
        {
            option.IsSelected = false;
        }

        foreach (var option in TransactionUserFilterOptions)
        {
            option.IsSelected = false;
        }

        OnPropertyChanged(nameof(HasTransactionAdvancedFilter));
        RefreshTransactionFilterOptions();
        ScheduleTransactionFilters();
    }

    private void RefreshTransactionTypeFilterOptions()
    {
        var selectedCode = SelectedTransactionTypeFilter?.Code ?? "all";
        TransactionTypeFilterOptions = new ObservableCollection<CategoryOptionViewModel>(
        [
            new CategoryOptionViewModel("all", Texts.AllTransactionsTypeFilterPlaceholder),
            new CategoryOptionViewModel("expense", Texts.TransactionExpenseType),
            new CategoryOptionViewModel("income", Texts.TransactionIncomeType)
        ]);
        SelectedTransactionTypeFilter = TransactionTypeFilterOptions.FirstOrDefault(x => x.Code == selectedCode)
            ?? TransactionTypeFilterOptions.FirstOrDefault();
        OnPropertyChanged(nameof(TransactionTypeFilterOptions));
    }

    private void RefreshTransactionFilterOptions()
    {
        var source = GetAllTransactionsDateFilteredSource().ToArray();
        var typeCode = SelectedTransactionTypeFilter?.Code ?? "all";
        var typedSource = string.Equals(typeCode, "all", StringComparison.OrdinalIgnoreCase)
            ? source
            : source.Where(t => string.Equals(t.Type, typeCode, StringComparison.OrdinalIgnoreCase)).ToArray();

        RefreshTransactionAccountFilterOptions(typedSource);
        RefreshTransactionCurrencyFilterOptions(typedSource);
        RefreshTransactionCategoryFilterOptions(typedSource);
        RefreshTransactionAdvancedFilterOptions(typedSource);
    }

    private void RefreshTransactionCategoryFilterOptions(IEnumerable<TransactionItemViewModel> transactions)
    {
        var selectedCode = SelectedTransactionCategoryFilter?.Code ?? "all";
        var options = transactions
            .Where(t => !string.IsNullOrWhiteSpace(t.CategoryCode))
            .GroupBy(t => t.CategoryCode!, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.First().CategoryDisplay)
            .Select(g => new CategoryOptionViewModel(g.Key, g.First().CategoryDisplay))
            .ToList();
        options.Insert(0, new CategoryOptionViewModel("all", Texts.AllTransactionsCategoriesFilterPlaceholder));
        TransactionCategoryFilterOptions = new ObservableCollection<CategoryOptionViewModel>(options);
        SelectedTransactionCategoryFilter = options.FirstOrDefault(x => x.Code == selectedCode)
            ?? options.FirstOrDefault();
    }

    private void RefreshTransactionAccountFilterOptions(IEnumerable<TransactionItemViewModel> transactions)
    {
        var selectedAccountId = SelectedTransactionAccountFilter?.AccountId;
        var accountIds = transactions
            .Select(t => t.AccountId)
            .Distinct()
            .ToHashSet();
        var options = BankAccounts
            .Where(account => accountIds.Contains(account.Id))
            .OrderBy(account => account.Name)
            .Select(account => new BankAccountFilterOptionViewModel(account.Id, account.Name))
            .ToList();
        options.Insert(0, new BankAccountFilterOptionViewModel(null, Texts.AllTransactionsAccountsFilterPlaceholder));

        TransactionAccountFilterOptions = new ObservableCollection<BankAccountFilterOptionViewModel>(options);
        SelectedTransactionAccountFilter = options.FirstOrDefault(x => x.AccountId == selectedAccountId)
            ?? options.FirstOrDefault();
    }

    private void RefreshTransactionCurrencyFilterOptions(IEnumerable<TransactionItemViewModel> transactions)
    {
        var selectedCode = SelectedTransactionCurrencyFilter?.Code ?? "all";
        var options = transactions
            .Select(t => t.Currency)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c)
            .Select(c => new CategoryOptionViewModel(c, c))
            .ToList();
        options.Insert(0, new CategoryOptionViewModel("all", Texts.ScheduledPaymentsCurrencyFilterPlaceholder));

        TransactionCurrencyFilterOptions = new ObservableCollection<CategoryOptionViewModel>(options);
        SelectedTransactionCurrencyFilter = options.FirstOrDefault(x => x.Code == selectedCode)
            ?? options.FirstOrDefault();
    }

    private void RefreshTransactionAdvancedFilterOptions(IEnumerable<TransactionItemViewModel> transactions)
    {
        var selectedGroups = TransactionGroupFilterOptions
            .Where(option => option.IsSelected)
            .Select(option => option.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var selectedUsers = TransactionUserFilterOptions
            .Where(option => option.IsSelected)
            .Select(option => option.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        TransactionGroupFilterOptions = new ObservableCollection<FilterSelectionOptionViewModel>(
            transactions
                .SelectMany(transaction => transaction.GroupIds)
                .Distinct()
                .Select(id =>
                {
                    var code = id.ToString();
                    var name = Groups.FirstOrDefault(group => group.Id == id)?.Name
                        ?? transactions.FirstOrDefault(transaction => transaction.GroupIds.Contains(id))?.GroupDisplay
                        ?? "Group";
                    return new FilterSelectionOptionViewModel(code, name, selectedGroups.Contains(code));
                })
                .OrderBy(option => option.DisplayName));

        TransactionUserFilterOptions = new ObservableCollection<FilterSelectionOptionViewModel>(
            transactions
                .Select(transaction => GetTransactionOwnerFilterDisplay(transaction))
                .Where(owner => !string.IsNullOrWhiteSpace(owner))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(owner => owner)
                .Select(owner => new FilterSelectionOptionViewModel(owner, owner, selectedUsers.Contains(owner))));
        OnPropertyChanged(nameof(HasTransactionAdvancedFilter));
    }

    private IEnumerable<TransactionItemViewModel> GetAllTransactionsDateFilteredSource() =>
        AllTransactions.Where(t => t.TransactionDate >= allTransactionsDateRangeFrom && t.TransactionDate <= allTransactionsDateRangeTo);

    private string GetTransactionOwnerFilterDisplay(TransactionItemViewModel transaction)
    {
        var owner = transaction.PayerDisplay;
        if (string.IsNullOrWhiteSpace(owner) || owner == "-")
        {
            owner = string.IsNullOrWhiteSpace(CurrentUsername) || CurrentUsername == Texts.GuestLabel
                ? Texts.UserLabel
                : CurrentUsername.Trim();
        }

        return !string.IsNullOrWhiteSpace(CurrentUsername) &&
               string.Equals(owner.Trim(), CurrentUsername.Trim(), StringComparison.OrdinalIgnoreCase)
            ? "Я"
            : owner.Trim();
    }

    private void ApplyTransactionFilters()
    {
        var search = TransactionSearchText.Trim();
        var categoryCode = SelectedTransactionCategoryFilter?.Code;
        var accountId = SelectedTransactionAccountFilter?.AccountId;
        var currencyCode = SelectedTransactionCurrencyFilter?.Code;
        var filtered = GetAllTransactionsDateFilteredSource();
        filteredTransactionTakeCount = AllTransactionsPageSize;

        if (!string.Equals(SelectedTransactionTypeFilter?.Code, "all", StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(t => string.Equals(t.Type, SelectedTransactionTypeFilter?.Code, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(categoryCode) && categoryCode != "all")
        {
            filtered = filtered.Where(t => string.Equals(t.CategoryCode, categoryCode, StringComparison.OrdinalIgnoreCase));
        }

        if (accountId.HasValue)
        {
            filtered = filtered.Where(t => t.AccountId == accountId.Value);
        }

        if (!string.IsNullOrWhiteSpace(currencyCode) && currencyCode != "all")
        {
            filtered = filtered.Where(t => string.Equals(t.Currency, currencyCode, StringComparison.OrdinalIgnoreCase));
        }

        var selectedGroupIds = TransactionGroupFilterOptions
            .Where(option => option.IsSelected)
            .Select(option => option.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (selectedGroupIds.Count > 0)
        {
            filtered = filtered.Where(t => t.GroupIds.Any(id => selectedGroupIds.Contains(id.ToString())));
        }

        var selectedUsers = TransactionUserFilterOptions
            .Where(option => option.IsSelected)
            .Select(option => option.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (selectedUsers.Count > 0)
        {
            filtered = filtered.Where(t => selectedUsers.Contains(GetTransactionOwnerFilterDisplay(t)));
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

    private string GetAccountName(Guid accountId)
    {
        return BankAccounts.FirstOrDefault(a => a.Id == accountId)?.Name
            ?? SelectedBankAccount?.Name
            ?? "-";
    }

    private static string FormatTransactionTotals(IEnumerable<TransactionItemViewModel> transactions)
    {
        var totals = transactions
            .GroupBy(t => t.Currency)
            .Select(g => $"{FormatMoneyAmount(g.Sum(t => t.Amount))} {g.Key}")
            .ToArray();

        return totals.Length == 0 ? "0.00" : string.Join(" / ", totals);
    }

    private static string FormatScheduledPaymentTotals(IEnumerable<ScheduledPaymentItemViewModel> payments)
    {
        var totals = payments
            .GroupBy(p => p.Currency)
            .Select(g => $"{FormatMoneyAmount(g.Sum(p => p.Amount))} {g.Key}")
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
        var options = BuildCategoryOptions(TransactionType, selectedCode);
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

    private IEnumerable<CategoryOptionViewModel> BuildCategoryOptions(string transactionType, string? forcedSelectedCode = null)
    {
        if (apiCategories.Count > 0)
        {
            var options = apiCategories
                .Where(c => string.Equals(c.Type, transactionType, StringComparison.OrdinalIgnoreCase))
                .Where(c => selectedCategoryIds is null || selectedCategoryIds.Contains(c.Id))
                .Select(c => new CategoryOptionViewModel(c.Id.ToString(CultureInfo.InvariantCulture), c.Name))
                .ToArray();

            if (int.TryParse(forcedSelectedCode, NumberStyles.Integer, CultureInfo.InvariantCulture, out var forcedCategoryId) &&
                options.All(c => c.Code != forcedSelectedCode))
            {
                var forcedCategory = apiCategories.FirstOrDefault(c =>
                    c.Id == forcedCategoryId &&
                    string.Equals(c.Type, transactionType, StringComparison.OrdinalIgnoreCase));
                if (forcedCategory is not null)
                {
                    options = options
                        .Append(new CategoryOptionViewModel(forcedCategory.Id.ToString(CultureInfo.InvariantCulture), forcedCategory.Name))
                        .ToArray();
                }
            }

            return options.Append(new CategoryOptionViewModel(NewCategoryOptionCode, Texts.NewCategoryOption)).ToArray();
        }

        if (transactionType == "income")
        {
            return
            [
                new CategoryOptionViewModel("salary", Texts.CategorySalary),
                new CategoryOptionViewModel("freelance", Texts.CategoryFreelance),
                new CategoryOptionViewModel("other_income", Texts.CategoryOtherIncome),
                new CategoryOptionViewModel(NewCategoryOptionCode, Texts.NewCategoryOption)
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
            new CategoryOptionViewModel("other_expense", Texts.CategoryOtherExpense),
            new CategoryOptionViewModel(NewCategoryOptionCode, Texts.NewCategoryOption)
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

    private string? ResolveGroupColor(Guid? groupId) =>
        groupId.HasValue
            ? Groups.FirstOrDefault(g => g.Id == groupId.Value)?.SelectedColor
            : null;

    private string? ResolveTransactionGroupName(Guid? groupId, string? apiGroupName) =>
        !string.IsNullOrWhiteSpace(apiGroupName)
            ? apiGroupName
            : groupId.HasValue
                ? Groups.FirstOrDefault(g => g.Id == groupId.Value)?.Name
                : null;

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
}
