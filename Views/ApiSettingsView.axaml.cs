using Avalonia.Controls;
using Avalonia.Input;
using KeepWalletDesktop.ViewModels;

namespace KeepWalletDesktop.Views;

public partial class ApiSettingsView : UserControl
{
    public ApiSettingsView()
    {
        InitializeComponent();
    }

    private void AdminUserRow_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Control { DataContext: UserListItemViewModel user } ||
            DataContext is not MainViewModel viewModel ||
            !viewModel.BeginEditAdminUserCommand.CanExecute(user))
        {
            return;
        }

        viewModel.BeginEditAdminUserCommand.Execute(user);
    }
}
