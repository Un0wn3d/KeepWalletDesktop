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
{
    [RelayCommand]
    private async Task AddOnboardingCategoryAsync()
    {
        if (IsApiBusy)
        {
            return;
        }

        var name = OnboardingNewCategoryName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            OnboardingStatusMessage = Texts.OnboardingCategoryNameRequired;
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();

            var category = await _apiClient!.CreateCategoryAsync(new CreateCategoryRequestDto(
                name,
                string.Equals(OnboardingNewCategoryType, "income", StringComparison.OrdinalIgnoreCase)
                    ? "income"
                    : "expense"));

            if (OnboardingCategories.All(c => c.Id != category.Id))
            {
                OnboardingCategories.Add(new OnboardingCategoryOptionViewModel(
                    category.Id,
                    category.Name,
                    category.Type,
                    GetOnboardingCategoryTypeDisplay(category.Type),
                    true));
            }
            else
            {
                var existing = OnboardingCategories.First(c => c.Id == category.Id);
                existing.IsSelected = true;
            }

            OnboardingNewCategoryName = string.Empty;
            OnboardingStatusMessage = string.Empty;
            RefreshOnboardingCategoryColumns();
            await LoadCategoriesAsync();
        }
        catch (HttpRequestException ex)
        {
            OnboardingStatusMessage = GetApiErrorMessage(ex);
        }
        catch (Exception ex)
        {
            OnboardingStatusMessage = $"{Texts.RequestErrorPrefix}: {ex.Message}";
        }
        finally
        {
            IsApiBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddCategoryAsync()
    {
        if (IsApiBusy)
        {
            return;
        }

        var name = CategoryNewName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            CategoryStatusMessage = Texts.OnboardingCategoryNameRequired;
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();
            var iconKey = CategoryNewIconKey;
            var color = CategoryNewColor;

            var category = await _apiClient!.CreateCategoryAsync(new CreateCategoryRequestDto(
                name,
                string.Equals(CategoryNewType, "income", StringComparison.OrdinalIgnoreCase) ? "income" : "expense",
                iconKey,
                color));

            var selectedIds = Categories
                .Where(c => c.IsSelected)
                .Select(c => c.Id)
                .Append(category.Id)
                .Distinct()
                .ToArray();
            var preferences = Categories
                .Select(c => new UserCategoryPreferenceUpdateDto(c.Id, c.SelectedIconKey, c.SelectedColor, c.IsSelected))
                .Append(new UserCategoryPreferenceUpdateDto(category.Id, iconKey, color, true))
                .ToArray();
            await _apiClient.UpdateUserCategoriesAsync(new UpdateUserCategoryPreferencesRequestDto(selectedIds, preferences));
            selectedCategoryIds = selectedIds.ToHashSet();

            CategoryNewName = string.Empty;
            CategoryNewColor = "#FFFFFF";
            CategoryStatusMessage = string.Empty;
            await LoadCategoryManagementAsync();
            await LoadCategoriesAsync();
            if (IsAddCategoryModuleOpen)
            {
                IsAddCategoryModuleOpen = false;
                UpdateTransactionCategoryOptions(category.Id.ToString(CultureInfo.InvariantCulture));
            }
        }
        catch (HttpRequestException ex)
        {
            CategoryStatusMessage = GetApiErrorMessage(ex);
        }
        catch (Exception ex)
        {
            CategoryStatusMessage = $"{Texts.RequestErrorPrefix}: {ex.Message}";
        }
        finally
        {
            IsApiBusy = false;
        }
    }

    [RelayCommand]
    private void CloseAddCategoryModule()
    {
        IsAddCategoryModuleOpen = false;
        CategoryStatusMessage = string.Empty;
        SelectedTransactionCategory = TransactionCategoryOptions.FirstOrDefault(x => x.Code != NewCategoryOptionCode);
    }

    [RelayCommand]
    private void SelectCategoryType(string type)
    {
        CategoryNewType = string.Equals(type, "income", StringComparison.OrdinalIgnoreCase)
            ? "income"
            : "expense";
    }

    [RelayCommand]
    private async Task SaveCategoryIconAsync(CategoryManagementItemViewModel? category)
    {
        if (IsApiBusy || category is null)
        {
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();
            await _apiClient!.UpdateCategoryAsync(category.Id, new UpdateCategoryRequestDto(
                IconKey: category.SelectedIconKey,
                Color: category.SelectedColor));
            CategoryStatusMessage = string.Empty;
            await LoadCategoryManagementAsync();
            await LoadCategoriesAsync();
            await LoadUserCategoryPreferencesAsync();
            await LoadTransactionsAsync();
        }
        catch (HttpRequestException ex)
        {
            CategoryStatusMessage = GetApiErrorMessage(ex);
        }
        catch (Exception ex)
        {
            CategoryStatusMessage = $"{Texts.RequestErrorPrefix}: {ex.Message}";
        }
        finally
        {
            IsApiBusy = false;
        }
    }

    [RelayCommand]
    private void EditCategoryName(CategoryManagementItemViewModel? category)
    {
        if (category is null)
        {
            return;
        }

        category.NameInput = category.Name;
        category.IsEditingName = true;
    }

    [RelayCommand]
    private async Task FinishCategoryNameEditAsync(CategoryManagementItemViewModel? category)
    {
        if (category is null || !category.IsEditingName)
        {
            return;
        }

        var name = category.NameInput.Trim();
        if (string.IsNullOrWhiteSpace(name) || string.Equals(name, category.Name, StringComparison.Ordinal))
        {
            category.NameInput = category.Name;
            category.IsEditingName = false;
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();
            var updated = await _apiClient!.UpdateCategoryAsync(category.Id, new UpdateCategoryRequestDto(Name: name));
            category.Name = updated.Name;
            category.NameInput = updated.Name;
            category.IsEditingName = false;
            await LoadCategoriesAsync();
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
    private async Task SaveCategoriesAsync()
    {
        if (IsApiBusy)
        {
            return;
        }

        var selectedIds = Categories
            .Where(c => c.IsSelected)
            .Select(c => c.Id)
            .Distinct()
            .ToArray();

        if (selectedIds.Length == 0)
        {
            CategoryStatusMessage = Texts.OnboardingCategorySelectionRequired;
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();
            await _apiClient!.UpdateUserCategoriesAsync(CreateCategoryPreferencesRequest(selectedIds));
            selectedCategoryIds = selectedIds.ToHashSet();
            CategoryStatusMessage = string.Empty;
            await LoadCategoriesAsync();
            await LoadUserCategoryPreferencesAsync();
            await LoadTransactionsAsync();
        }
        catch (HttpRequestException ex)
        {
            CategoryStatusMessage = GetApiErrorMessage(ex);
        }
        catch (Exception ex)
        {
            CategoryStatusMessage = $"{Texts.RequestErrorPrefix}: {ex.Message}";
        }
        finally
        {
            IsApiBusy = false;
        }
    }

    private UpdateUserCategoryPreferencesRequestDto CreateCategoryPreferencesRequest(IReadOnlyList<int> selectedIds)
    {
        var preferences = Categories
            .Select(category => new UserCategoryPreferenceUpdateDto(
                category.Id,
                category.SelectedIconKey,
                category.SelectedColor,
                category.IsSelected))
            .ToArray();

        return new UpdateUserCategoryPreferencesRequestDto(selectedIds, preferences);
    }

    [RelayCommand]
    private void DeleteCategory(CategoryManagementItemViewModel? category)
    {
        if (IsApiBusy || category is null)
        {
            return;
        }

        pendingDeleteCategory = category;
        DeleteCategoryPromptMessage = string.Format(CultureInfo.CurrentCulture, Texts.DeleteCategoryPromptMessage, category.Name);
        IsDeleteCategoryPromptOpen = true;
    }

    [RelayCommand]
    private void CancelDeleteCategory()
    {
        pendingDeleteCategory = null;
        DeleteCategoryPromptMessage = string.Empty;
        IsDeleteCategoryPromptOpen = false;
    }

    [RelayCommand]
    private void CloseCategoryErrorDialog()
    {
        CategoryErrorDialogMessage = string.Empty;
        IsCategoryErrorDialogOpen = false;
    }

    [RelayCommand]
    private async Task ConfirmDeleteCategoryAsync()
    {
        if (IsApiBusy || pendingDeleteCategory is null)
        {
            return;
        }

        var category = pendingDeleteCategory;
        try
        {
            IsApiBusy = true;
            IsDeleteCategoryPromptOpen = false;
            EnsureApiClient();
            await _apiClient!.DeleteCategoryAsync(category.Id);
            pendingDeleteCategory = null;
            DeleteCategoryPromptMessage = string.Empty;
            CategoryStatusMessage = string.Empty;
            await LoadCategoryManagementAsync();
            await LoadCategoriesAsync();
            await LoadTransactionsAsync();
        }
        catch (HttpRequestException ex)
        {
            if (ex.StatusCode == HttpStatusCode.Conflict)
            {
                pendingDeleteCategory = null;
                DeleteCategoryPromptMessage = string.Empty;
                CategoryStatusMessage = string.Empty;
                CategoryErrorDialogMessage = Texts.DeleteCategoryInUseError;
                IsCategoryErrorDialogOpen = true;
            }
            else
            {
                CategoryStatusMessage = GetApiErrorMessage(ex);
            }
        }
        catch (Exception ex)
        {
            CategoryStatusMessage = $"{Texts.RequestErrorPrefix}: {ex.Message}";
        }
        finally
        {
            IsApiBusy = false;
        }
    }

    [RelayCommand]
    private async Task MergeCategoriesAsync()
    {
        if (IsApiBusy || SelectedMergeSourceCategory is null || SelectedMergeTargetCategory is null)
        {
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();
            await _apiClient!.MergeCategoryAsync(
                SelectedMergeSourceCategory.Id,
                new MergeCategoryRequestDto(SelectedMergeTargetCategory.Id));

            SelectedMergeSourceCategory = null;
            SelectedMergeTargetCategory = null;
            CategoryStatusMessage = string.Empty;
            await LoadCategoryManagementAsync();
            await LoadCategoriesAsync();
            await LoadBudgetsAsync();
            await LoadTransactionsAsync();
        }
        catch (HttpRequestException ex)
        {
            CategoryStatusMessage = GetApiErrorMessage(ex);
        }
        catch (Exception ex)
        {
            CategoryStatusMessage = $"{Texts.RequestErrorPrefix}: {ex.Message}";
        }
        finally
        {
            IsApiBusy = false;
        }
    }

    [RelayCommand]
    private void SelectOnboardingCategoryType(string type)
    {
        OnboardingNewCategoryType = string.Equals(type, "income", StringComparison.OrdinalIgnoreCase)
            ? "income"
            : "expense";
    }

    [RelayCommand]
    private async Task FinishOnboardingAsync()
    {
        if (IsApiBusy)
        {
            return;
        }

        var selectedIds = OnboardingCategories
            .Where(c => c.IsSelected)
            .Select(c => c.Id)
            .Distinct()
            .ToArray();

        if (!IsCategorySettingsMode && string.IsNullOrWhiteSpace(OnboardingAccountName))
        {
            OnboardingStatusMessage = Texts.OnboardingAccountNameRequired;
            return;
        }

        if (selectedIds.Length == 0)
        {
            OnboardingStatusMessage = Texts.OnboardingCategorySelectionRequired;
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();

            if (IsCategorySettingsMode)
            {
                await _apiClient!.UpdateUserCategoriesAsync(new UpdateUserCategoryPreferencesRequestDto(selectedIds));
                selectedCategoryIds = selectedIds.ToHashSet();
                IsOnboardingDialogOpen = false;
                IsCategorySettingsMode = false;
                OnboardingStatusMessage = string.Empty;

                await LoadCategoriesAsync();
                await LoadTransactionsAsync();
                return;
            }

            var account = await _apiClient!.CreateBankAccountAsync(new CreateBankAccountRequestDto(
                OnboardingAccountName.Trim(),
                OnboardingSelectedCurrency.Trim().ToUpperInvariant(),
                0,
                true));

            await _apiClient.UpdateUserCategoriesAsync(new UpdateUserCategoryPreferencesRequestDto(selectedIds));

            defaultAccountId = account.Id;
            TransactionCurrencyInput = account.Currency;
            selectedCategoryIds = selectedIds.ToHashSet();
            IsOnboardingDialogOpen = false;
            OnboardingStatusMessage = string.Empty;

            await LoadCategoriesAsync();
            await LoadBankAccountsAsync();
            await LoadTransactionsAsync();
        }
        catch (HttpRequestException ex)
        {
            OnboardingStatusMessage = GetApiErrorMessage(ex);
        }
        catch (Exception ex)
        {
            OnboardingStatusMessage = $"{Texts.RequestErrorPrefix}: {ex.Message}";
        }
        finally
        {
            IsApiBusy = false;
        }
    }

}
