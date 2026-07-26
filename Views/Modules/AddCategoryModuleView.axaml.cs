using Avalonia.Controls;
using Avalonia.Interactivity;
using KeepWalletDesktop.ViewModels;

namespace KeepWalletDesktop.Views.Modules;

public partial class AddCategoryModuleView : UserControl
{
    private readonly IconPickerModuleView iconPicker = new();

    public AddCategoryModuleView()
    {
        InitializeComponent();
    }

    private void NewCategoryIconButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || DataContext is not MainViewModel viewModel)
        {
            return;
        }

        iconPicker.Show(
            button,
            viewModel.CategoryIconOptions,
            viewModel.CategoryColorOptions,
            iconKey => viewModel.CategoryNewIconKey = iconKey,
            color => viewModel.CategoryNewColor = color,
            viewModel.Texts.IconSearchHint,
            circularIcons: false);
    }
}
