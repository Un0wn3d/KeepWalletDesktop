using CommunityToolkit.Mvvm.Input;
using KeepWalletDesktop.Models;
using KeepWalletDesktop.Services;
using System;
using System.Collections.Generic;
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
    private async Task CreateTransactionCategoryFromDialogAsync()
    {
        CategoryNewType = TransactionType;
        CategoryNewName = string.Empty;
        CategoryStatusMessage = string.Empty;
        if (apiCategories.Count == 0)
        {
            await LoadCategoriesAsync();
        }

        IsAddCategoryModuleOpen = true;
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

        if (!TryValidateTransactionForm(out var amount, out var transactionDate, out var validationMessage))
        {
            TransactionFormStatusMessage = validationMessage;
            return;
        }
        TransactionFormStatusMessage = string.Empty;

        try
        {
            IsApiBusy = true;
            EnsureApiClient();
            var shouldReloadScheduledPayments = false;
            if (apiCategories.Count == 0)
            {
                await LoadCategoriesAsync();
            }

            var selectedAccount = SelectedBankAccount;
            var accountId = selectedAccount?.Id ?? await EnsureDefaultAccountAsync();
            var categoryId = GetCategoryId(SelectedTransactionCategory!.Code);
            var groupIds = GetSelectedTransactionShareGroupIds();
            var requestGroupId = ResolveTransactionRequestGroupId(selectedAccount, groupIds);
            var canUpdateTransactionGroups = EditingTransactionId.HasValue
                ? ActiveTransaction is not null && IsOwnTransaction(ActiveTransaction)
                : selectedAccount is null || IsOwnBankAccount(selectedAccount);
            var transactionName = string.IsNullOrWhiteSpace(TransactionDescriptionInput) ? null : TransactionDescriptionInput.Trim();
            var hasScheduledRepeat = TryBuildScheduledRepeatInterval(out var repeatInterval);
            var originalTransaction = ActiveTransaction;
            ApiTransactionDto savedTransaction;

            if (EditingScheduledPaymentId.HasValue && EditingTransactionId.HasValue)
            {
                if (!hasScheduledRepeat)
                {
                    TransactionFormStatusMessage = $"{Texts.TransactionFormValidationText} Причина: для запланованої транзакції потрібно вибрати повторення.";
                    return;
                }
                shouldReloadScheduledPayments = true;

                savedTransaction = await _apiClient!.UpdatePlannedTransactionAsync(
                    EditingTransactionId.Value,
                    new CreatePlannedTransactionRequestDto(
                        transactionName ?? Texts.NewTransactionButton,
                        accountId,
                        requestGroupId,
                        categoryId,
                        amount,
                        null,
                        DateOnly.FromDateTime(transactionDate.LocalDateTime.Date),
                        repeatInterval));

                ApiStatusMessage = Texts.TransactionUpdatedText;
            }
            else if (EditingTransactionId.HasValue)
            {
                savedTransaction = await _apiClient!.UpdateTransactionAsync(
                    EditingTransactionId.Value,
                    new UpdateTransactionRequestDto(
                        accountId,
                        requestGroupId,
                        categoryId,
                        originalTransaction?.SavingId,
                        null,
                        amount,
                        transactionName,
                        transactionDate.ToUniversalTime()));

                await AdjustSavingCurrentAmountAfterTransactionEditAsync(originalTransaction, amount, GetTransactionType(categoryId));
                ApiStatusMessage = Texts.TransactionUpdatedText;
            }
            else if (IsPlannedTransactionMode)
            {
                if (!hasScheduledRepeat)
                {
                    TransactionFormStatusMessage = $"{Texts.TransactionFormValidationText} Причина: для запланованої транзакції потрібно вибрати повторення.";
                    return;
                }
                shouldReloadScheduledPayments = true;

                savedTransaction = await _apiClient!.CreatePlannedTransactionAsync(
                    new CreatePlannedTransactionRequestDto(
                        transactionName ?? Texts.NewTransactionButton,
                        accountId,
                        requestGroupId,
                        categoryId,
                        amount,
                        null,
                        DateOnly.FromDateTime(transactionDate.LocalDateTime.Date),
                        repeatInterval));

                ApiStatusMessage = Texts.TransactionSavedText;
            }
            else
            {
                savedTransaction = await _apiClient!.CreateTransactionAsync(
                    new CreateTransactionRequestDto(
                        accountId,
                        requestGroupId,
                        categoryId,
                        null,
                        null,
                        amount,
                        transactionName,
                        transactionDate.ToUniversalTime()));

                ApiStatusMessage = Texts.TransactionSavedText;
            }

            if (canUpdateTransactionGroups)
            {
                await _apiClient!.UpdateTransactionGroupsAsync(savedTransaction.Id, new ReplaceResourceGroupsRequestDto(groupIds));
            }

            IsTransactionDialogOpen = false;
            ResetTransactionForm();
            await LoadTransactionsAsync();
            await LoadBankAccountsAsync();
            if (shouldReloadScheduledPayments)
            {
                await LoadScheduledPaymentsAsync();
            }
        }
        catch (HttpRequestException ex)
        {
            ApiStatusMessage = GetApiErrorMessage(ex, Texts);
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

    private static Guid? ResolveTransactionRequestGroupId(BankAccountItemViewModel? selectedAccount, Guid[] selectedGroupIds)
    {
        if (selectedGroupIds.Length > 0)
        {
            return selectedGroupIds[0];
        }

        if (selectedAccount?.GroupId is { } groupId)
        {
            return groupId;
        }

        var inheritedGroupId = selectedAccount?.GroupIds.FirstOrDefault();
        return inheritedGroupId is { } value && value != Guid.Empty ? value : null;
    }

    private bool TryValidateTransactionForm(
        out decimal amount,
        out DateTimeOffset transactionDate,
        out string validationMessage)
    {
        amount = 0;
        transactionDate = default;

        var reasons = new List<string>();
        if (string.IsNullOrWhiteSpace(TransactionDescriptionInput))
        {
            reasons.Add("не заповнена назва");
        }

        if (!decimal.TryParse(TransactionAmountInput, NumberStyles.Number, CultureInfo.InvariantCulture, out amount) || amount <= 0)
        {
            reasons.Add("сума має бути більшою за 0");
        }

        if (SelectedTransactionCategory is null)
        {
            reasons.Add("не вибрана категорія");
        }
        else if (SelectedTransactionCategory.Code == NewCategoryOptionCode)
        {
            reasons.Add("спочатку створіть нову категорію або виберіть існуючу");
        }
        else if (int.TryParse(SelectedTransactionCategory.Code, NumberStyles.Integer, CultureInfo.InvariantCulture, out var categoryId))
        {
            var categoryType = GetTransactionType(categoryId);
            var selectedType = TransactionType;
            if (!string.Equals(categoryType, selectedType, StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add($"категорія належить до типу \"{(categoryType == "income" ? Texts.TransactionIncomeType : Texts.TransactionExpenseType)}\"");
            }
        }

        if (!TryBuildTransactionDateTime(TransactionSelectedDate, TransactionSelectedTime, out transactionDate))
        {
            reasons.Add("некоректна дата або час");
        }

        if (IsTransactionSharedInput && GetSelectedTransactionShareGroupIds().Length == 0)
        {
            reasons.Add("увімкнено спільний доступ, але не вибрана група");
        }

        validationMessage = reasons.Count == 0
            ? string.Empty
            : $"{Texts.TransactionFormValidationText} Причина: {string.Join("; ", reasons)}.";
        return reasons.Count == 0;
    }

    private async Task AdjustSavingCurrentAmountAfterTransactionEditAsync(
        TransactionItemViewModel? originalTransaction,
        decimal newAmount,
        string newType)
    {
        if (originalTransaction?.SavingId is not { } savingId)
        {
            return;
        }

        var oldEffect = GetSavingTransactionEffect(originalTransaction.Type, originalTransaction.Amount);
        var newEffect = GetSavingTransactionEffect(newType, newAmount);
        var delta = newEffect - oldEffect;
        if (delta == 0)
        {
            return;
        }

        var savings = await _apiClient!.GetSavingsAsync();
        var saving = savings
            .GroupBy(s => s.Id)
            .Select(group => group.FirstOrDefault(s => !s.GroupId.HasValue) ?? group.First())
            .FirstOrDefault(s => s.Id == savingId);
        if (saving is null)
        {
            return;
        }

        var updatedCurrentAmount = Math.Max(0, saving.CurrentAmount + delta);
        var targetAmount = saving.TargetAmount;
        await _apiClient.UpdateSavingAsync(
            saving.Id,
            new UpdateSavingRequestDto(
                saving.Name,
                targetAmount,
                updatedCurrentAmount,
                saving.Deadline,
                saving.IsCompleted,
                saving.Currency,
                saving.IconKey,
                saving.Color));

        if (ActiveSavingId == savingId || CurrentPage == SavingsPage)
        {
            await LoadSavingsAsync(savingId);
        }
    }

    private static decimal GetSavingTransactionEffect(string type, decimal amount) =>
        string.Equals(type, "income", StringComparison.OrdinalIgnoreCase)
            ? -amount
            : amount;

    [RelayCommand]
    private void BeginCreateTransaction()
    {
        IsTransactionDialogOpen = false;
        ResetTransactionForm();
        if (Groups.Count == 0)
        {
            _ = LoadGroupsAsync();
        }
        TransactionShareGroups = BuildGroupSelections([]);
        ApiStatusMessage = string.Empty;
        TransactionFormStatusMessage = string.Empty;
    }

    [RelayCommand]
    private async Task EditTransactionAsync(TransactionItemViewModel? transaction)
    {
        if (transaction is null)
        {
            return;
        }

        ActiveTransaction = transaction;
        TransactionFormStatusMessage = string.Empty;
        EditingTransactionId = transaction.Id;
        CanEditTransactionShareGroups = IsOwnTransaction(transaction);
        TransactionType = transaction.Type;
        TransactionAmountInput = transaction.Amount.ToString("0.##", CultureInfo.InvariantCulture);
        TransactionCurrencyInput = transaction.Currency;
        var categoryCode = transaction.SavingId is not null
            ? (await ResolveSavingsTransferCategoryIdAsync(transaction.Type)).ToString(CultureInfo.InvariantCulture)
            : transaction.CategoryCode;
        UpdateTransactionCategoryOptions(categoryCode);
        TransactionDescriptionInput = transaction.Description ?? string.Empty;
        var selectedGroupIds = transaction.GroupIds.ToArray();
        IsTransactionSharedInput = selectedGroupIds.Length > 0;
        SelectedTransactionShareGroup = selectedGroupIds.Length > 0
            ? Groups.FirstOrDefault(g => g.Id == selectedGroupIds[0])
            : null;
        TransactionShareGroups = BuildGroupSelections(selectedGroupIds);
        if (selectedGroupIds.Length > 0 && TransactionShareGroups.Count == 0)
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

    [RelayCommand]
    private async Task DeleteTransactionDialogAsync()
    {
        if (IsEditingScheduledPayment && EditingTransactionId.HasValue)
        {
            if (IsApiBusy)
            {
                return;
            }

            try
            {
                IsApiBusy = true;
                EnsureApiClient();
                await _apiClient!.DeletePlannedTransactionAsync(EditingTransactionId.Value);
                ApiStatusMessage = Texts.TransactionDeletedText;
                IsTransactionDialogOpen = false;
                ResetTransactionForm();
                await LoadScheduledPaymentsAsync();
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

            return;
        }

        await DeleteTransactionAsync(null);
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
            if (transactions.Any(t => t.GroupId.HasValue) && Groups.Count == 0)
            {
                await LoadGroupsAsync();
            }
            ViewModelLog.Information("Transactions received from API: Count={Count}", transactions.Count);
            AllTransactions = new ObservableCollection<TransactionItemViewModel>(
                transactions
                    .GroupBy(t => t.Id)
                    .Select(group =>
                    {
                        var t = group.FirstOrDefault(x => !x.GroupId.HasValue) ?? group.First();
                        var groupIds = group.Where(x => x.GroupId.HasValue).Select(x => x.GroupId!.Value).Distinct().ToArray();
                        var displayGroupId = groupIds.FirstOrDefault();
                        return new TransactionItemViewModel(
                            t.Id,
                            t.AccountId,
                            groupIds.Length > 0 ? displayGroupId : null,
                            ResolveTransactionGroupName(groupIds.Length > 0 ? displayGroupId : null, group.FirstOrDefault(x => x.GroupId == displayGroupId)?.GroupName),
                            t.SharedBy,
                            GetTransactionType(t.CategoryId),
                            t.SavingId,
                            t.Amount,
                            GetAccountCurrency(t.AccountId),
                            t.CategoryId.ToString(CultureInfo.InvariantCulture),
                            GetCategoryDisplayName(t.CategoryId.ToString(CultureInfo.InvariantCulture)),
                            GetCategoryIconKey(t.CategoryId.ToString(CultureInfo.InvariantCulture)),
                            GetCategoryColor(t.CategoryId.ToString(CultureInfo.InvariantCulture)),
                            ResolveGroupColor(groupIds.Length > 0 ? displayGroupId : null),
                            t.Description,
                            t.TransactionDate.ToLocalTime(),
                            GetTransactionType(t.CategoryId) == "income" ? Texts.TransactionIncomeType : Texts.TransactionExpenseType,
                            groupIds,
                            GetAccountName(t.AccountId));
                    }));

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
