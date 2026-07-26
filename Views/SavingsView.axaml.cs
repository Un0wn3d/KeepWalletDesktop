using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using KeepWalletDesktop.ViewModels;
using KeepWalletDesktop.Views.Modules;

namespace KeepWalletDesktop.Views;

public partial class SavingsView : UserControl
{
    private readonly IconPickerModuleView iconPicker = new();

    public SavingsView()
    {
        InitializeComponent();
    }

    private void ActiveSavingIcon_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Control owner || DataContext is not MainViewModel viewModel)
        {
            return;
        }

        iconPicker.Show(
            owner,
            viewModel.CategoryIconOptions,
            viewModel.SavingColorOptions,
            iconKey =>
            {
                viewModel.ActiveSavingIconKey = iconKey;
                if (viewModel.SaveSavingIconColorImmediatelyCommand.CanExecute(null))
                {
                    _ = viewModel.SaveSavingIconColorImmediatelyCommand.ExecuteAsync(null);
                }
            },
            color =>
            {
                viewModel.ActiveSavingColor = color;
                if (viewModel.SaveSavingIconColorImmediatelyCommand.CanExecute(null))
                {
                    _ = viewModel.SaveSavingIconColorImmediatelyCommand.ExecuteAsync(null);
                }
            },
            viewModel.Texts.IconSearchHint,
            circularIcons: true);
    }

    private void ActiveSavingIcon_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control owner || DataContext is not MainViewModel viewModel)
        {
            return;
        }

        iconPicker.Show(
            owner,
            viewModel.CategoryIconOptions,
            viewModel.SavingColorOptions,
            iconKey =>
            {
                viewModel.ActiveSavingIconKey = iconKey;
                if (viewModel.SaveSavingIconColorImmediatelyCommand.CanExecute(null))
                {
                    _ = viewModel.SaveSavingIconColorImmediatelyCommand.ExecuteAsync(null);
                }
            },
            color =>
            {
                viewModel.ActiveSavingColor = color;
                if (viewModel.SaveSavingIconColorImmediatelyCommand.CanExecute(null))
                {
                    _ = viewModel.SaveSavingIconColorImmediatelyCommand.ExecuteAsync(null);
                }
            },
            viewModel.Texts.IconSearchHint,
            circularIcons: true);
    }

    private void SavingName_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && viewModel.BeginEditSavingNameCommand.CanExecute(null))
        {
            viewModel.BeginEditSavingNameCommand.Execute(null);
        }
    }

    private async void SavingNameEditTextBox_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel || !viewModel.SaveSavingNameOnBlurCommand.CanExecute(null))
        {
            return;
        }

        await viewModel.SaveSavingNameOnBlurCommand.ExecuteAsync(null);
    }

    private void SavingGoal_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && viewModel.BeginEditSavingGoalAmountCommand.CanExecute(null))
        {
            viewModel.BeginEditSavingGoalAmountCommand.Execute(null);
        }
    }

    private void SavingCurrent_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && viewModel.BeginEditSavingCurrentAmountCommand.CanExecute(null))
        {
            viewModel.BeginEditSavingCurrentAmountCommand.Execute(null);
        }
    }

    private async void SavingGoalEditTextBox_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && viewModel.SaveSavingGoalOnBlurCommand.CanExecute(null))
        {
            await viewModel.SaveSavingGoalOnBlurCommand.ExecuteAsync(null);
        }
    }

    private async void SavingCurrentEditTextBox_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && viewModel.SaveSavingCurrentOnBlurCommand.CanExecute(null))
        {
            await viewModel.SaveSavingCurrentOnBlurCommand.ExecuteAsync(null);
        }
    }

    private void SavingCurrency_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && viewModel.BeginEditSavingCurrencyCommand.CanExecute(null))
        {
            viewModel.BeginEditSavingCurrencyCommand.Execute(null);
        }
    }

    private async void SavingCurrencyComboBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && viewModel.SaveSavingCurrencyOnSelectCommand.CanExecute(null))
        {
            await viewModel.SaveSavingCurrencyOnSelectCommand.ExecuteAsync(null);
        }
    }

    private void SavingDeadline_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && viewModel.BeginEditSavingDeadlineCommand.CanExecute(null))
        {
            viewModel.BeginEditSavingDeadlineCommand.Execute(null);
        }
    }

    private void SavingGroups_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && viewModel.BeginEditSavingGroupsCommand.CanExecute(null))
        {
            viewModel.BeginEditSavingGroupsCommand.Execute(null);
        }
    }
}
