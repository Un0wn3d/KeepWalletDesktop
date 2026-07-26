using System;
using System.Linq;
using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;

namespace KeepWalletDesktop.Services;

public static class ThemeManager
{
    public const string Light = "Light";
    public const string Dark = "Dark";

    private static readonly Uri LightResources = new("avares://KeepWalletDesktop/Assets/Styles/Themes/Light/Theme.axaml");
    private static readonly Uri LightStyles = new("avares://KeepWalletDesktop/Assets/Styles/Themes/Light/AppTheme.axaml");
    private static readonly Uri DarkResources = new("avares://KeepWalletDesktop/Assets/Styles/Themes/Dark/Theme.axaml");
    private static readonly Uri DarkStyles = new("avares://KeepWalletDesktop/Assets/Styles/Themes/Dark/AppTheme.axaml");

    public static string NormalizeThemeName(string? themeName) =>
        string.Equals(themeName, Dark, StringComparison.OrdinalIgnoreCase) ? Dark : Light;

    public static void Apply(string? themeName)
    {
        var app = Application.Current;
        if (app is null)
        {
            return;
        }

        var normalizedTheme = NormalizeThemeName(themeName);
        var resourceSource = normalizedTheme == Dark ? DarkResources : LightResources;
        var styleSource = normalizedTheme == Dark ? DarkStyles : LightStyles;
        app.RequestedThemeVariant = normalizedTheme == Dark ? ThemeVariant.Dark : ThemeVariant.Light;

        var oldThemeResources = app.Resources.MergedDictionaries
            .OfType<ResourceInclude>()
            .Where(x => IsThemeResource(x.Source))
            .ToArray();
        foreach (var resource in oldThemeResources)
        {
            app.Resources.MergedDictionaries.Remove(resource);
        }

        app.Resources.MergedDictionaries.Add(new ResourceInclude(resourceSource)
        {
            Source = resourceSource
        });

        var oldThemeStyles = app.Styles
            .OfType<StyleInclude>()
            .Where(x => IsThemeStyle(x.Source))
            .ToArray();
        foreach (var style in oldThemeStyles)
        {
            app.Styles.Remove(style);
        }

        app.Styles.Add(new StyleInclude(styleSource)
        {
            Source = styleSource
        });
    }

    private static bool IsThemeResource(Uri? source)
    {
        var value = source?.ToString() ?? string.Empty;
        return value.Contains("/Assets/Styles/Themes/Light/Theme.axaml", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("/Assets/Styles/Themes/Dark/Theme.axaml", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("/Assets/Styles/Colors.axaml", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsThemeStyle(Uri? source)
    {
        var value = source?.ToString() ?? string.Empty;
        return value.Contains("/Assets/Styles/AppTheme.axaml", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("/Assets/Styles/Themes/Light/AppTheme.axaml", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("/Assets/Styles/Themes/Dark/AppTheme.axaml", StringComparison.OrdinalIgnoreCase);
    }
}
