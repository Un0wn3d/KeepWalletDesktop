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
                    p.Name,
                    p.AccountId,
                    p.CategoryId,
                    p.Amount,
                    GetTransactionType(p.CategoryId),
                    GetCategoryDisplayName(p.CategoryId.ToString(CultureInfo.InvariantCulture)),
                    p.Description,
                    p.NextDueDate.ToDateTime(TimeOnly.MinValue).ToString(DateTimeDisplayFormat, CultureInfo.InvariantCulture),
                    FormatRepeatInterval(p.RepeatInterval),
                    p.IsActive,
                    $"{p.Amount:N2}",
                    p.IsActive ? Texts.ScheduledPaymentPendingStatus : Texts.ScheduledPaymentPaidStatus)));
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
            !TryParseDateOnly(ScheduledPaymentDateInput, out var dueDate))
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
            var repeatInterval = ParseRepeatInterval(IsCustomScheduledRepeat
                ? ScheduledPaymentCustomRepeatInput
                : SelectedScheduledRepeatOption);

            await _apiClient!.CreatePlannedTransactionAsync(new CreatePlannedTransactionRequestDto(
                ScheduledPaymentNameInput.Trim(),
                accountId,
                categoryId,
                amount,
                string.IsNullOrWhiteSpace(ScheduledPaymentDescriptionInput) ? null : ScheduledPaymentDescriptionInput.Trim(),
                dueDate,
                repeatInterval));

            ScheduledPaymentNameInput = string.Empty;
            ScheduledPaymentAmountInput = string.Empty;
            ScheduledPaymentDescriptionInput = string.Empty;
            ScheduledPaymentDateInput = DateOnly.FromDateTime(DateTime.Today).ToDateTime(TimeOnly.MinValue).ToString(DateTimeDisplayFormat);
            SelectedScheduledRepeatOption = "once";
            ScheduledPaymentCustomRepeatInput = string.Empty;
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
            IsApiBusy = true;
            EnsureApiClient();
            await _apiClient!.ConfirmPlannedTransactionAsync(payment.Id);
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

    private static string FormatRepeatInterval(TimeSpan value) =>
        value.TotalDays switch
        {
            <= 0 => "once",
            <= 1 => "daily",
            7 => "weekly",
            >= 28 and <= 31 => "monthly",
            >= 365 and <= 366 => "yearly",
            _ => $"every {Math.Max(1, (int)Math.Round(value.TotalDays))} days"
        };

    private static TimeSpan ParseRepeatInterval(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "daily" or "day" => TimeSpan.FromDays(1),
            "weekly" or "week" => TimeSpan.FromDays(7),
            "monthly" or "month" => TimeSpan.FromDays(30),
            "yearly" or "year" => TimeSpan.FromDays(365),
            "once" => TimeSpan.Zero,
            _ => ParseCustomRepeatInterval(normalized)
        };
    }

    private static TimeSpan ParseCustomRepeatInterval(string value)
    {
        var totalDays = 0;
        foreach (Match match in Regex.Matches(value, @"(\d+)\s*([a-zа-яіїєґ]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            var amount = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            var unit = match.Groups[2].Value.ToLowerInvariant();
            totalDays += unit switch
            {
                "day" or "days" or "день" or "дні" or "днів" or "дня" => amount,
                "week" or "weeks" or "тиждень" or "тижні" or "тижнів" => amount * 7,
                "month" or "months" or "місяць" or "місяці" or "місяців" or "месяц" or "месяца" or "месяцев" => amount * 30,
                "year" or "years" or "рік" or "роки" or "років" or "год" or "года" or "лет" => amount * 365,
                _ => 0
            };
        }

        return TimeSpan.FromDays(Math.Max(0, totalDays));
    }
}
