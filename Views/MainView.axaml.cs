using Avalonia.Controls;
using KeepWalletDesktop.ViewModels;

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
    }
}
