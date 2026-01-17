using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Diagnostics;
using Diplom.ViewModels;
using Serilog;

namespace Diplom.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }

    private void Button_123(object? sender, RoutedEventArgs e)
    {
        Log.Debug("Button clicked");
    }

    private void Button_Setting(object? sender, RoutedEventArgs e)
    {
        Log.Debug("Button clicked");
    }
}