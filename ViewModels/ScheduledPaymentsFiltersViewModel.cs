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
{    partial void OnScheduledPaymentsChanged(ObservableCollection<ScheduledPaymentItemViewModel> value)
    {
        OnPropertyChanged(nameof(SelectedGroupScheduledPayments));
        RefreshGroupAccessProperties();

        var hasIncome = value.Any(p => string.Equals(p.Type, "income", StringComparison.OrdinalIgnoreCase));
        var hasExpense = value.Any(p => string.Equals(p.Type, "expense", StringComparison.OrdinalIgnoreCase));

        var scheduledPaymentTypeOptions = new List<CategoryOptionViewModel>
        {
            new("all", Texts.ScheduledPaymentsFilterAll)
        };

        if (hasExpense)
        {
            scheduledPaymentTypeOptions.Add(new CategoryOptionViewModel("expense", Texts.TransactionExpenseType));
        }

        if (hasIncome)
        {
            scheduledPaymentTypeOptions.Add(new CategoryOptionViewModel("income", Texts.TransactionIncomeType));
        }

        ScheduledPaymentTypeFilterOptions = new ObservableCollection<CategoryOptionViewModel>(scheduledPaymentTypeOptions);
        SelectedScheduledPaymentTypeFilter = ScheduledPaymentTypeFilterOptions
            .FirstOrDefault(x => x.Code == (SelectedScheduledPaymentTypeFilter?.Code ?? "all"))
            ?? ScheduledPaymentTypeFilterOptions.FirstOrDefault();

        ScheduledPaymentCurrencyFilterOptions = new ObservableCollection<CategoryOptionViewModel>(
        [
            new CategoryOptionViewModel("all", Texts.ScheduledPaymentsCurrencyFilterPlaceholder),
            .. value.Select(p => p.Currency)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c)
                .Select(c => new CategoryOptionViewModel(c, c))
        ]);
        SelectedScheduledPaymentCurrencyFilter = ScheduledPaymentCurrencyFilterOptions
            .FirstOrDefault(x => x.Code == (SelectedScheduledPaymentCurrencyFilter?.Code ?? "all"))
            ?? ScheduledPaymentCurrencyFilterOptions.FirstOrDefault();

        ScheduledPaymentCategoryFilterOptions = new ObservableCollection<CategoryOptionViewModel>(
        [
            new CategoryOptionViewModel("all", Texts.ScheduledPaymentsCategoryFilterPlaceholder),
            .. value.GroupBy(p => p.CategoryId)
                .OrderBy(g => g.First().CategoryDisplay)
                .Select(g => new CategoryOptionViewModel(g.Key.ToString(CultureInfo.InvariantCulture), g.First().CategoryDisplay))
        ]);
        SelectedScheduledPaymentCategoryFilter = ScheduledPaymentCategoryFilterOptions
            .FirstOrDefault(x => x.Code == (SelectedScheduledPaymentCategoryFilter?.Code ?? "all"))
            ?? ScheduledPaymentCategoryFilterOptions.FirstOrDefault();

        OnPropertyChanged(nameof(ScheduledPaymentsTotalCount));
        OnPropertyChanged(nameof(ScheduledPaymentsTotalAmountDisplay));
        OnPropertyChanged(nameof(ScheduledPaymentsPaidThisMonthCount));
        OnPropertyChanged(nameof(ScheduledPaymentsPaidThisMonthAmountDisplay));
        OnPropertyChanged(nameof(ScheduledPaymentsUnpaidCount));
        OnPropertyChanged(nameof(ScheduledPaymentsUnpaidAmountDisplay));
        OnPropertyChanged(nameof(ScheduledPaymentsOverdueCount));
        OnPropertyChanged(nameof(ScheduledPaymentsOverdueAmountDisplay));
        OnPropertyChanged(nameof(ScheduledPaymentsUpcomingCount));
        OnPropertyChanged(nameof(ScheduledPaymentCategoryPillWidth));
        OnPropertyChanged(nameof(ScheduledPaymentGroupPillWidth));
        OnPropertyChanged(nameof(HasScheduledPaymentTypeFilter));
        RefreshScheduledPaymentAdvancedFilterOptions(value);
        OnPropertyChanged(nameof(HasScheduledPaymentAdvancedFilter));
        OnPropertyChanged(nameof(FilteredScheduledPayments));
    }

    partial void OnScheduledPaymentsFilterChanged(string value)
    {
        OnPropertyChanged(nameof(FilteredScheduledPayments));
        OnPropertyChanged(nameof(IsAllScheduledPaymentsFilterSelected));
        OnPropertyChanged(nameof(IsFutureScheduledPaymentsFilterSelected));
        OnPropertyChanged(nameof(IsOverdueScheduledPaymentsFilterSelected));
    }

    partial void OnSelectedScheduledPaymentCurrencyFilterChanged(CategoryOptionViewModel? value)
    {
        OnPropertyChanged(nameof(FilteredScheduledPayments));
    }

    partial void OnSelectedScheduledPaymentTypeFilterChanged(CategoryOptionViewModel? value)
    {
        OnPropertyChanged(nameof(FilteredScheduledPayments));
    }

    partial void OnSelectedScheduledPaymentCategoryFilterChanged(CategoryOptionViewModel? value)
    {
        OnPropertyChanged(nameof(FilteredScheduledPayments));
    }

    private void RefreshScheduledPaymentAdvancedFilterOptions(IEnumerable<ScheduledPaymentItemViewModel> payments)
    {
        var selectedGroups = ScheduledPaymentGroupFilterOptions.Count == 0
            ? null
            : ScheduledPaymentGroupFilterOptions
                .Where(option => option.IsSelected)
                .Select(option => option.Code)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var selectedUsers = ScheduledPaymentUserFilterOptions.Count == 0
            ? null
            : ScheduledPaymentUserFilterOptions
                .Where(option => option.IsSelected)
                .Select(option => option.Code)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var groupOptions = payments
            .SelectMany(payment => payment.GroupIds)
            .Distinct()
            .Select(id =>
            {
                var code = id.ToString();
                var name = Groups.FirstOrDefault(group => group.Id == id)?.Name
                    ?? ScheduledPayments.FirstOrDefault(payment => payment.GroupIds.Contains(id))?.GroupName
                    ?? "Group";
                return new FilterSelectionOptionViewModel(code, name, selectedGroups is not null && selectedGroups.Contains(code));
            })
            .OrderBy(option => option.DisplayName)
            .ToArray();

        var userOptions = payments
            .Select(payment =>
            {
                var code = GetScheduledPaymentOwnerFilterCode(payment);
                var displayName = GetScheduledPaymentOwnerFilterDisplay(payment);
                return new { code, displayName };
            })
            .Where(option => !string.IsNullOrWhiteSpace(option.code))
            .GroupBy(option => option.code, StringComparer.OrdinalIgnoreCase)
            .Select(group => new FilterSelectionOptionViewModel(
                group.Key,
                group.First().displayName,
                selectedUsers is not null && selectedUsers.Contains(group.Key)))
            .OrderBy(option => option.DisplayName)
            .ToArray();

        ScheduledPaymentGroupFilterOptions = new ObservableCollection<FilterSelectionOptionViewModel>(groupOptions);
        ScheduledPaymentUserFilterOptions = new ObservableCollection<FilterSelectionOptionViewModel>(userOptions);
    }

    private string GetScheduledPaymentOwnerFilterCode(ScheduledPaymentItemViewModel payment)
    {
        if (!string.IsNullOrWhiteSpace(payment.OwnerDisplay) && payment.OwnerDisplay != "-")
        {
            return payment.OwnerDisplay.Trim();
        }

        return string.IsNullOrWhiteSpace(CurrentUsername) || CurrentUsername == Texts.GuestLabel
            ? Texts.UserLabel
            : CurrentUsername.Trim();
    }

    private string GetScheduledPaymentOwnerFilterDisplay(ScheduledPaymentItemViewModel payment)
    {
        var owner = GetScheduledPaymentOwnerFilterCode(payment);
        if (!string.IsNullOrWhiteSpace(CurrentUsername) &&
            string.Equals(owner, CurrentUsername.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return "Я";
        }

        return owner;
    }

    private bool IsPaidScheduledPaymentThisMonth(TransactionItemViewModel transaction)
    {
        var localTransactionDate = transaction.TransactionDate.ToLocalTime();
        var now = DateTimeOffset.Now;
        if (localTransactionDate.Year != now.Year || localTransactionDate.Month != now.Month)
        {
            return false;
        }

        return ScheduledPayments.Any(payment =>
            payment.RecurringPaymentId > 0 &&
            payment.AccountId == transaction.AccountId &&
            payment.Amount == transaction.Amount &&
            (string.Equals(payment.Name, transaction.Description, StringComparison.OrdinalIgnoreCase) ||
             (!string.IsNullOrWhiteSpace(payment.Description) &&
              string.Equals(payment.Description, transaction.Description, StringComparison.OrdinalIgnoreCase))));
    }

    private static bool IsScheduledPaymentThisMonth(ScheduledPaymentItemViewModel payment)
    {
        var localDueDate = payment.DueDate.ToLocalTime();
        var now = DateTimeOffset.Now;
        return localDueDate.Year == now.Year && localDueDate.Month == now.Month;
    }

    private static bool IsOverdueScheduledPayment(ScheduledPaymentItemViewModel payment) =>
        payment.IsActive && payment.DueDate.ToLocalTime().Date < DateTime.Today;
}
