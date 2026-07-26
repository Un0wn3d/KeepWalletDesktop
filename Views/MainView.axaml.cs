using Avalonia.Controls;
using Avalonia.Platform.Storage;
using KeepWalletDesktop.ViewModels;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace KeepWalletDesktop.Views;

public partial class MainView : Window
{
    public MainView()
    {
        InitializeComponent();

        if (DataContext is null)
        {
            DataContext = new MainViewModel();
        }

        AttachFilePicker(DataContext);
        DataContextChanged += (_, _) => AttachFilePicker(DataContext);
    }

    private void AttachFilePicker(object? dataContext)
    {
        if (dataContext is MainViewModel viewModel)
        {
            viewModel.SaveReportFilePickerAsync = PickSaveReportFileAsync;
        }
    }

    private async Task<string?> PickSaveReportFileAsync(string title, string suggestedFileName, string extension)
    {
        var normalizedExtension = extension.TrimStart('.');
        var fileTypeName = normalizedExtension.Equals("pdf", StringComparison.OrdinalIgnoreCase)
            ? "PDF"
            : "Excel";

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedFileName,
            DefaultExtension = normalizedExtension,
            FileTypeChoices =
            [
                new FilePickerFileType(fileTypeName)
                {
                    Patterns = [$"*.{normalizedExtension}"]
                }
            ]
        });

        return file?.Path.LocalPath;
    }
}
