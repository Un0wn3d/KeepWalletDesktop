using System;
using System.Linq;
using Avalonia;
using Avalonia.Markup.Xaml.Styling;

namespace KeepWalletDesktop.Services;

public static class ThemeManager
{
    public const string Theme1 = "Theme1";
    public const string Theme2 = "Theme2";

    private static readonly Uri Theme1Resources = new("avares://KeepWalletDesktop/Assets/Styles/Themes/Theme1/Theme.axaml");
    private static readonly Uri Theme2Resources = new("avares://KeepWalletDesktop/Assets/Styles/Themes/Theme2/Theme.axaml");
    private static readonly Uri Theme1Styles = new("avares://KeepWalletDesktop/Assets/Styles/AppTheme.axaml");
    private static readonly Uri Theme2Styles = new("avares://KeepWalletDesktop/Assets/Styles/Themes/Theme2/AppTheme.axaml");

    public static string NormalizeThemeName(string? themeName) =>
        string.Equals(themeName, Theme2, StringComparison.OrdinalIgnoreCase) ? Theme2 : Theme1;

    public static void Apply(string? themeName)
    {
        var app = Application.Current;
        if (app is null)
        {
            return;
        }

        var normalizedTheme = NormalizeThemeName(themeName);
        var resourceSource = normalizedTheme == Theme2 ? Theme2Resources : Theme1Resources;
        var styleSource = normalizedTheme == Theme2 ? Theme2Styles : Theme1Styles;

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
        return value.Contains("/Assets/Styles/Themes/Theme1/Theme.axaml", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("/Assets/Styles/Themes/Theme2/Theme.axaml", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("/Assets/Styles/Colors.axaml", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsThemeStyle(Uri? source)
    {
        var value = source?.ToString() ?? string.Empty;
        return value.Contains("/Assets/Styles/AppTheme.axaml", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("/Assets/Styles/Themes/Theme2/AppTheme.axaml", StringComparison.OrdinalIgnoreCase);
    }
}
