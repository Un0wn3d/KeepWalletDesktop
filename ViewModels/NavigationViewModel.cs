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
    private async Task OpenSavingsAsync()
    {
        if (!EnsureAuthorizedNavigation())
        {
            return;
        }

        CurrentPage = SavingsPage;
        await LoadBankAccountsAsync();
        await LoadSavingsAsync();
    }

    [RelayCommand]
    private async Task OpenBankAccountsAsync()
    {
        if (!EnsureAuthorizedNavigation())
        {
            return;
        }

        CurrentPage = BankAccountsPage;
        await LoadBankAccountsAsync();
    }

    [RelayCommand]
    private async Task OpenGroupsAsync()
    {
        if (!EnsureAuthorizedNavigation())
        {
            return;
        }

        CurrentPage = GroupsPage;
        await LoadGroupsAsync();
        await LoadBankAccountsAsync();
        await LoadScheduledPaymentsAsync();
        await LoadSavingsAsync();
    }

    [RelayCommand]
    private async Task OpenScheduledPaymentsAsync()
    {
        if (!EnsureAuthorizedNavigation())
        {
            return;
        }

        CurrentPage = ScheduledPaymentsPage;
        await LoadScheduledPaymentsAsync();
    }

    [RelayCommand]
    private async Task OpenCategoriesAsync()
    {
        if (!EnsureAuthorizedNavigation())
        {
            return;
        }

        CurrentPage = CategoriesPage;
        IsCategoryDetailsOpen = false;
        IsCategorySettingsMode = true;
        await LoadCategoryManagementAsync();
        await LoadBudgetsAsync();
        UpdateCategoryDetails();
    }

    [RelayCommand]
    private void OpenAllTransactionsTab()
    {
        if (!EnsureAuthorizedNavigation())
        {
            return;
        }

        CurrentPage = AllTransactionsPage;
        RefreshTransactionTypeFilterOptions();
        RefreshTransactionFilterOptions();
        ApplyTransactionFilters();
    }

    [RelayCommand]
    private async Task OpenSettingsAsync()
    {
        if (!EnsureAuthorizedNavigation())
        {
            return;
        }

        CurrentPage = SettingsPage;
        await LoadSettingsDataAsync();
    }

    [RelayCommand]
    private async Task OpenReportingAsync()
    {
        if (!EnsureAuthorizedNavigation())
        {
            return;
        }

        CurrentPage = ReportingPage;
        await LoadReportingDataAsync();
    }

    private async Task LoadReportingDataAsync()
    {
        await LoadBankAccountsAsync();
        await LoadTransactionsAsync();
        await LoadSavingsAsync();
        await LoadBudgetsAsync();
        RefreshReportingSelections();
        OnPropertyChanged(nameof(ReportIncomeTotal));
        OnPropertyChanged(nameof(ReportExpenseTotal));
        OnPropertyChanged(nameof(ReportBalanceTotal));
    }

}
