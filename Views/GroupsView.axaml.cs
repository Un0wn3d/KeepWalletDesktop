using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using KeepWalletDesktop.ViewModels;
using KeepWalletDesktop.Views.Modules;

namespace KeepWalletDesktop.Views;

public partial class GroupsView : UserControl
{
    private readonly IconPickerModule iconPicker = new();

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

        iconPicker.Show(button, viewModel.CategoryIconOptions, iconKey => viewModel.NewGroupIconKey = iconKey, viewModel.Texts.IconSearchHint);
    }

    private void SelectedGroupIconButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button ||
            DataContext is not MainViewModel viewModel ||
            viewModel.SelectedAccessGroup is null)
        {
            return;
        }

        iconPicker.Show(button, viewModel.CategoryIconOptions, iconKey => viewModel.SelectedAccessGroup.SelectedIconKey = iconKey, viewModel.Texts.IconSearchHint);
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

        if (viewModel.OpenBankAccountsCommand.CanExecute(null))
        {
            viewModel.OpenBankAccountsCommand.Execute(null);
        }

        if (viewModel.EditBankAccountCommand.CanExecute(account))
        {
            viewModel.EditBankAccountCommand.Execute(account);
        }
    }

    private void OpenGroupTransaction_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: TransactionItemViewModel transaction } || DataContext is not MainViewModel viewModel)
        {
            return;
        }

        if (viewModel.OpenHomeCommand.CanExecute(null))
        {
            viewModel.OpenHomeCommand.Execute(null);
        }

        if (viewModel.EditTransactionCommand.CanExecute(transaction))
        {
            viewModel.EditTransactionCommand.Execute(transaction);
        }
    }

    private void OpenGroupSaving_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: SavingOptionViewModel saving } || DataContext is not MainViewModel viewModel)
        {
            return;
        }

        if (viewModel.OpenPiggyBankCommand.CanExecute(null))
        {
            viewModel.OpenPiggyBankCommand.Execute(null);
        }

        if (viewModel.SelectSavingCommand.CanExecute(saving))
        {
            viewModel.SelectSavingCommand.Execute(saving);
        }
    }
}
