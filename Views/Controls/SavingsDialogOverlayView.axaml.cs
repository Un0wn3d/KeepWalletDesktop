using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using KeepWalletDesktop.ViewModels;
using KeepWalletDesktop.Views.Modules;

namespace KeepWalletDesktop.Views.Controls;

public partial class SavingsDialogOverlayView : UserControl
{
    private readonly IconPickerModuleView iconPicker = new();

    public SavingsDialogOverlayView()
    {
        InitializeComponent();
    }

    private void SavingsDialogOverlay_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!ReferenceEquals(e.Source, sender))
        {
            return;
        }

        if (DataContext is MainViewModel viewModel &&
            viewModel.CloseSavingsDialogCommand.CanExecute(null))
        {
            viewModel.CloseSavingsDialogCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void SavingsDialogIconButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || DataContext is not MainViewModel viewModel)
        {
            return;
        }

        iconPicker.Show(
            button,
            viewModel.CategoryIconOptions,
            viewModel.SavingColorOptions,
            iconKey => viewModel.SavingsDialogIconKey = iconKey,
            color => viewModel.SavingsDialogColor = color,
            viewModel.Texts.IconSearchHint,
            circularIcons: true);
    }
}
