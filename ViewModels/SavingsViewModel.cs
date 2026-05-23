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
    private async Task LoadSavingsAsync()
    {
        if (!IsApiAuthorized)
        {
            return;
        }

        try
        {
            EnsureApiClient();
            var savings = await _apiClient!.GetSavingsAsync();
            var activeSaving = savings.FirstOrDefault(s => !s.IsCompleted) ?? savings.FirstOrDefault();

            if (activeSaving is null)
            {
                ActiveSavingId = null;
                SavingNameInput = "Savings";
                GoalAmount = 0;
                SavedAmount = 0;
                AddAmount = 0;
                InterestRate = 0;
                Savings = [];
                SavingRateHistory = [];
                SavingItems = [];
                SavingsStatusMessage = string.Empty;
                RefreshSavingsProperties();
                return;
            }

            Savings = new ObservableCollection<SavingOptionViewModel>(
                savings.Select(s =>
                {
                    var targetAmount = s.TargetAmount ?? 0;
                    var progress = targetAmount <= 0
                        ? 0
                        : Math.Clamp((double)(s.CurrentAmount / targetAmount * 100), 0, 100);
                    return new SavingOptionViewModel(
                        s.Id,
                        s.GroupId,
                        s.Name,
                        $"{s.CurrentAmount:N2} UAH",
                        $"{targetAmount:N2} UAH",
                        progress,
                        $"{progress:0}%",
                        s.Id == activeSaving.Id);
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
    private async Task AddSavingItemAsync()
    {
        if (!ActiveSavingId.HasValue || IsApiBusy || string.IsNullOrWhiteSpace(SavingItemNameInput))
        {
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
                new CreateSavingItemRequestDto(SavingItemNameInput.Trim(), (decimal)Math.Max(0, SavingItemAmount), (short)nextPriority, false));
            SavingItemNameInput = string.Empty;
            SavingItemAmount = 0;
            await UpdateActiveSavingTargetFromItemsAsync();
            await LoadSavingDetailsAsync(ActiveSavingId.Value);
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
            await LoadSavingDetailsAsync(ActiveSavingId.Value);
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
            AddAmount = 0;
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
                    await LoadSavingDetailsAsync(ActiveSavingId.Value, latestSaving.CurrentAmount);
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
            await LoadSavingDetailsAsync(ActiveSavingId.Value, updatedCurrentAmount);
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

        // Куплені вже списані з балансу — додаємо їх назад,
        // щоб порахувати "повний" баланс до будь-яких покупок
        var purchasedTotal = orderedItems
            .Where(i => i.IsPurchased)
            .Sum(i => Math.Max(0, i.Price));
    
        var totalAvailable = coveredAmount + purchasedTotal;
        var cumulativeCost = 0m;

        SavingItems = new ObservableCollection<SavingItemViewModel>(
            orderedItems.Select((i, index) =>
            {
                var itemCost = Math.Max(0, i.Price);
                var amountAvailableForItem = Math.Max(0, totalAvailable - cumulativeCost);
                var isFullyFunded = i.IsPurchased || itemCost <= 0 || amountAvailableForItem >= itemCost;
            
                var progress = i.IsPurchased
                    ? 100
                    : itemCost <= 0
                        ? 100
                        : Math.Clamp((double)(amountAvailableForItem / itemCost * 100), 0, 100);

                // Всі айтеми (і куплені, і ні) займають місце в черзі
                cumulativeCost += itemCost;

                return new SavingItemViewModel(
                    i.Id, i.Name, itemCost,
                    i.Priority ?? (short)(index + 1),
                    i.IsPurchased,
                    $"{itemCost:N2} UAH",
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

        var marker = $"saving:{ActiveSavingId.Value}";
        var depositPrefix = $"Накопичення: {SavingNameInput}";
        var withdrawalPrefix = $"Зняття з накопичення: {SavingNameInput}";

        SavingTransactions = new ObservableCollection<TransactionItemViewModel>(
            AllTransactions
                .Where(t =>
                    (t.Description?.Contains(marker, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    string.Equals(t.Description, depositPrefix, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(t.Description, withdrawalPrefix, StringComparison.OrdinalIgnoreCase))
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

            await LoadSavingDetailsAsync(ActiveSavingId.Value);
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
                null,
                targetAmount > 0 && (currentAmountOverride ?? (decimal)SavedAmount) >= targetAmount));

        ApplySaving(saving);
    }

    private void RefreshSavingsProperties()
    {
        OnPropertyChanged(nameof(RemainingAmount));
        OnPropertyChanged(nameof(ProgressPercent));
        OnPropertyChanged(nameof(SavedAmountDisplay));
        OnPropertyChanged(nameof(RemainingAmountDisplay));
        OnPropertyChanged(nameof(ActiveSavingAmountGoalDisplay));
    }
}
