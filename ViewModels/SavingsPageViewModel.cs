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
    private void CreateNewSavings()
    {
        SavingsDialogNameInput = string.Empty;
        SavingsDialogTargetAmountInput = string.Empty;
        SavingsDialogDeadlineInput = string.Empty;
        SavingsDialogDeadlineDate = null;
        SavingsDialogCurrency = OnboardingSelectedCurrency;
        SavingsDialogIconKey = "other";
        SavingsDialogColor = "#3B82F6";
        SavingsStatusMessage = string.Empty;
        IsSavingsDialogOpen = true;
    }

    [RelayCommand]
    private void CloseSavingsDialog()
    {
        IsSavingsDialogOpen = false;
    }

    [RelayCommand]
    private async Task SaveNewSavingsAsync()
    {
        if (IsApiBusy)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(SavingsDialogNameInput) ||
            !TryParseOptionalPositiveDecimal(SavingsDialogTargetAmountInput, out var targetAmount))
        {
            SavingsStatusMessage = Texts.TransactionFormValidationText;
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();
            DateOnly? deadline = SavingsDialogDeadlineDate.HasValue
                ? DateOnly.FromDateTime(SavingsDialogDeadlineDate.Value.Date)
                : null;

            var saving = await _apiClient!.CreateSavingAsync(new CreateSavingRequestDto(
                SavingsDialogNameInput.Trim(),
                targetAmount > 0 ? targetAmount : null,
                0,
                deadline,
                SavingsDialogCurrency,
                SavingsDialogIconKey,
                SavingsDialogColor));

            ApplySaving(saving);
            await LoadSavingsAsync();
            await LoadSavingDetailsAsync(saving.Id);
            await LoadBankAccountsAsync();
            await LoadTransactionsAsync();
            IsSavingsDialogOpen = false;
            SavingsStatusMessage = Texts.TransactionSavedText;
        }
        catch (HttpRequestException ex)
        {
            SavingsStatusMessage = GetApiErrorMessage(ex);
        }
        catch (Exception ex)
        {
            SavingsStatusMessage = $"{Texts.RequestErrorPrefix}: {ex.Message}";
        }
        finally
        {
            IsApiBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveGoalAsync()
    {
        if (IsApiBusy)
        {
            return;
        }

        if (!IsEditingSavingDetails)
        {
            IsEditingSavingDetails = true;
            IsEditingSavingGroups = true;
            IsEditingSavingNameField = true;
            IsEditingSavingGoalField = true;
            IsEditingSavingCurrentField = true;
            IsEditingSavingCurrencyField = true;
            IsEditingSavingDeadlineField = true;
            return;
        }

        var savingGroupIds = GetSelectedSavingShareGroupIds();
        if (!decimal.TryParse(SavingGoalAmountEditInput, NumberStyles.Number, CultureInfo.InvariantCulture, out var goalAmountValue) ||
            goalAmountValue < 0 ||
            !decimal.TryParse(SavingCurrentAmountEditInput, NumberStyles.Number, CultureInfo.InvariantCulture, out var currentAmountValue) ||
            currentAmountValue < 0 ||
            string.IsNullOrWhiteSpace(SavingNameEditInput) ||
            (IsSavingSharedInput && savingGroupIds.Length == 0))
        {
            SavingsStatusMessage = Texts.TransactionFormValidationText;
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();

            decimal? targetAmount = goalAmountValue > 0 ? goalAmountValue : null;
            var normalizedName = SavingNameEditInput.Trim();
            var deadline = SavingDeadlineEditDate.HasValue
                ? DateOnly.FromDateTime(SavingDeadlineEditDate.Value.Date)
                : (DateOnly?)null;
            ApiSavingDto saving;

            if (ActiveSavingId.HasValue)
            {
                saving = await _apiClient!.UpdateSavingAsync(
                        ActiveSavingId.Value,
                        new UpdateSavingRequestDto(
                        string.IsNullOrWhiteSpace(normalizedName) ? "Savings" : normalizedName,
                        targetAmount,
                        currentAmountValue,
                        deadline,
                        ActiveSavingIsCompleted,
                        string.IsNullOrWhiteSpace(SavingCurrencyEditInput) ? "UAH" : SavingCurrencyEditInput.Trim().ToUpperInvariant(),
                        ActiveSavingIconKey,
                        ActiveSavingColor));
            }
            else
            {
                saving = await _apiClient!.CreateSavingAsync(new CreateSavingRequestDto(
                    string.IsNullOrWhiteSpace(normalizedName) ? "Savings" : normalizedName,
                    targetAmount,
                    currentAmountValue,
                    deadline,
                    string.IsNullOrWhiteSpace(SavingCurrencyEditInput) ? "UAH" : SavingCurrencyEditInput.Trim().ToUpperInvariant(),
                    ActiveSavingIconKey,
                    ActiveSavingColor));
            }

            ApplySaving(saving);
            if (CanEditSavingShareGroups)
            {
                await _apiClient!.UpdateSavingGroupsAsync(saving.Id, new ReplaceResourceGroupsRequestDto(savingGroupIds));
            }

            await LoadSavingsAsync(saving.Id);
            IsEditingSavingDetails = false;
            IsEditingSavingGroups = false;
            IsEditingSavingNameField = false;
            IsEditingSavingGoalField = false;
            IsEditingSavingCurrentField = false;
            IsEditingSavingCurrencyField = false;
            IsEditingSavingDeadlineField = false;
            SavingsStatusMessage = Texts.TransactionUpdatedText;
        }
        catch (HttpRequestException ex)
        {
            SavingsStatusMessage = GetApiErrorMessage(ex);
        }
        catch (Exception ex)
        {
            SavingsStatusMessage = $"{Texts.RequestErrorPrefix}: {ex.Message}";
        }
        finally
        {
            IsApiBusy = false;
        }
    }

    [RelayCommand]
    private void BeginEditSavingName()
    {
        IsEditingSavingDetails = true;
        IsEditingSavingNameField = true;
    }

    [RelayCommand]
    private async Task SaveSavingNameOnBlurAsync()
    {
        if (!IsEditingSavingNameField || IsApiBusy || !ActiveSavingId.HasValue)
        {
            IsEditingSavingNameField = false;
            return;
        }

        var normalizedName = SavingNameEditInput?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            SavingNameEditInput = SavingNameInput;
            IsEditingSavingNameField = false;
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();
            var targetAmount = GoalAmount > 0 ? (decimal?)GoalAmount : null;
            var deadline = SavingDeadlineEditDate.HasValue ? DateOnly.FromDateTime(SavingDeadlineEditDate.Value.Date) : ActiveSavingDeadline;
            var updated = await _apiClient!.UpdateSavingAsync(
                ActiveSavingId.Value,
                new UpdateSavingRequestDto(
                    normalizedName,
                    targetAmount,
                    (decimal)SavedAmount,
                    deadline,
                    ActiveSavingIsCompleted,
                    string.IsNullOrWhiteSpace(ActiveSavingCurrency) ? "UAH" : ActiveSavingCurrency,
                    ActiveSavingIconKey,
                    ActiveSavingColor));
            ApplySaving(updated);
        }
        catch (Exception ex)
        {
            SavingsStatusMessage = ex.Message;
        }
        finally
        {
            IsEditingSavingNameField = false;
            IsApiBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveSavingGoalOnBlurAsync()
    {
        await SaveSavingInlineAsync(closeGoal: true, closeCurrent: false, closeCurrency: false);
    }

    [RelayCommand]
    private async Task SaveSavingCurrentOnBlurAsync()
    {
        await SaveSavingInlineAsync(closeGoal: false, closeCurrent: true, closeCurrency: false);
    }

    [RelayCommand]
    private async Task SaveSavingCurrencyOnSelectAsync()
    {
        await SaveSavingInlineAsync(closeGoal: false, closeCurrent: false, closeCurrency: true);
    }

    private async Task SaveSavingInlineAsync(bool closeGoal, bool closeCurrent, bool closeCurrency)
    {
        if (IsApiBusy || !ActiveSavingId.HasValue)
        {
            if (closeGoal) IsEditingSavingGoalField = false;
            if (closeCurrent) IsEditingSavingCurrentField = false;
            if (closeCurrency) IsEditingSavingCurrencyField = false;
            return;
        }

        if (!decimal.TryParse(SavingGoalAmountEditInput, NumberStyles.Number, CultureInfo.InvariantCulture, out var goalAmountValue) ||
            goalAmountValue < 0 ||
            !decimal.TryParse(SavingCurrentAmountEditInput, NumberStyles.Number, CultureInfo.InvariantCulture, out var currentAmountValue) ||
            currentAmountValue < 0)
        {
            if (closeGoal) IsEditingSavingGoalField = false;
            if (closeCurrent) IsEditingSavingCurrentField = false;
            if (closeCurrency) IsEditingSavingCurrencyField = false;
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();
            var targetAmount = goalAmountValue > 0 ? (decimal?)goalAmountValue : null;
            var deadline = SavingDeadlineEditDate.HasValue ? DateOnly.FromDateTime(SavingDeadlineEditDate.Value.Date) : ActiveSavingDeadline;
            var updated = await _apiClient!.UpdateSavingAsync(
                ActiveSavingId.Value,
                new UpdateSavingRequestDto(
                    string.IsNullOrWhiteSpace(SavingNameEditInput) ? "Savings" : SavingNameEditInput.Trim(),
                    targetAmount,
                    currentAmountValue,
                    deadline,
                    ActiveSavingIsCompleted,
                    string.IsNullOrWhiteSpace(SavingCurrencyEditInput) ? "UAH" : SavingCurrencyEditInput.Trim().ToUpperInvariant(),
                    ActiveSavingIconKey,
                    ActiveSavingColor));
            ApplySaving(updated);
            await LoadSavingDetailsAsync(updated.Id);
        }
        finally
        {
            if (closeGoal) IsEditingSavingGoalField = false;
            if (closeCurrent) IsEditingSavingCurrentField = false;
            if (closeCurrency) IsEditingSavingCurrencyField = false;
            IsApiBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveSavingIconColorImmediatelyAsync()
    {
        if (IsApiBusy || !ActiveSavingId.HasValue)
        {
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();
            var targetAmount = GoalAmount > 0 ? (decimal?)GoalAmount : null;
            var deadline = ActiveSavingDeadline;
            var updated = await _apiClient!.UpdateSavingAsync(
                ActiveSavingId.Value,
                new UpdateSavingRequestDto(
                    string.IsNullOrWhiteSpace(SavingNameInput) ? "Savings" : SavingNameInput.Trim(),
                    targetAmount,
                    (decimal)SavedAmount,
                    deadline,
                    ActiveSavingIsCompleted,
                    string.IsNullOrWhiteSpace(ActiveSavingCurrency) ? "UAH" : ActiveSavingCurrency,
                    ActiveSavingIconKey,
                    ActiveSavingColor));
            ApplySaving(updated);
        }
        finally
        {
            IsApiBusy = false;
        }
    }

    [RelayCommand]
    private void BeginEditSavingGroups()
    {
        IsEditingSavingGroups = true;
    }

    [RelayCommand]
    private void BeginEditSavingGoalAmount()
    {
        IsEditingSavingDetails = true;
        IsEditingSavingGoalField = true;
    }

    [RelayCommand]
    private void BeginEditSavingCurrentAmount()
    {
        IsEditingSavingDetails = true;
        IsEditingSavingCurrentField = true;
    }

    [RelayCommand]
    private void BeginEditSavingCurrency()
    {
        IsEditingSavingDetails = true;
        IsEditingSavingCurrencyField = true;
    }

    [RelayCommand]
    private void BeginEditSavingDeadline()
    {
        IsEditingSavingDetails = true;
        IsEditingSavingDeadlineField = true;
    }

    [RelayCommand]
    private async Task AddToSavingsAsync()
    {
        await TransferSavingsAsync(isDeposit: true);
    }

    [RelayCommand]
    private async Task WithdrawFromSavingsAsync()
    {
        await TransferSavingsAsync(isDeposit: false);
    }

    private async Task TransferSavingsAsync(bool isDeposit)
    {
        if (IsApiBusy)
        {
            return;
        }

        if (!TryParsePositiveAmount(AddAmount, out var transferAmount))
        {
            AddAmountError = string.IsNullOrWhiteSpace(AddAmount)
                ? "Сума має бути більше 0"
                : ValidatePositiveAmountInput(AddAmount);
            return;
        }

        if (SelectedSavingsBankAccount is null)
        {
            SavingsStatusMessage = Texts.TransactionFormValidationText;
            return;
        }

        if (!string.Equals(SelectedSavingsBankAccount.Currency, ActiveSavingCurrency, StringComparison.OrdinalIgnoreCase))
        {
            SavingsStatusMessage = Texts.TransactionFormValidationText;
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();

            var newCurrentAmount = isDeposit
                ? (decimal)SavedAmount + transferAmount
                : Math.Max(0, (decimal)SavedAmount - transferAmount);
            decimal? targetAmount = GoalAmount > 0 ? (decimal)GoalAmount : null;
            ApiSavingDto saving;

            if (ActiveSavingId.HasValue)
            {
                saving = await _apiClient!.UpdateSavingAsync(
                        ActiveSavingId.Value,
                        new UpdateSavingRequestDto(
                        string.IsNullOrWhiteSpace(SavingNameInput) ? "Savings" : SavingNameInput.Trim(),
                        targetAmount,
                        newCurrentAmount,
                        null,
                        ActiveSavingIsCompleted,
                        ActiveSavingCurrency,
                        ActiveSavingIconKey,
                        ActiveSavingColor));
            }
            else
            {
                saving = await _apiClient!.CreateSavingAsync(new CreateSavingRequestDto(
                    string.IsNullOrWhiteSpace(SavingNameInput) ? "Savings" : SavingNameInput.Trim(),
                    targetAmount,
                    newCurrentAmount,
                    null,
                    ActiveSavingCurrency,
                    ActiveSavingIconKey,
                    ActiveSavingColor));
            }

            await CreateSavingsAccountTransactionAsync(
                transferAmount,
                SelectedSavingsBankAccount,
                isDeposit,
                saving.Id,
                saving.Name);

            AddAmount = string.Empty;
            ApplySaving(saving);
            await LoadTransactionsAsync();
            await LoadSavingsAsync(saving.Id);
            await LoadBankAccountsAsync();
            SavingsStatusMessage = Texts.TransactionUpdatedText;
        }
        catch (HttpRequestException ex)
        {
            SavingsStatusMessage = GetApiErrorMessage(ex);
        }
        catch (Exception ex)
        {
            SavingsStatusMessage = $"{Texts.RequestErrorPrefix}: {ex.Message}";
        }
        finally
        {
            IsApiBusy = false;
        }
    }

    private async Task CreateSavingsAccountTransactionAsync(
        decimal amount,
        BankAccountItemViewModel account,
        bool isDeposit,
        int savingId,
        string savingName)
    {
        if (apiCategories.Count == 0)
        {
            await LoadCategoriesAsync();
        }

        var transactionType = isDeposit ? "expense" : "income";
        var categoryId = await ResolveSavingsTransferCategoryIdAsync(transactionType);

        await _apiClient!.CreateTransactionAsync(new CreateTransactionRequestDto(
            account.Id,
            account.GroupId,
            categoryId,
            savingId,
            null,
            amount,
            savingName,
            DateTimeOffset.Now.ToUniversalTime()));
    }

    private async Task<int> ResolveSavingsTransferCategoryIdAsync(string transactionType)
    {
        var categoryNames = new[]
            {
                Texts.SavingsTransferCategoryName,
                "Накопичення",
                "Savings"
            }
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (apiCategories.Count > 0)
        {
            var candidates = apiCategories
                .Where(c => string.Equals(c.Type, transactionType, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var preferred = candidates.FirstOrDefault(c =>
                categoryNames.Any(name => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)));

            if (preferred is not null)
            {
                return preferred.Id;
            }
        }

        var allCategories = await _apiClient!.GetCategoriesAsync();
        var systemCategory = allCategories.FirstOrDefault(c =>
            string.Equals(c.Type, transactionType, StringComparison.OrdinalIgnoreCase) &&
            categoryNames.Any(name => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)));
        if (systemCategory is not null)
        {
            return systemCategory.Id;
        }

        var created = await _apiClient!.CreateCategoryAsync(new CreateCategoryRequestDto(
            Texts.SavingsTransferCategoryName,
            transactionType,
            "savings",
            "#3B82F6"));

        await LoadCategoriesAsync();
        return created.Id;
    }

}
