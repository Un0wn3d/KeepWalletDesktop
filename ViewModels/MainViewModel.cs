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

public partial class MainViewModel : ViewModelBase
{
    private const int AllTransactionsPageSize = 30;
    private const string DateDisplayFormat = "dd.MM.yyyy";
    private const string DateTimeDisplayFormat = "dd.MM.yyyy HH:mm";
    private const string AuthPage = "Auth";
    private const string HomePage = "Home";
    private const string BankAccountsPage = "BankAccounts";
    private const string SavingsPage = "Savings";
    private const string GroupsPage = "Groups";
    private const string ScheduledPaymentsPage = "ScheduledPayments";
    private const string CategoriesPage = "Categories";
    private const string AllTransactionsPage = "AllTransactions";
    private const string ReportingPage = "Reporting";
    private const string SettingsPage = "Settings";
    private const string LogsPage = "Logs";
    private const string NewCategoryOptionCode = "__new_category";
    private KeepWalletApiClient? _apiClient;
    private IReadOnlyList<ApiCategoryDto> apiCategories = [];
    private IReadOnlySet<int>? selectedCategoryIds;
    private IReadOnlyList<TransactionItemViewModel> filteredTransactionSource = [];
    private readonly Dictionary<string, decimal> categoryLimits = new(StringComparer.OrdinalIgnoreCase);
    private bool isRefreshingReportingSelections;
    private int filteredTransactionTakeCount = AllTransactionsPageSize;
    private static readonly ILogger ViewModelLog = Log.ForContext<MainViewModel>();
    private DispatcherTimer? transactionFilterDebounceTimer;
    private bool isLoadingTransactions;
    private DateTimeOffset lastTransactionsLoadedAt = DateTimeOffset.MinValue;
    private DateTimeOffset allTransactionsDateRangeFrom = DateTimeOffset.Now.AddMonths(-1);
    private DateTimeOffset allTransactionsDateRangeTo = DateTimeOffset.Now;
    private CancellationTokenSource? transactionsLoadCts;

    public Func<string, string, string, Task<string?>>? SaveReportFilePickerAsync { get; set; }


    public MainViewModel()
    {
        var apiOptions = AppConfiguration.GetApiOptions();
        var userSettings = UserSettingsStore.Load();
        ApiBaseUrl = string.IsNullOrWhiteSpace(userSettings.ApiBaseUrl)
            ? apiOptions.BaseUrl
            : userSettings.ApiBaseUrl;
        ApiLogin = userSettings.LastLogin;
        InitializeLanguages(userSettings.LanguageCode);
        InitializeThemes(userSettings.ThemeName);
        InitializeScheduledRepeatOptions();
        InitializeScheduledCustomRepeatOptions();
        InitializeReportingOptions();
        SelectedGroupAccessResourceType = GroupAccessResourceTypeOptions.FirstOrDefault();
        LoadServerSettingsFromBaseUrl();
        ResetAuthorizedUser();
        LoadApiOnlyPlaceholders();
        _ = LoadAppSettingsAsync();
    }

    public void StartBackgroundInitialization()
    {
        _ = EnsureEmojiCatalogIfMissingAsync();
    }

    private async Task EnsureEmojiCatalogIfMissingAsync()
    {
        if (CategoryIconCatalog.HasCachedCatalog())
        {
            return;
        }

        await Task.Delay(1500);
        await CategoryIconCatalog.PreloadAsync();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            OnPropertyChanged(nameof(CategoryIconOptions));
            OnPropertyChanged(nameof(CategoryNewIconGlyph));
            OnPropertyChanged(nameof(CategoryNewIconSampleImage));
            OnPropertyChanged(nameof(HasCategoryNewIconSampleImage));
            OnPropertyChanged(nameof(NewGroupIconGlyph));
            OnPropertyChanged(nameof(NewGroupIconSampleImage));
            OnPropertyChanged(nameof(HasNewGroupIconSampleImage));
            RefreshTransactionItems();
        });
    }

}
