using CommunityToolkit.Mvvm.Input;
using KeepWalletDesktop.Models;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace KeepWalletDesktop.ViewModels;

public partial class MainViewModel
{    private async Task LoadBankAccountsAsync()
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
                accounts
                    .GroupBy(a => a.Id)
                    .Select(group =>
                    {
                        var a = group.FirstOrDefault(x => !x.GroupId.HasValue) ?? group.First();
                        var groupIds = group.Where(x => x.GroupId.HasValue).Select(x => x.GroupId!.Value).Distinct().ToArray();
                        var displayGroupId = groupIds.FirstOrDefault();
                        return new BankAccountItemViewModel(
                            a.Id,
                            a.Name,
                            a.Currency,
                            a.Balance,
                            a.IsDefault,
                            groupIds.Length > 0 ? displayGroupId : null,
                            $"{a.Balance:N2} {a.Currency}",
                            ResolveBankAccountGroupName(groupIds.Length > 0 ? displayGroupId : null, a.GroupName),
                            ResolveBankAccountGroupColor(groupIds.Length > 0 ? displayGroupId : null, a.GroupColor),
                            ResolveBankAccountSharedBy(a.UserId, a.GroupOwnerDisplay),
                            a.UserId,
                            groupIds);
                    }));

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
        BankAccountShareGroups = BuildGroupSelections([]);
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
        CanEditBankAccountShareGroups = IsOwnBankAccount(account);
        BankAccountNameInput = account.Name;
        BankAccountCurrencyInput = account.Currency;
        BankAccountBalanceInput = account.Balance.ToString("0.##", CultureInfo.InvariantCulture);
        IsBankAccountDefaultInput = account.IsDefault;
        var selectedGroupIds = account.GroupIds.ToArray();
        IsBankAccountSharedInput = selectedGroupIds.Length > 0;
        SelectedBankAccountShareGroup = selectedGroupIds.Length > 0
            ? Groups.FirstOrDefault(g => g.Id == selectedGroupIds[0])
            : null;
        BankAccountShareGroups = BuildGroupSelections(selectedGroupIds);
        if (selectedGroupIds.Length > 0 && BankAccountShareGroups.Count == 0)
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
            (IsBankAccountSharedInput && GetSelectedBankAccountShareGroupIds().Length == 0))
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
            var groupIds = GetSelectedBankAccountShareGroupIds();
            var account = EditingBankAccountId.HasValue
                ? await _apiClient!.UpdateBankAccountAsync(
                    EditingBankAccountId.Value,
                    new UpdateBankAccountRequestDto(name, currency, balance, IsBankAccountDefaultInput))
                : await _apiClient!.CreateBankAccountAsync(
                    new CreateBankAccountRequestDto(name, currency, balance, IsBankAccountDefaultInput));

            var canUpdateGroups = !EditingBankAccountId.HasValue ||
                BankAccounts.FirstOrDefault(a => a.Id == EditingBankAccountId.Value) is { } sourceAccount &&
                IsOwnBankAccount(sourceAccount);
            if (canUpdateGroups)
            {
                await _apiClient!.UpdateBankAccountGroupsAsync(account.Id, new ReplaceResourceGroupsRequestDto(groupIds));
            }

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


    private bool IsOwnBankAccount(BankAccountItemViewModel account) =>
        account.UserId.HasValue
            ? account.UserId == CurrentUserId
            : !account.GroupId.HasValue || string.IsNullOrWhiteSpace(account.OwnerDisplayText) || account.OwnerDisplayText == "-";

    private string? ResolveBankAccountGroupName(Guid? groupId, string? apiGroupName) =>
        !string.IsNullOrWhiteSpace(apiGroupName)
            ? apiGroupName
            : groupId.HasValue
                ? Groups.FirstOrDefault(g => g.Id == groupId.Value)?.Name
                : null;

    private string? ResolveBankAccountGroupColor(Guid? groupId, string? apiGroupColor) =>
        !string.IsNullOrWhiteSpace(apiGroupColor)
            ? apiGroupColor
            : groupId.HasValue
                ? Groups.FirstOrDefault(g => g.Id == groupId.Value)?.SelectedColor
                : null;

    private string? ResolveBankAccountSharedBy(Guid accountUserId, string? apiSharedBy) =>
        !string.IsNullOrWhiteSpace(apiSharedBy)
            ? apiSharedBy
            : CurrentUserId == accountUserId && !string.IsNullOrWhiteSpace(CurrentUsername)
                ? CurrentUsername
                : null;
}
