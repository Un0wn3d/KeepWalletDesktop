using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using AvaloniaUI.DiagnosticsSupport;
using Diplom.Helpers;
using Diplom.ViewModels;
using Diplom.Views;
using Serilog;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System;

namespace Diplom;

public partial class App : Application
{
#if DEBUG
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllocConsole();
#endif

    public override void Initialize()
    {
        SerilogConfig.Initialize();
        AvaloniaXamlLoader.Load(this);

#if DEBUG
        // AllocConsole();
        this.AttachDeveloperTools();
#endif

        Log.Information("App is started!");
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
            desktop.MainWindow = new MainView
            {
                DataContext = new MainViewModel(),
            };
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
        try
        {
            var db = new DatabaseService();
            await db.ReadDataAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Database error");
        }
    }
}