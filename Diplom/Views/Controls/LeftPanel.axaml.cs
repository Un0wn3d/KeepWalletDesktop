using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Diplom.ViewModels;

namespace Diplom.Views.Controls;

public partial class LeftPanel : UserControl
{
    public LeftPanel()
    {
        InitializeComponent();
    }
    
    //[RelayCommand]
    private void btn_Home(object? sender, RoutedEventArgs e)
    {
        
    }
}