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
{    private void ResetBankAccountForm()
    {
        EditingBankAccountId = null;
        BankAccountNameInput = string.Empty;
        BankAccountCurrencyInput = "UAH";
        BankAccountBalanceInput = "0";
        IsBankAccountDefaultInput = false;
        IsBankAccountSharedInput = false;
        CanEditBankAccountShareGroups = true;
        SelectedBankAccountShareGroup = null;
        BankAccountShareGroups = BuildGroupSelections([]);
        BankAccountStatusMessage = string.Empty;
    }

    private ObservableCollection<GroupSelectionItemViewModel> BuildGroupSelections(IEnumerable<Guid> selectedGroupIds)
    {
        var selected = selectedGroupIds.ToHashSet();
        return new ObservableCollection<GroupSelectionItemViewModel>(
            Groups
                .Where(g => !g.IsViewerRole)
                .OrderBy(g => g.Name)
                .Select(g => new GroupSelectionItemViewModel(g.Id, g.Name, selected.Contains(g.Id))));
    }

    private Guid[] GetSelectedBankAccountShareGroupIds() =>
        IsBankAccountSharedInput
            ? BankAccountShareGroups.Where(g => g.IsSelected).Select(g => g.Id).Distinct().ToArray()
            : [];

    private Guid[] GetSelectedTransactionShareGroupIds() =>
        IsTransactionSharedInput
            ? TransactionShareGroups.Where(g => g.IsSelected).Select(g => g.Id).Distinct().ToArray()
            : [];

    private Guid[] GetSelectedSavingShareGroupIds() =>
        IsSavingSharedInput
            ? SavingShareGroups.Where(g => g.IsSelected).Select(g => g.Id).Distinct().ToArray()
            : [];

    partial void OnAddAmountChanged(string value)
    {
        AddAmountError = ValidatePositiveAmountInput(value);
    }

    partial void OnSavingItemAmountChanged(string value)
    {
        SavingItemAmountError = ValidatePositiveAmountInput(value);
    }

    private static string ValidatePositiveAmountInput(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return TryParsePositiveAmount(value, out _)
            ? string.Empty
            : decimal.TryParse(value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out _)
                ? "Сума має бути більше 0"
                : "Введіть число";
    }

    private static bool TryParsePositiveAmount(string? value, out decimal amount)
    {
        amount = 0;
        return !string.IsNullOrWhiteSpace(value) &&
               decimal.TryParse(value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out amount) &&
               amount > 0;
    }

    private void RefreshActiveSavingShareProperties()
    {
        OnPropertyChanged(nameof(ActiveSavingOption));
        OnPropertyChanged(nameof(ActiveSavingOwnerDisplay));
        OnPropertyChanged(nameof(ActiveSavingGroupDisplay));

        var selectedGroupIds = ActiveSavingOption?.GroupIds ?? [];
        CanEditSavingShareGroups = ActiveSavingOption is null || IsOwnSaving(ActiveSavingOption);
        IsSavingSharedInput = selectedGroupIds.Count > 0;
        SavingShareGroups = BuildGroupSelections(selectedGroupIds);
    }

    private bool IsOwnSaving(SavingOptionViewModel saving) =>
        saving.UserId.HasValue
            ? saving.UserId == CurrentUserId
            : !saving.GroupId.HasValue || string.IsNullOrWhiteSpace(saving.OwnerDisplayText) || saving.OwnerDisplayText == "-";

    private bool IsOwnTransaction(TransactionItemViewModel transaction) =>
        string.IsNullOrWhiteSpace(transaction.SharedBy) ||
        transaction.SharedBy == "-" ||
        string.Equals(transaction.SharedBy, CurrentUsername, StringComparison.OrdinalIgnoreCase);

    private bool IsOwnScheduledPayment(ScheduledPaymentItemViewModel payment) =>
        string.IsNullOrWhiteSpace(payment.OwnerDisplayText) ||
        payment.OwnerDisplayText == "-" ||
        string.Equals(payment.OwnerDisplayText, CurrentUsername, StringComparison.OrdinalIgnoreCase);

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
}
