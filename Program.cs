using Avalonia;
using Serilog;
using System;

namespace KeepWalletDesktop;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]

    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .With(new X11PlatformOptions
            {
                // On Hyprland/XWayland prefer stable GLX path and keep software fallback.
                RenderingMode = new[] { X11RenderingMode.Glx, X11RenderingMode.Software }
            })
            .LogToTrace();
}
