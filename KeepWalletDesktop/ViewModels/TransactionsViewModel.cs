using CommunityToolkit.Mvvm.Input;
using KeepWalletDesktop.Models;
using KeepWalletDesktop.Services;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace KeepWalletDesktop.ViewModels;

public partial class MainViewModel
{
    [RelayCommand]
    private void SelectTransactionType(string type)
    {
        TransactionType = string.Equals(type, "income", StringComparison.OrdinalIgnoreCase) ? "income" : "expense";
    }

    [RelayCommand]
    private void SelectScheduledPaymentType(string type)
    {
        ScheduledPaymentType = string.Equals(type, "income", StringComparison.OrdinalIgnoreCase) ? "income" : "expense";
    }

    [RelayCommand]
    private async Task SaveTransactionAsync()
    {
        if (IsApiBusy)
        {
            return;
        }

        if (!decimal.TryParse(TransactionAmountInput, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) ||
            amount <= 0 ||
            SelectedTransactionCategory is null ||
            !TryBuildTransactionDateTime(TransactionSelectedDate, TransactionTimeInput, out var transactionDate))
        {
            ApiStatusMessage = Texts.TransactionFormValidationText;
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();
            if (apiCategories.Count == 0)
            {
                await LoadCategoriesAsync();
            }

            var accountId = SelectedBankAccount?.Id ?? await EnsureDefaultAccountAsync();
            var categoryId = GetCategoryId(SelectedTransactionCategory.Code);

            if (EditingTransactionId.HasValue)
            {
                await _apiClient!.UpdateTransactionAsync(
                    EditingTransactionId.Value,
                    new UpdateTransactionRequestDto(
                        accountId,
                        categoryId,
                        null,
                        amount,
                        string.IsNullOrWhiteSpace(TransactionDescriptionInput) ? null : TransactionDescriptionInput.Trim(),
                        transactionDate.ToUniversalTime()));

                ApiStatusMessage = Texts.TransactionUpdatedText;
            }
            else
            {
                await _apiClient!.CreateTransactionAsync(
                    new CreateTransactionRequestDto(
                        accountId,
                        categoryId,
                        null,
                        amount,
                        string.IsNullOrWhiteSpace(TransactionDescriptionInput) ? null : TransactionDescriptionInput.Trim(),
                        transactionDate.ToUniversalTime()));

                ApiStatusMessage = Texts.TransactionSavedText;
            }

            IsTransactionDialogOpen = false;
            ResetTransactionForm();
            await LoadTransactionsAsync();
            await LoadBankAccountsAsync();
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
    private void BeginCreateTransaction()
    {
        IsTransactionDialogOpen = false;
        ResetTransactionForm();
        ApiStatusMessage = string.Empty;
    }

    [RelayCommand]
    private void EditTransaction(TransactionItemViewModel? transaction)
    {
        if (transaction is null)
        {
            return;
        }

        ActiveTransaction = transaction;
        EditingTransactionId = transaction.Id;
        TransactionType = transaction.Type;
        TransactionAmountInput = transaction.Amount.ToString("0.##", CultureInfo.InvariantCulture);
        TransactionCurrencyInput = transaction.Currency;
        UpdateTransactionCategoryOptions(transaction.CategoryCode);
        TransactionDescriptionInput = transaction.Description ?? string.Empty;
        var localDate = transaction.TransactionDate.ToLocalTime();
        TransactionDateInput = localDate.ToString(DateDisplayFormat);
        TransactionSelectedDate = localDate.DateTime;
        TransactionTimeInput = localDate.ToString("HH:mm");
        IsAllTransactionsDialogOpen = false;
        IsTransactionDialogOpen = true;
    }

    [RelayCommand]
    private async Task DeleteTransactionAsync(TransactionItemViewModel? transaction)
    {
        transaction ??= ActiveTransaction;

        if (transaction is null || IsApiBusy)
        {
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();
            await _apiClient!.DeleteTransactionAsync(transaction.Id);
            ApiStatusMessage = Texts.TransactionDeletedText;
            IsTransactionDialogOpen = false;

            if (EditingTransactionId == transaction.Id)
            {
                ResetTransactionForm();
            }

            await LoadTransactionsAsync();
            await LoadBankAccountsAsync();
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

    private async Task LoadTransactionsAsync()
    {
        if (!IsApiAuthorized)
        {
            return;
        }

        try
        {
            EnsureApiClient();
            if (BankAccounts.Count == 0)
            {
                await LoadBankAccountsAsync();
            }

            var transactions = await _apiClient!.GetTransactionsAsync();
            AllTransactions = new ObservableCollection<TransactionItemViewModel>(
                transactions.Select(t => new TransactionItemViewModel(
                    t.Id,
                    t.AccountId,
                    GetTransactionType(t.CategoryId),
                    t.Amount,
                    GetAccountCurrency(t.AccountId),
                    t.CategoryId.ToString(CultureInfo.InvariantCulture),
                    GetCategoryDisplayName(t.CategoryId.ToString(CultureInfo.InvariantCulture)),
                    t.Description,
                    t.TransactionDate.ToLocalTime(),
                    GetTransactionType(t.CategoryId) == "income" ? Texts.TransactionIncomeType : Texts.TransactionExpenseType)));

            var mainAccountId = SelectedBankAccount?.Id
                ?? BankAccounts.FirstOrDefault(a => a.IsDefault)?.Id
                ?? BankAccounts.FirstOrDefault()?.Id;

            if (mainAccountId.HasValue && SelectedBankAccount is null)
            {
                SelectedBankAccount = BankAccounts.FirstOrDefault(a => a.Id == mainAccountId.Value);
            }

            LatestExpenses = new ObservableCollection<TransactionItemViewModel>(
                AllTransactions
                    .Where(t => !mainAccountId.HasValue || t.AccountId == mainAccountId.Value)
                    .OrderByDescending(t => t.TransactionDate));

            AvailableBalance = LatestExpenses.Where(t => t.IsIncome).Sum(t => (double)t.Amount) -
                               LatestExpenses.Where(t => !t.IsIncome).Sum(t => (double)t.Amount);

            ApplyTransactionFilters();
            UpdateVisibleTransactions();
            UpdateCharts();
            OnPropertyChanged(nameof(Money));
            OnPropertyChanged(nameof(Income));
            OnPropertyChanged(nameof(Expense));
            OnPropertyChanged(nameof(HasTransactions));
            OnPropertyChanged(nameof(HasNoTransactions));
            OnPropertyChanged(nameof(HasAllTransactions));
            OnPropertyChanged(nameof(HasExpenseTransactions));
            OnPropertyChanged(nameof(HasNoExpenseTransactions));
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
}
