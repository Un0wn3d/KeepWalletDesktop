using System;
using System.IO;
using System.Text.Json;

namespace KeepWalletDesktop.Services;

public sealed class UserSettings
{
    public string ApiBaseUrl { get; set; } = string.Empty;
    public string LastLogin { get; set; } = string.Empty;
    public string ThemeName { get; set; } = ThemeManager.Theme1;
}

public static class UserSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KeepWalletDesktop");

    private static readonly string SettingsPath = Path.Combine(SettingsDirectory, "user-settings.json");

    public static UserSettings Load()
    {
        if (!File.Exists(SettingsPath))
        {
            return new UserSettings();
        }

        try
        {
            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();
        }
        catch
        {
            return new UserSettings();
        }
    }

    public static void Save(UserSettings settings)
    {
        Directory.CreateDirectory(SettingsDirectory);
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(SettingsPath, json);
    }
}
