using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using KeepWalletDesktop.ViewModels;
using KeepWalletDesktop.Views.Modules;

namespace KeepWalletDesktop.Views;

public partial class GroupsView : UserControl
{
    private readonly IconPickerModuleView iconPicker = new();

    public GroupsView()
    {
        InitializeComponent();
    }

    private void NewGroupIconButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || DataContext is not MainViewModel viewModel)
        {
            return;
        }

        iconPicker.Show(
            button,
            viewModel.CategoryIconOptions,
            viewModel.SavingColorOptions,
            iconKey => viewModel.NewGroupIconKey = iconKey,
            color => viewModel.NewGroupColor = color,
            viewModel.Texts.IconSearchHint,
            circularIcons: false);
    }

    private void SelectedGroupIconButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button ||
            DataContext is not MainViewModel viewModel ||
            viewModel.SelectedAccessGroup is null)
        {
            return;
        }

        iconPicker.Show(
            button,
            viewModel.CategoryIconOptions,
            viewModel.SavingColorOptions,
            iconKey =>
            {
                viewModel.SelectedAccessGroup.SelectedIconKey = iconKey;
                SaveSelectedGroupImmediately(viewModel);
            },
            color =>
            {
                viewModel.SelectedAccessGroup.SelectedColor = color;
                SaveSelectedGroupImmediately(viewModel);
            },
            viewModel.Texts.IconSearchHint,
            circularIcons: false);
    }

    private static void SaveSelectedGroupImmediately(MainViewModel viewModel)
    {
        if (viewModel.UpdateGroupCommand.CanExecute(null))
        {
            _ = viewModel.UpdateGroupCommand.ExecuteAsync(null);
        }
    }

    private void SelectedGroupName_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && viewModel.EditSelectedGroupNameCommand.CanExecute(null))
        {
            viewModel.EditSelectedGroupNameCommand.Execute(null);
        }
    }

    private void SelectedGroupNameTextBox_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        FinishSelectedGroupNameEdit();
    }

    private void SelectedGroupNameTextBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            FinishSelectedGroupNameEdit();
            e.Handled = true;
        }
    }

    private void FinishSelectedGroupNameEdit()
    {
        if (DataContext is MainViewModel viewModel && viewModel.FinishSelectedGroupNameEditCommand.CanExecute(null))
        {
            viewModel.FinishSelectedGroupNameEditCommand.Execute(null);
        }
    }

    private void GroupMemberRole_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Control { DataContext: GroupMemberItemViewModel member } ||
            DataContext is not MainViewModel viewModel ||
            !viewModel.EditGroupMemberRoleCommand.CanExecute(member))
        {
            return;
        }

        viewModel.EditGroupMemberRoleCommand.Execute(member);
    }

    private void GroupMemberRoleComboBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox { DataContext: GroupMemberItemViewModel member } ||
            DataContext is not MainViewModel viewModel ||
            !member.IsEditingRole ||
            !viewModel.ApplyGroupMemberRoleCommand.CanExecute(member))
        {
            return;
        }

        viewModel.ApplyGroupMemberRoleCommand.Execute(member);
    }

    private void OpenGroupBankAccount_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: BankAccountItemViewModel account } || DataContext is not MainViewModel viewModel)
        {
            return;
        }

        viewModel.SelectedBankAccount = account;

        if (viewModel.SelectBankAccountCommand.CanExecute(account))
        {
            viewModel.SelectBankAccountCommand.Execute(account);
        }
    }

    private void OpenGroupTransaction_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: TransactionItemViewModel transaction } || DataContext is not MainViewModel viewModel)
        {
            return;
        }

        if (viewModel.EditTransactionCommand.CanExecute(transaction))
        {
            viewModel.EditTransactionCommand.Execute(transaction);
        }
    }

    private async void OpenGroupSaving_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: SavingOptionViewModel saving } || DataContext is not MainViewModel viewModel)
        {
            return;
        }

        if (viewModel.OpenSavingsCommand.CanExecute(null))
        {
            await viewModel.OpenSavingsCommand.ExecuteAsync(null);
        }

        if (viewModel.SelectSavingCommand.CanExecute(saving))
        {
            await viewModel.SelectSavingCommand.ExecuteAsync(saving);
        }
    }

    private void CloseGroupSavingOverlay_OnClick(object? sender, RoutedEventArgs e)
    {
        GroupSavingOverlay.IsVisible = false;
    }

    private void GroupSavingOverlay_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        GroupSavingOverlay.IsVisible = false;
    }

    private void GroupSavingDialog_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
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
}
