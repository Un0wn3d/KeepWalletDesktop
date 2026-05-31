using CommunityToolkit.Mvvm.Input;
using KeepWalletDesktop.Models;
using KeepWalletDesktop.Services;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace KeepWalletDesktop.ViewModels;

public partial class MainViewModel
{
    [RelayCommand]
    private void SetScheduledPaymentsFilter(string filter)
    {
        ScheduledPaymentsFilter = string.IsNullOrWhiteSpace(filter) ? "all" : filter.Trim().ToLowerInvariant();
    }

    private async Task LoadScheduledPaymentsAsync()
    {
        if (!IsApiAuthorized)
        {
            return;
        }

        try
        {
            EnsureApiClient();
            var payments = await _apiClient!.GetPlannedTransactionsAsync();
            ScheduledPayments = new ObservableCollection<ScheduledPaymentItemViewModel>(
                payments.Select(p => new ScheduledPaymentItemViewModel(
                    p.Id,
                    p.RecurringPaymentId,
                    p.Name,
                    p.AccountId,
                    p.CategoryId,
                    p.Amount,
                    GetTransactionType(p.CategoryId),
                    GetCategoryDisplayName(p.CategoryId.ToString(CultureInfo.InvariantCulture)),
                    GetCategoryIconKey(p.CategoryId.ToString(CultureInfo.InvariantCulture)),
                    GetCategoryColor(p.CategoryId.ToString(CultureInfo.InvariantCulture)),
                    p.Description,
                    ResolveScheduledPaymentCurrency(p.AccountId),
                    ToScheduledPaymentDueDate(p.NextDueDate),
                    p.RepeatInterval,
                    FormatRepeatIntervalDisplay(p.RepeatInterval),
                    p.IsActive,
                    $"{p.Amount:N2}",
                    p.IsActive ? Texts.ScheduledPaymentPendingStatus : Texts.ScheduledPaymentPaidStatus,
                    FormatDaysRemainingDisplay(p.NextDueDate, p.IsActive))));
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

    private async Task LoadBankAccountsAsync()
    {
        if (!IsApiAuthorized)
        {
            return;
        }

        try
        {
            EnsureApiClient();
            var accounts = await _apiClient!.GetBankAccountsAsync();
            BankAccounts = new ObservableCollection<BankAccountItemViewModel>(
                accounts.Select(a => new BankAccountItemViewModel(
                    a.Id,
                    a.Name,
                    a.Currency,
                    a.Balance,
                    a.IsDefault,
                    a.GroupId,
                    $"{a.Balance:N2} {a.Currency}")));

            SelectedBankAccount = BankAccounts.FirstOrDefault(a => SelectedBankAccount?.Id == a.Id)
                ?? BankAccounts.FirstOrDefault(a => a.IsDefault)
                ?? BankAccounts.FirstOrDefault();
            defaultAccountId = SelectedBankAccount?.Id;
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
    private void BeginCreateBankAccount()
    {
        ResetBankAccountForm();
        if (Groups.Count == 0)
        {
            _ = LoadGroupsAsync();
        }
        BankAccountStatusMessage = string.Empty;
        IsBankAccountDialogOpen = true;
    }

    [RelayCommand]
    private async Task SelectBankAccountAsync(BankAccountItemViewModel? account)
    {
        if (account is null)
        {
            return;
        }

        SelectedBankAccount = account;
        CurrentPage = HomePage;
        await LoadTransactionsAsync();
    }

    [RelayCommand]
    private void EditBankAccount(BankAccountItemViewModel? account)
    {
        if (account is null)
        {
            return;
        }

        EditingBankAccountId = account.Id;
        BankAccountNameInput = account.Name;
        BankAccountCurrencyInput = account.Currency;
        BankAccountBalanceInput = account.Balance.ToString("0.##", CultureInfo.InvariantCulture);
        IsBankAccountDefaultInput = account.IsDefault;
        IsBankAccountSharedInput = account.GroupId.HasValue;
        SelectedBankAccountShareGroup = account.GroupId.HasValue
            ? Groups.FirstOrDefault(g => g.Id == account.GroupId.Value)
            : null;
        if (account.GroupId.HasValue && SelectedBankAccountShareGroup is null)
        {
            _ = LoadGroupsAsync();
        }
        BankAccountStatusMessage = string.Empty;
        IsBankAccountDialogOpen = true;
    }

    [RelayCommand]
    private void CloseBankAccountDialog()
    {
        IsBankAccountDialogOpen = false;
        ResetBankAccountForm();
    }

    [RelayCommand]
    private async Task SaveBankAccountAsync()
    {
        if (IsApiBusy)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(BankAccountNameInput) ||
            string.IsNullOrWhiteSpace(BankAccountCurrencyInput) ||
            !decimal.TryParse(BankAccountBalanceInput, NumberStyles.Number, CultureInfo.InvariantCulture, out var balance) ||
            balance < 0 ||
            (IsBankAccountSharedInput && SelectedBankAccountShareGroup is null))
        {
            BankAccountStatusMessage = Texts.TransactionFormValidationText;
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();

            var name = BankAccountNameInput.Trim();
            var currency = BankAccountCurrencyInput.Trim().ToUpperInvariant();
            var groupId = IsBankAccountSharedInput ? SelectedBankAccountShareGroup?.Id : null;
            var account = EditingBankAccountId.HasValue
                ? await _apiClient!.UpdateBankAccountAsync(
                    EditingBankAccountId.Value,
                    new UpdateBankAccountRequestDto(name, currency, balance, IsBankAccountDefaultInput, groupId))
                : await _apiClient!.CreateBankAccountAsync(
                    new CreateBankAccountRequestDto(name, currency, balance, IsBankAccountDefaultInput, groupId));

            if (account.IsDefault)
            {
                defaultAccountId = account.Id;
                TransactionCurrencyInput = account.Currency;
            }

            IsBankAccountDialogOpen = false;
            ResetBankAccountForm();
            await LoadBankAccountsAsync();
            SelectedBankAccount = BankAccounts.FirstOrDefault(a => a.Id == account.Id) ?? SelectedBankAccount;
            await LoadTransactionsAsync();
        }
        catch (HttpRequestException ex)
        {
            BankAccountStatusMessage = GetApiErrorMessage(ex);
        }
        catch (Exception ex)
        {
            BankAccountStatusMessage = $"{Texts.RequestErrorPrefix}: {ex.Message}";
        }
        finally
        {
            IsApiBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteBankAccountAsync()
    {
        if (IsApiBusy || !EditingBankAccountId.HasValue)
        {
            return;
        }

        if (BankAccounts.Count <= 1)
        {
            BankAccountStatusMessage = Texts.LastBankAccountRequired;
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();
            await _apiClient!.DeleteBankAccountAsync(EditingBankAccountId.Value);

            IsBankAccountDialogOpen = false;
            ResetBankAccountForm();
            await LoadBankAccountsAsync();
            await LoadTransactionsAsync();
        }
        catch (HttpRequestException ex)
        {
            BankAccountStatusMessage = GetApiErrorMessage(ex);
        }
        catch (Exception ex)
        {
            BankAccountStatusMessage = $"{Texts.RequestErrorPrefix}: {ex.Message}";
        }
        finally
        {
            IsApiBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddScheduledPaymentAsync()
    {
        if (IsApiBusy ||
            string.IsNullOrWhiteSpace(ScheduledPaymentNameInput) ||
            !decimal.TryParse(ScheduledPaymentAmountInput, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) ||
            amount <= 0 ||
            SelectedScheduledPaymentCategory is null ||
            !TryParseTransactionDate(ScheduledPaymentDateInput, out var dueDate))
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

            var accountId = await EnsureDefaultAccountAsync();
            var categoryId = GetCategoryId(SelectedScheduledPaymentCategory.Code);
            if (!TryBuildScheduledRepeatInterval(out var repeatInterval))
            {
                ApiStatusMessage = Texts.TransactionFormValidationText;
                return;
            }

            await _apiClient!.CreatePlannedTransactionAsync(new CreatePlannedTransactionRequestDto(
                ScheduledPaymentNameInput.Trim(),
                accountId,
                categoryId,
                amount,
                string.IsNullOrWhiteSpace(ScheduledPaymentDescriptionInput) ? null : ScheduledPaymentDescriptionInput.Trim(),
                DateOnly.FromDateTime(dueDate.LocalDateTime.Date),
                repeatInterval));

            ScheduledPaymentNameInput = string.Empty;
            ScheduledPaymentAmountInput = string.Empty;
            ScheduledPaymentDescriptionInput = string.Empty;
            ScheduledPaymentDateInput = DateTimeOffset.Now.ToString(DateTimeDisplayFormat, CultureInfo.InvariantCulture);
            SelectedScheduledRepeatOption = ScheduledRepeatOptions.FirstOrDefault(x => x.Code == "once");
            ResetScheduledCustomRepeatBuilder();
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
    }

    [RelayCommand]
    private async Task ToggleScheduledPaymentAsync(ScheduledPaymentItemViewModel? payment)
    {
        if (payment is null || IsApiBusy)
        {
            return;
        }

        try
        {
            if (BankAccounts.Count == 0)
            {
                await LoadBankAccountsAsync();
            }

            var paymentCurrency = BankAccounts
                .FirstOrDefault(a => a.Id == payment.AccountId)?
                .Currency;

            if (string.IsNullOrWhiteSpace(paymentCurrency))
            {
                ApiStatusMessage = "Не удалось определить валюту запланированной транзакции.";
                return;
            }

            var compatibleAccounts = BankAccounts
                .Where(a => string.Equals(a.Currency, paymentCurrency, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(a => a.Id == payment.AccountId)
                .ThenByDescending(a => a.IsDefault)
                .ThenBy(a => a.Name)
                .ToList();

            if (compatibleAccounts.Count == 0)
            {
                ApiStatusMessage = $"Нет доступных счетов в валюте {paymentCurrency}.";
                return;
            }

            PendingScheduledPaymentConfirmation = payment;
            ScheduledPaymentConfirmCurrency = paymentCurrency;
            ScheduledPaymentConfirmAccounts = new ObservableCollection<BankAccountItemViewModel>(compatibleAccounts);
            SelectedScheduledPaymentConfirmAccount = compatibleAccounts.FirstOrDefault(a => a.Id == payment.AccountId)
                ?? compatibleAccounts.FirstOrDefault(a => a.IsDefault)
                ?? compatibleAccounts.FirstOrDefault();
            IsScheduledPaymentConfirmDialogOpen = true;
        }
        catch (Exception ex)
        {
            ApiStatusMessage = $"{Texts.RequestErrorPrefix}: {ex.Message}";
        }
    }

    [RelayCommand]
    private void CloseScheduledPaymentConfirmDialog()
    {
        IsScheduledPaymentConfirmDialogOpen = false;
        PendingScheduledPaymentConfirmation = null;
        ScheduledPaymentConfirmAccounts = [];
        SelectedScheduledPaymentConfirmAccount = null;
        ScheduledPaymentConfirmCurrency = string.Empty;
    }

    [RelayCommand]
    private async Task ConfirmScheduledPaymentWithAccountAsync()
    {
        if (IsApiBusy || PendingScheduledPaymentConfirmation is null || SelectedScheduledPaymentConfirmAccount is null)
        {
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();
            await _apiClient!.ConfirmPlannedTransactionAsync(
                PendingScheduledPaymentConfirmation.Id,
                new ConfirmPlannedTransactionRequestDto(SelectedScheduledPaymentConfirmAccount.Id));
            CloseScheduledPaymentConfirmDialog();
            await LoadScheduledPaymentsAsync();
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
    private async Task ConfirmScheduledPaymentFromDialogAsync()
    {
        if (!EditingTransactionId.HasValue)
        {
            return;
        }

        var payment = ScheduledPayments.FirstOrDefault(p => p.Id == EditingTransactionId.Value);
        if (payment is null)
        {
            return;
        }

        IsTransactionDialogOpen = false;
        await ToggleScheduledPaymentAsync(payment);
    }

    [RelayCommand]
    private async Task DeleteScheduledPaymentAsync(ScheduledPaymentItemViewModel? payment)
    {
        if (payment is null || IsApiBusy)
        {
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();
            await _apiClient!.DeletePlannedTransactionAsync(payment.Id);
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
    }

    [RelayCommand]
    private async Task EditScheduledPaymentAsync(ScheduledPaymentItemViewModel? payment)
    {
        if (payment is null || IsApiBusy)
        {
            return;
        }

        if (apiCategories.Count == 0)
        {
            await LoadCategoriesAsync();
        }

        if (BankAccounts.Count == 0)
        {
            await LoadBankAccountsAsync();
        }

        var account = BankAccounts.FirstOrDefault(a => a.Id == payment.AccountId);
        if (account is not null)
        {
            SelectedBankAccount = account;
            TransactionCurrencyInput = account.Currency;
        }

        ActiveTransaction = null;
        EditingTransactionId = payment.Id;
        EditingScheduledPaymentId = payment.RecurringPaymentId;
        TransactionType = payment.Type;
        TransactionAmountInput = payment.Amount.ToString("0.##", CultureInfo.InvariantCulture);
        TransactionDescriptionInput = payment.Description ?? string.Empty;
        IsTransactionSharedInput = false;
        SelectedTransactionShareGroup = null;
        UpdateTransactionCategoryOptions(payment.CategoryId.ToString(CultureInfo.InvariantCulture));
        ApplyScheduledRepeatSelection(payment.RepeatInterval);

        var dueDate = payment.DueDate.ToLocalTime();
        TransactionDateInput = dueDate.ToString(DateDisplayFormat, CultureInfo.InvariantCulture);
        TransactionSelectedDate = dueDate;
        TransactionSelectedTime = dueDate.TimeOfDay;
        TransactionTimeInput = dueDate.ToString("HH:mm", CultureInfo.InvariantCulture);

        IsAllTransactionsDialogOpen = false;
        IsScheduledPaymentConfirmDialogOpen = false;
        IsTransactionDialogOpen = true;
        ApiStatusMessage = string.Empty;
    }

    private string FormatRepeatIntervalDisplay(TimeSpan value) =>
        value.TotalDays switch
        {
            <= 0 => Texts.ScheduledRepeatOnce,
            <= 1 => Texts.ScheduledRepeatDaily,
            7 => Texts.ScheduledRepeatWeekly,
            >= 28 and <= 31 => Texts.ScheduledRepeatMonthly,
            >= 365 and <= 366 => Texts.ScheduledRepeatYearly,
            _ => BuildCustomRepeatDisplay(Math.Max(1, (int)Math.Round(value.TotalDays)))
        };

    private string BuildCustomRepeatDisplay(int totalDays)
    {
        var years = totalDays / 365;
        totalDays %= 365;
        var months = totalDays / 30;
        totalDays %= 30;
        var weeks = totalDays / 7;
        totalDays %= 7;
        var days = totalDays;

        var parts = new[]
        {
            years > 0 ? $"{years} {(years == 1 ? Texts.ScheduledRepeatYearUnit : Texts.ScheduledRepeatYearsUnit)}" : null,
            months > 0 ? $"{months} {(months == 1 ? Texts.ScheduledRepeatMonthUnit : Texts.ScheduledRepeatMonthsUnit)}" : null,
            weeks > 0 ? $"{weeks} {(weeks == 1 ? Texts.ScheduledRepeatWeekUnit : Texts.ScheduledRepeatWeeksUnit)}" : null,
            days > 0 ? $"{days} {(days == 1 ? Texts.ScheduledRepeatDayUnit : Texts.ScheduledRepeatDaysUnit)}" : null
        }.Where(x => x is not null);

        return string.Join(" ", parts!);
    }

    private string ResolveScheduledPaymentCurrency(Guid accountId) =>
        BankAccounts.FirstOrDefault(a => a.Id == accountId)?.Currency ?? "UAH";

    private static DateTimeOffset ToScheduledPaymentDueDate(DateOnly dueDate) =>
        new(dueDate.ToDateTime(TimeOnly.MinValue), TimeZoneInfo.Local.GetUtcOffset(DateTime.Now));

    private string FormatDaysRemainingDisplay(DateOnly dueDate, bool isActive)
    {
        if (!isActive)
        {
            return Texts.ScheduledPaymentPaidStatus;
        }

        var days = (dueDate.ToDateTime(TimeOnly.MinValue) - DateTime.Today).Days;
        if (days < 0)
        {
            return $"{Texts.ScheduledPaymentsOverduePrefix} {-days} {Texts.ScheduledPaymentsDayShort}";
        }

        return days == 0
            ? Texts.ScheduledPaymentsToday
            : $"{days} {Texts.ScheduledPaymentsDayShort}";
    }
}
