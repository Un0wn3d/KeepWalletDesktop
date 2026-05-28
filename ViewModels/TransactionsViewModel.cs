using CommunityToolkit.Mvvm.Input;
using KeepWalletDesktop.Models;
using KeepWalletDesktop.Services;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
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
            !TryBuildTransactionDateTime(TransactionSelectedDate, TransactionSelectedTime, out var transactionDate) ||
            (IsTransactionSharedInput && SelectedTransactionShareGroup is null))
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
            var groupId = IsTransactionSharedInput ? SelectedTransactionShareGroup?.Id : null;
            var transactionName = string.IsNullOrWhiteSpace(TransactionDescriptionInput) ? null : TransactionDescriptionInput.Trim();

            if (EditingTransactionId.HasValue)
            {
                await _apiClient!.UpdateTransactionAsync(
                    EditingTransactionId.Value,
                    new UpdateTransactionRequestDto(
                        accountId,
                        groupId,
                        categoryId,
                        null,
                        null,
                        amount,
                        transactionName,
                        transactionDate.ToUniversalTime()));

                ApiStatusMessage = Texts.TransactionUpdatedText;
            }
            else if (IsPlannedTransactionMode)
            {
                var repeatInterval = ParseRepeatInterval(IsCustomScheduledRepeat
                    ? ScheduledPaymentCustomRepeatInput
                    : SelectedScheduledRepeatOption?.Code ?? "once");

                await _apiClient!.CreatePlannedTransactionAsync(
                    new CreatePlannedTransactionRequestDto(
                        transactionName ?? Texts.NewTransactionButton,
                        accountId,
                        categoryId,
                        amount,
                        null,
                        DateOnly.FromDateTime(transactionDate.Date),
                        repeatInterval));

                ApiStatusMessage = Texts.TransactionSavedText;
            }
            else
            {
                await _apiClient!.CreateTransactionAsync(
                    new CreateTransactionRequestDto(
                        accountId,
                        groupId,
                        categoryId,
                        null,
                        null,
                        amount,
                        transactionName,
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
        if (Groups.Count == 0)
        {
            _ = LoadGroupsAsync();
        }
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
        IsTransactionSharedInput = transaction.GroupId.HasValue;
        SelectedTransactionShareGroup = transaction.GroupId.HasValue
            ? Groups.FirstOrDefault(g => g.Id == transaction.GroupId.Value)
            : null;
        if (transaction.GroupId.HasValue && SelectedTransactionShareGroup is null)
        {
            _ = LoadGroupsAsync();
        }
        var localDate = transaction.TransactionDate.ToLocalTime();
        TransactionDateInput = localDate.ToString(DateDisplayFormat);
        TransactionSelectedDate = localDate;
        TransactionSelectedTime = localDate.TimeOfDay;
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

        if (isLoadingTransactions)
        {
            return;
        }

        // Throttle frequent page-switch storms.
        if (DateTimeOffset.UtcNow - lastTransactionsLoadedAt < TimeSpan.FromMilliseconds(700))
        {
            return;
        }

        transactionsLoadCts?.Cancel();
        transactionsLoadCts?.Dispose();
        var currentLoadCts = new CancellationTokenSource();
        transactionsLoadCts = currentLoadCts;
        var cancellationToken = currentLoadCts.Token;

        try
        {
            isLoadingTransactions = true;
            EnsureApiClient();
            if (BankAccounts.Count == 0)
            {
                await LoadBankAccountsAsync();
            }

            ViewModelLog.Information(
                "Loading transactions from API for home/all transaction views. CurrentPage={CurrentPage}, BankAccountsCount={BankAccountsCount}",
                CurrentPage,
                BankAccounts.Count);
            var transactions = await _apiClient!.GetTransactionsAsync(cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            ViewModelLog.Information("Transactions received from API: Count={Count}", transactions.Count);
            AllTransactions = new ObservableCollection<TransactionItemViewModel>(
                transactions.Select(t => new TransactionItemViewModel(
                    t.Id,
                    t.AccountId,
                    t.GroupId,
                    t.GroupName,
                    t.SharedBy,
                    GetTransactionType(t.CategoryId),
                    t.SavingId,
                    t.Amount,
                    GetAccountCurrency(t.AccountId),
                    t.CategoryId.ToString(CultureInfo.InvariantCulture),
                    GetCategoryDisplayName(t.CategoryId.ToString(CultureInfo.InvariantCulture)),
                    GetCategoryIconKey(t.CategoryId.ToString(CultureInfo.InvariantCulture)),
                    GetCategoryColor(t.CategoryId.ToString(CultureInfo.InvariantCulture)),
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

            homeTransactionSource = AllTransactions
                .Where(t => !mainAccountId.HasValue || t.AccountId == mainAccountId.Value)
                .OrderByDescending(t => t.TransactionDate)
                .ToArray();

            RefreshHomeTransactionsFromDateRange();
            ViewModelLog.Information(
                "Transactions processed: AllCount={AllCount}, HomeSourceCount={HomeSourceCount}, SelectedAccountId={SelectedAccountId}",
                AllTransactions.Count,
                homeTransactionSource.Count,
                mainAccountId);
            lastTransactionsLoadedAt = DateTimeOffset.UtcNow;
            OnPropertyChanged(nameof(SharedTransactions));
            OnPropertyChanged(nameof(HasSharedTransactions));
        }
        catch (HttpRequestException ex)
        {
            ViewModelLog.Warning(ex, "HTTP error while loading transactions from API");
            ApiStatusMessage = GetApiErrorMessage(ex);
        }
        catch (OperationCanceledException)
        {
            // Expected when a newer transactions load supersedes current one.
        }
        catch (Exception ex)
        {
            ViewModelLog.Error(ex, "Unexpected error while loading transactions from API");
            ApiStatusMessage = $"{Texts.RequestErrorPrefix}: {ex.Message}";
        }
        finally
        {
            isLoadingTransactions = false;

            if (ReferenceEquals(transactionsLoadCts, currentLoadCts))
            {
                transactionsLoadCts.Dispose();
                transactionsLoadCts = null;
            }
        }
    }
}
