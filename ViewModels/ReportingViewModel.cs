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
    private async Task ExportReportExcelAsync()
    {
        if (IsApiBusy)
        {
            return;
        }

        try
        {
            IsApiBusy = true;
            await LoadReportingDataAsync();
            var filePath = await SelectReportSavePathAsync("Save Excel report", "xlsx");
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add("Transactions");
            var headers = new[] { "Type", "Date", "Description", "Category", "Amount", "Currency", "AccountName" };

            for (var i = 0; i < headers.Length; i++)
            {
                sheet.Cell(1, i + 1).Value = headers[i];
            }

            var row = 2;
            foreach (var t in AllTransactions.OrderByDescending(x => x.TransactionDate))
            {
                sheet.Cell(row, 1).Value = t.Type;
                sheet.Cell(row, 2).Value = t.TransactionDate.DateTime;
                sheet.Cell(row, 2).Style.DateFormat.Format = "dd.MM.yyyy HH:mm";
                sheet.Cell(row, 3).Value = t.DescriptionDisplay ?? string.Empty;
                sheet.Cell(row, 4).Value = t.CategoryDisplay ?? string.Empty;
                sheet.Cell(row, 5).Value = t.Amount;
                sheet.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00";
                sheet.Cell(row, 6).Value = t.Currency;
                sheet.Cell(row, 7).Value = t.AccountDisplay;
                row++;
            }

            var usedRange = sheet.RangeUsed();
            if (usedRange is not null)
            {
                usedRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                usedRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                sheet.Row(1).Style.Font.Bold = true;
                sheet.Row(1).Style.Fill.BackgroundColor = XLColor.FromHtml("#EAF3FF");
                sheet.Columns().AdjustToContents();
            }

            workbook.SaveAs(filePath);
            ReportStatusMessage = $"Excel export: {filePath}";
        }
        catch (Exception ex)
        {
            ReportStatusMessage = $"{Texts.RequestErrorPrefix}: {ex.Message}";
        }
        finally
        {
            IsApiBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExportReportPdfAsync()
    {
        if (IsApiBusy)
        {
            return;
        }

        try
        {
            IsApiBusy = true;
            await LoadReportingDataAsync();
            var filePath = await SelectReportSavePathAsync("Save PDF report", "pdf");
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);
                    page.Header().Text("KeepWallet Profile Report").SemiBold().FontSize(18);
                    page.Content().Column(col =>
                    {
                        col.Spacing(8);
                        col.Item().Text($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}");
                        col.Item().Text($"Bank Accounts: {BankAccounts.Count}");
                        col.Item().Text($"Savings: {Savings.Count}");
                        col.Item().Text($"Transactions: {AllTransactions.Count}");
                        col.Item().Text($"Income: {ReportIncomeTotal:N2}");
                        col.Item().Text($"Expense: {ReportExpenseTotal:N2}");
                        col.Item().Text($"Balance: {ReportBalanceTotal:N2}");
                        col.Item().LineHorizontal(1);
                        foreach (var t in AllTransactions.OrderByDescending(x => x.TransactionDate).Take(200))
                        {
                            col.Item().Text($"{t.TransactionDateDisplay} | {t.Type} | {t.Amount:N2} {t.Currency} | {t.CategoryDisplay} | {t.DescriptionDisplay}");
                        }
                    });
                });
            }).GeneratePdf(filePath);

            ReportStatusMessage = $"PDF export: {filePath}";
        }
        catch (Exception ex)
        {
            ReportStatusMessage = $"{Texts.RequestErrorPrefix}: {ex.Message}";
        }
        finally
        {
            IsApiBusy = false;
        }
    }

    private async Task<string?> SelectReportSavePathAsync(string title, string extension)
    {
        var suggestedFileName = $"report-{DateTime.Now:yyyyMMdd-HHmmss}.{extension}";
        var selectedPath = SaveReportFilePickerAsync is not null
            ? await SaveReportFilePickerAsync(title, suggestedFileName, extension)
            : null;

        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return null;
        }

        var directory = Path.GetDirectoryName(selectedPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return Path.HasExtension(selectedPath)
            ? selectedPath
            : $"{selectedPath}.{extension}";
    }

}
