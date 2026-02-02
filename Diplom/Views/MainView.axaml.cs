using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Diagnostics;
using Diplom.ViewModels;
using HarfBuzzSharp;
using Serilog;

namespace Diplom.Views;

public partial class MainView : Window
{
    public MainView()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
        Language.Content = "Language";
    }
}