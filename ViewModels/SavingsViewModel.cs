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
                PiggyBankStatusMessage = string.Empty;
                RefreshSavingsProperties();
                return;
            }

            Savings = new ObservableCollection<SavingOptionViewModel>(
                savings.Select(s => new SavingOptionViewModel(
                    s.Id,
                    s.Name,
                    $"{s.CurrentAmount:N2} UAH",
                    s.Id == activeSaving.Id)));
            ApplySaving(activeSaving);
            await LoadSavingDetailsAsync(activeSaving.Id);
        }
        catch (HttpRequestException ex)
        {
            PiggyBankStatusMessage = GetApiErrorMessage(ex);
        }
        catch (Exception ex)
        {
            PiggyBankStatusMessage = $"{Texts.RequestErrorPrefix}: {ex.Message}";
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
            PiggyBankStatusMessage = GetApiErrorMessage(ex);
        }
        catch (Exception ex)
        {
            PiggyBankStatusMessage = $"{Texts.RequestErrorPrefix}: {ex.Message}";
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
            PiggyBankStatusMessage = Texts.TransactionDeletedText;
        }
        catch (HttpRequestException ex)
        {
            PiggyBankStatusMessage = GetApiErrorMessage(ex);
        }
        catch (Exception ex)
        {
            PiggyBankStatusMessage = $"{Texts.RequestErrorPrefix}: {ex.Message}";
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
            await _apiClient!.UpdateSavingItemAsync(
                ActiveSavingId.Value,
                item.Id,
                new UpdateSavingItemRequestDto(item.Name, item.Amount, item.Priority, !item.IsPaid));
            await UpdateActiveSavingTargetFromItemsAsync();
            await LoadSavingDetailsAsync(ActiveSavingId.Value);
        }
        catch (HttpRequestException ex)
        {
            PiggyBankStatusMessage = GetApiErrorMessage(ex);
        }
        catch (Exception ex)
        {
            PiggyBankStatusMessage = $"{Texts.RequestErrorPrefix}: {ex.Message}";
        }
        finally
        {
            IsApiBusy = false;
        }
    }

    private async Task LoadSavingDetailsAsync(int savingId)
    {
        SavingRateHistory = [];

        var items = await _apiClient!.GetSavingItemsAsync(savingId);
        var orderedItems = items
            .OrderBy(i => i.Priority ?? short.MaxValue)
            .ThenBy(i => i.Id)
            .ToList();
        var coveredAmount = (decimal)SavedAmount;
        var cumulativeCost = 0m;

        SavingItems = new ObservableCollection<SavingItemViewModel>(
            orderedItems.Select((i, index) =>
            {
                var itemCost = Math.Max(0, i.Price);
                var amountAvailableForItem = Math.Max(0, coveredAmount - cumulativeCost);
                var progress = itemCost <= 0
                    ? 100
                    : Math.Clamp((double)(amountAvailableForItem / itemCost * 100), 0, 100);
                cumulativeCost += itemCost;

                return new SavingItemViewModel(
                    i.Id,
                    i.Name,
                    itemCost,
                    i.Priority ?? (short)(index + 1),
                    i.IsPurchased,
                    $"{itemCost:N2} UAH",
                    progress,
                    $"{progress:0}%");
            }));
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
            PiggyBankStatusMessage = GetApiErrorMessage(ex);
        }
        catch (Exception ex)
        {
            PiggyBankStatusMessage = $"{Texts.RequestErrorPrefix}: {ex.Message}";
        }
        finally
        {
            IsApiBusy = false;
        }
    }

    private async Task UpdateActiveSavingTargetFromItemsAsync()
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
                (decimal)SavedAmount,
                null,
                targetAmount > 0 && (decimal)SavedAmount >= targetAmount));

        ApplySaving(saving);
    }

    private void RefreshSavingsProperties()
    {
        OnPropertyChanged(nameof(RemainingAmount));
        OnPropertyChanged(nameof(ProgressPercent));
        OnPropertyChanged(nameof(SavedAmountDisplay));
        OnPropertyChanged(nameof(RemainingAmountDisplay));
    }
}
