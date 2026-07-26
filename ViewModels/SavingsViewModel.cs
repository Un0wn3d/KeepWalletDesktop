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
    private async Task LoadSavingsAsync(int? preferredSavingId = null)
    {
        if (!IsApiAuthorized)
        {
            return;
        }

        try
        {
            EnsureApiClient();
            var savings = await _apiClient!.GetSavingsAsync();
            if (savings.Any(s => s.GroupId.HasValue) && Groups.Count == 0)
            {
                await LoadGroupsAsync();
            }

            var activeSaving = preferredSavingId.HasValue
                ? savings.FirstOrDefault(s => s.Id == preferredSavingId.Value)
                : null;
            activeSaving ??= savings.FirstOrDefault(s => !s.IsCompleted) ?? savings.FirstOrDefault();

            if (activeSaving is null)
            {
                ActiveSavingId = null;
                SavingNameInput = "Savings";
                GoalAmount = 0;
                SavedAmount = 0;
                ActiveSavingCurrency = "UAH";
                ActiveSavingIconKey = "other";
                ActiveSavingColor = "#3B82F6";
                ActiveSavingDeadline = null;
                ActiveSavingIsCompleted = false;
                AddAmount = string.Empty;
                InterestRate = 0;
                Savings = [];
                SavingRateHistory = [];
                SavingItems = [];
                SavingsStatusMessage = string.Empty;
                RefreshSavingsProperties();
                return;
            }

            Savings = new ObservableCollection<SavingOptionViewModel>(
                savings
                    .GroupBy(s => s.Id)
                    .Select(group =>
                    {
                        var s = group.FirstOrDefault(x => !x.GroupId.HasValue) ?? group.First();
                        var groupIds = group.Where(x => x.GroupId.HasValue).Select(x => x.GroupId!.Value).Distinct().ToArray();
                        var displayGroupId = groupIds.FirstOrDefault();
                        var targetAmount = s.TargetAmount ?? 0;
                        var progress = targetAmount <= 0
                            ? 0
                            : Math.Clamp((double)(s.CurrentAmount / targetAmount * 100), 0, 100);
                        return new SavingOptionViewModel(
                            s.Id,
                            groupIds.Length > 0 ? displayGroupId : null,
                            s.Name,
                            s.IconKey ?? "other",
                            s.Color ?? "#3B82F6",
                            s.Currency,
                            BuildSavingDeadlineDisplay(s.Deadline),
                            $"{s.CurrentAmount:N2}",
                            $"{targetAmount:N2} {s.Currency}",
                            progress,
                            $"{progress:0}%",
                            s.Id == activeSaving.Id,
                            s.IsCompleted,
                            s.OwnerDisplay,
                            s.UserId,
                            groupIds);
                    }));
            ApplySaving(activeSaving);
            await LoadSavingDetailsAsync(activeSaving.Id);
        }
        catch (HttpRequestException ex)
        {
            SavingsStatusMessage = GetApiErrorMessage(ex);
        }
        catch (Exception ex)
        {
            SavingsStatusMessage = $"{Texts.RequestErrorPrefix}: {ex.Message}";
        }
    }

    private void ApplySaving(ApiSavingDto saving)
    {
        ActiveSavingId = saving.Id;
        SavingNameInput = saving.Name;
        GoalAmount = (double)(saving.TargetAmount ?? 0);
        SavedAmount = (double)saving.CurrentAmount;
        ActiveSavingCurrency = saving.Currency;
        ActiveSavingIconKey = string.IsNullOrWhiteSpace(saving.IconKey) ? "other" : saving.IconKey;
        ActiveSavingColor = string.IsNullOrWhiteSpace(saving.Color) ? "#3B82F6" : saving.Color;
        ActiveSavingDeadline = saving.Deadline;
        ActiveSavingIsCompleted = saving.IsCompleted;
        IsEditingSavingGroups = false;
        SavingNameEditInput = saving.Name;
        SavingGoalAmountEditInput = (saving.TargetAmount ?? 0).ToString("0.##", CultureInfo.InvariantCulture);
        SavingCurrentAmountEditInput = saving.CurrentAmount.ToString("0.##", CultureInfo.InvariantCulture);
        SavingCurrencyEditInput = string.IsNullOrWhiteSpace(saving.Currency) ? "UAH" : saving.Currency;
        SavingDeadlineEditDate = saving.Deadline?.ToDateTime(TimeOnly.MinValue);
        SelectedSavingsBankAccount = CompatibleSavingsBankAccounts.FirstOrDefault();
        InterestRate = 0;
        RefreshSavingsProperties();
        RefreshSavingTransactions();
    }

    [RelayCommand]
    private async Task SelectSavingAsync(SavingOptionViewModel? saving)
    {
        if (saving is null || IsApiBusy)
        {
            return;
        }

        EnsureApiClient();
        var selected = (await _apiClient!.GetSavingsAsync()).FirstOrDefault(s => s.Id == saving.Id);
        if (selected is null)
        {
            return;
        }

        ApplySaving(selected);
        Savings = new ObservableCollection<SavingOptionViewModel>(
            Savings.Select(s => s with { IsSelected = s.Id == selected.Id }));
        await LoadSavingDetailsAsync(selected.Id);
    }

    [RelayCommand]
    private void ToggleCompletedSavings()
    {
        AreCompletedSavingsVisible = !AreCompletedSavingsVisible;
    }

    [RelayCommand]
    private async Task SaveActiveSavingCompletionAsync()
    {
        if (!ActiveSavingId.HasValue || IsApiBusy)
        {
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();
            var targetAmount = GoalAmount > 0 ? (decimal?)GoalAmount : null;
            var updated = await _apiClient!.UpdateSavingAsync(
                ActiveSavingId.Value,
                new UpdateSavingRequestDto(
                    string.IsNullOrWhiteSpace(SavingNameInput) ? "Savings" : SavingNameInput.Trim(),
                    targetAmount,
                    (decimal)SavedAmount,
                    ActiveSavingDeadline,
                    ActiveSavingIsCompleted,
                    string.IsNullOrWhiteSpace(ActiveSavingCurrency) ? "UAH" : ActiveSavingCurrency,
                    ActiveSavingIconKey,
                    ActiveSavingColor));

            ApplySaving(updated);
            await LoadSavingsAsync(updated.Id);
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
    private async Task AddSavingItemAsync()
    {
        if (!ActiveSavingId.HasValue || IsApiBusy || string.IsNullOrWhiteSpace(SavingItemNameInput))
        {
            return;
        }

        if (!TryParsePositiveAmount(SavingItemAmount, out var itemAmount))
        {
            SavingItemAmountError = string.IsNullOrWhiteSpace(SavingItemAmount)
                ? "Сума має бути більше 0"
                : ValidatePositiveAmountInput(SavingItemAmount);
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();
            var nextPriority = SavingItems.Count == 0
                ? 1
                : SavingItems.Max(i => i.Priority ?? 0) + 1;
            await _apiClient!.CreateSavingItemAsync(
                ActiveSavingId.Value,
                new CreateSavingItemRequestDto(SavingItemNameInput.Trim(), itemAmount, (short)nextPriority, false));
            SavingItemNameInput = string.Empty;
            SavingItemAmount = string.Empty;
            await UpdateActiveSavingTargetFromItemsAsync();
            await LoadSavingsAsync(ActiveSavingId.Value);
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
    private async Task MoveSavingItemUpAsync(SavingItemViewModel? item)
    {
        await MoveSavingItemAsync(item, -1);
    }

    [RelayCommand]
    private async Task MoveSavingItemDownAsync(SavingItemViewModel? item)
    {
        await MoveSavingItemAsync(item, 1);
    }

    [RelayCommand]
    private async Task DeleteSavingItemAsync(SavingItemViewModel? item)
    {
        if (!ActiveSavingId.HasValue || item is null || IsApiBusy)
        {
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();
            await _apiClient!.DeleteSavingItemAsync(ActiveSavingId.Value, item.Id);
            await UpdateActiveSavingTargetFromItemsAsync();
            await LoadSavingsAsync(ActiveSavingId.Value);
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
    private async Task DeleteActiveSavingAsync()
    {
        if (!ActiveSavingId.HasValue || IsApiBusy)
        {
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();
            await _apiClient!.DeleteSavingAsync(ActiveSavingId.Value);
            ActiveSavingId = null;
            SavingNameInput = "Savings";
            GoalAmount = 0;
            SavedAmount = 0;
            AddAmount = string.Empty;
            InterestRate = 0;
            SavingRateHistory = [];
            SavingItems = [];
            await LoadSavingsAsync();
            SavingsStatusMessage = Texts.TransactionDeletedText;
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
    private async Task SaveSavingGroupsAsync()
    {
        if (!ActiveSavingId.HasValue || IsApiBusy || !CanEditSavingShareGroups)
        {
            return;
        }

        var groupIds = GetSelectedSavingShareGroupIds();
        if (IsSavingSharedInput && groupIds.Length == 0)
        {
            SavingsStatusMessage = Texts.TransactionFormValidationText;
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();
            await _apiClient!.UpdateSavingGroupsAsync(ActiveSavingId.Value, new ReplaceResourceGroupsRequestDto(groupIds));
            await LoadSavingsAsync(ActiveSavingId.Value);
            IsEditingSavingGroups = false;
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
    private async Task ToggleSavingItemAsync(SavingItemViewModel? item)
    {
        if (!ActiveSavingId.HasValue || item is null || IsApiBusy)
        {
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();
            var markAsPurchased = !item.IsPaid;
            var latestSaving = (await _apiClient!.GetSavingsAsync())
                .FirstOrDefault(s => s.Id == ActiveSavingId.Value);
            if (latestSaving is null)
            {
                SavingsStatusMessage = Texts.RequestErrorPrefix;
                return;
            }

            if (markAsPurchased)
            {
                if (latestSaving.CurrentAmount < item.Amount)
                {
                    SavingsStatusMessage = Texts.TransactionFormValidationText;
                    await LoadSavingsAsync(ActiveSavingId.Value);
                    return;
                }
            }

            await _apiClient!.UpdateSavingItemAsync(
                ActiveSavingId.Value,
                item.Id,
                new UpdateSavingItemRequestDto(item.Name, item.Amount, item.Priority, markAsPurchased));

            var updatedCurrentAmount = markAsPurchased
                ? Math.Max(0, latestSaving.CurrentAmount - item.Amount)
                : latestSaving.CurrentAmount + item.Amount;
            await UpdateActiveSavingTargetFromItemsAsync(updatedCurrentAmount);
            await LoadSavingsAsync(ActiveSavingId.Value);
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

    private async Task LoadSavingDetailsAsync(int savingId, decimal? currentAmountOverride = null)
    {
        SavingRateHistory = [];

        var items = await _apiClient!.GetSavingItemsAsync(savingId);
        var orderedItems = items
            .OrderBy(i => i.Priority ?? short.MaxValue)
            .ThenBy(i => i.Id)
            .ToList();

        decimal coveredAmount;
        if (currentAmountOverride.HasValue)
        {
            coveredAmount = currentAmountOverride.Value;
        }
        else
        {
            var saving = (await _apiClient!.GetSavingsAsync())
                .FirstOrDefault(s => s.Id == savingId);
            coveredAmount = saving?.CurrentAmount ?? (decimal)SavedAmount;
        }


        var remainingAmountForPriority = Math.Max(0, coveredAmount);

        SavingItems = new ObservableCollection<SavingItemViewModel>(
            orderedItems.Select((i, index) =>
            {
                var itemCost = Math.Max(0, i.Price);
                var amountForThisItem = i.IsPurchased ? itemCost : remainingAmountForPriority;
                var isFullyFunded = i.IsPurchased || itemCost <= 0 || amountForThisItem >= itemCost;

                var progress = i.IsPurchased
                    ? 100
                    : itemCost <= 0
                        ? 100
                        : Math.Clamp((double)(amountForThisItem / itemCost * 100), 0, 100);

                if (!i.IsPurchased && itemCost > 0)
                {
                    remainingAmountForPriority = Math.Max(0, remainingAmountForPriority - itemCost);
                }


                return new SavingItemViewModel(
                    i.Id, i.Name, itemCost,
                    i.Priority ?? (short)(index + 1),
                    i.IsPurchased,
                    $"{itemCost:N2} {ActiveSavingCurrency}",
                    progress,
                    $"{progress:0}%",
                    isFullyFunded);
            }));
    }

    private void RefreshSavingTransactions()
    {
        if (!ActiveSavingId.HasValue)
        {
            SavingTransactions = [];
            return;
        }

        SavingTransactions = new ObservableCollection<TransactionItemViewModel>(
            AllTransactions
                .Where(t => t.SavingId == ActiveSavingId.Value)
                .OrderByDescending(t => t.TransactionDate));
    }

    private async Task MoveSavingItemAsync(SavingItemViewModel? item, int direction)
    {
        if (!ActiveSavingId.HasValue || item is null || IsApiBusy)
        {
            return;
        }

        var currentIndex = SavingItems.IndexOf(item);
        var targetIndex = currentIndex + direction;
        if (currentIndex < 0 || targetIndex < 0 || targetIndex >= SavingItems.Count)
        {
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();

            var target = SavingItems[targetIndex];
            var currentPriority = item.Priority ?? (short)(currentIndex + 1);
            var targetPriority = target.Priority ?? (short)(targetIndex + 1);

            await _apiClient!.UpdateSavingItemAsync(
                ActiveSavingId.Value,
                item.Id,
                new UpdateSavingItemRequestDto(item.Name, item.Amount, targetPriority, item.IsPaid));
            await _apiClient.UpdateSavingItemAsync(
                ActiveSavingId.Value,
                target.Id,
                new UpdateSavingItemRequestDto(target.Name, target.Amount, currentPriority, target.IsPaid));

            await LoadSavingsAsync(ActiveSavingId.Value);
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

    private async Task UpdateActiveSavingTargetFromItemsAsync(decimal? currentAmountOverride = null)
    {
        if (!ActiveSavingId.HasValue)
        {
            return;
        }

        var items = await _apiClient!.GetSavingItemsAsync(ActiveSavingId.Value);
        var targetAmount = items
            .Where(i => !i.IsPurchased)
            .Sum(i => Math.Max(0, i.Price));

        var saving = await _apiClient.UpdateSavingAsync(
            ActiveSavingId.Value,
            new UpdateSavingRequestDto(
                string.IsNullOrWhiteSpace(SavingNameInput) ? "Savings" : SavingNameInput.Trim(),
                targetAmount > 0 ? targetAmount : null,
                currentAmountOverride ?? (decimal)SavedAmount,
                ActiveSavingDeadline,
                ActiveSavingIsCompleted,
                ActiveSavingCurrency,
                ActiveSavingIconKey,
                ActiveSavingColor));

        ApplySaving(saving);
    }

    private void RefreshSavingsProperties()
    {
        OnPropertyChanged(nameof(RemainingAmount));
        OnPropertyChanged(nameof(ProgressPercent));
        OnPropertyChanged(nameof(SavedAmountDisplay));
        OnPropertyChanged(nameof(RemainingAmountDisplay));
        OnPropertyChanged(nameof(ActiveSavingAmountGoalDisplay));
        OnPropertyChanged(nameof(ActiveSavingDeadlineDisplay));
    }

    private static string BuildSavingDeadlineDisplay(DateOnly? deadline)
    {
        if (!deadline.HasValue)
        {
            return string.Empty;
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var daysLeft = deadline.Value.DayNumber - today.DayNumber;
        if (daysLeft <= 0)
        {
            return "залишилося 0 днів";
        }

        if (daysLeft < 30)
        {
            return $"залишилося {daysLeft} днів";
        }

        var monthsLeft = daysLeft / 30;
        return $"залишилося {monthsLeft} місяців ({daysLeft} днів)";
    }
}
