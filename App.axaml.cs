using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using KeepWalletDesktop.Services;
using KeepWalletDesktop.ViewModels;
using KeepWalletDesktop.Views;
using Serilog;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace KeepWalletDesktop;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        CalendarDayButton.DataContextProperty.Changed.AddClassHandler<CalendarDayButton>((button, _) =>
            UpdateTodayClass(button));
    }

    public override void OnFrameworkInitializationCompleted()
    {
        SerilogConfig.Initialize();
        ButtonClickLogger.Initialize();
        ThemeManager.Apply(UserSettingsStore.Load().ThemeName);

        Log.Information("App is started!");

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
            var viewModel = new MainViewModel();
            var mainWindow = new MainView
            {
                DataContext = viewModel,
            };
            mainWindow.Opened += (_, _) => viewModel.StartBackgroundInitialization();
            desktop.MainWindow = mainWindow;
            _ = InitializeAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }

    private async Task InitializeAsync()
    {
        // try
        // {
        //     var db = new DatabaseService();
        //     await db.ReadDataAsync();
        // }
        // catch (Exception ex)
        // {
        //     Log.Error(ex, "Database error");
        // }
    }

    private static void UpdateTodayClass(CalendarDayButton button)
    {
        var today = DateTime.Today;
        var isToday = button.DataContext switch
        {
            DateTime dt => dt.Date == today,
            DateTimeOffset dto => dto.Date == today,
            _ => false
        };

        if (isToday)
        {
            button.Classes.Add("today");
        }
        else
        {
            button.Classes.Remove("today");
        }
    }
}
