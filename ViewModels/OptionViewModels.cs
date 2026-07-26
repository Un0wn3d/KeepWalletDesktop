using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Media.Imaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace KeepWalletDesktop.ViewModels;

public partial class LanguageOptionViewModel : ObservableObject
{
    public LanguageOptionViewModel(string code, string displayName)
    {
        Code = code;
        this.displayName = displayName;
    }

    public string Code { get; }

    [ObservableProperty]
    private string displayName;
}

public partial class CategoryOptionViewModel : ObservableObject
{
    public CategoryOptionViewModel(string code, string displayName)
    {
        Code = code;
        this.displayName = displayName;
    }

    public string Code { get; }

    [ObservableProperty]
    private string displayName;
}

public sealed record BankAccountFilterOptionViewModel(Guid? AccountId, string DisplayName);

public partial class FilterSelectionOptionViewModel : ObservableObject
{
    public FilterSelectionOptionViewModel(string code, string displayName, bool isSelected = true)
    {
        Code = code;
        DisplayName = displayName;
        this.isSelected = isSelected;
    }

    public string Code { get; }
    public string DisplayName { get; }

    [ObservableProperty]
    private bool isSelected;
}

public partial class OnboardingCategoryOptionViewModel : ObservableObject
{
    public OnboardingCategoryOptionViewModel(int id, string name, string type, string typeDisplay, bool isSelected)
    {
        Id = id;
        Name = name;
        Type = type;
        TypeDisplay = typeDisplay;
        this.isSelected = isSelected;
    }

    public int Id { get; }
    public string Name { get; }
    public string Type { get; }
    public string TypeDisplay { get; }
    public bool IsIncome => string.Equals(Type, "income", System.StringComparison.OrdinalIgnoreCase);
    public string IconGlyph => IsIncome ? "\uE4F1" : "\uE4F5";
    public string ChipClass => IsIncome ? "income-chip" : "expense-chip";

    [ObservableProperty]
    private bool isSelected;
}

public partial class CategoryManagementItemViewModel : ObservableObject
{
    public CategoryManagementItemViewModel(int id, string name, string type, string typeDisplay, string iconKey, string color, bool isSelected)
    {
        Id = id;
        this.name = name;
        Type = type;
        TypeDisplay = typeDisplay;
        nameInput = name;
        selectedIconKey = iconKey;
        selectedColor = color;
        this.isSelected = isSelected;
    }

    public int Id { get; }
    [ObservableProperty]
    private string name;

    [ObservableProperty]
    private string nameInput;

    [ObservableProperty]
    private bool isEditingName;

    public string Type { get; }
    public string TypeDisplay { get; }
    public string Initial => string.IsNullOrWhiteSpace(Name) ? "?" : Name.Trim()[0].ToString().ToUpperInvariant();
    public string IconGlyph => CategoryIconCatalog.GetGlyph(SelectedIconKey, Type);
    public Bitmap? IconSampleImage => CategoryIconCatalog.GetSampleImage(SelectedIconKey);
    public bool HasIconSampleImage => IconSampleImage is not null;

    [ObservableProperty]
    private bool isSelected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IconGlyph))]
    [NotifyPropertyChangedFor(nameof(IconSampleImage))]
    [NotifyPropertyChangedFor(nameof(HasIconSampleImage))]
    private string selectedIconKey;

    [ObservableProperty]
    private string selectedColor;

    public void RefreshIconGlyph()
    {
        OnPropertyChanged(nameof(IconGlyph));
        OnPropertyChanged(nameof(IconSampleImage));
        OnPropertyChanged(nameof(HasIconSampleImage));
    }
}

public sealed record CategoryIconOptionViewModel(
    string Key,
    string Glyph,
    string DisplayName,
    string MainCategory = "",
    string SubCategory = "")
{
    public bool Matches(string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return true;
        }

        return DisplayName.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
            MainCategory.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
            SubCategory.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
            Glyph.Contains(searchText, StringComparison.OrdinalIgnoreCase);
    }
}

public static class CategoryIconCatalog
{
    private static readonly ILogger EmojiLog = Log.ForContext("SourceContext", "CategoryIconCatalog");
    private static readonly Lock SyncRoot = new();
    private static readonly string OperatingFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KeepWallet",
        "UnicodeEmoji");
    private const string EmojiTestUrl = "https://www.unicode.org/Public/latest/emoji/emoji-test.txt";
    private const string EmojiJsonFileName = "filtered-emojis.json";
    private const string EmojiTestFileName = "emoji-test.txt";
    private static EmojiCatalogData catalogData = LoadCachedCatalogData();
    private static Task? preloadTask;

    private static readonly IReadOnlyDictionary<string, string> LegacyGlyphs = new Dictionary<string, string>
    {
        ["groceries"] = FromCodePoint(0x1F6D2),
        ["transport"] = FromCodePoint(0x1F697),
        ["home"] = FromCodePoint(0x1F3E0),
        ["entertainment"] = FromCodePoint(0x1F3AE),
        ["health"] = FromCodePoint(0x1F48A),
        ["shopping"] = FromCodePoint(0x1F6CD),
        ["education"] = FromCodePoint(0x1F393),
        ["cafe"] = FromCodePoint(0x2615),
        ["income"] = FromCodePoint(0x1F4B0),
        ["work"] = FromCodePoint(0x1F4BC),
        ["other"] = FromCodePoint(0x2728)
    };

    public static IReadOnlyList<CategoryIconOptionViewModel> Options => catalogData.Options;
    public static IReadOnlyList<string> Categories => catalogData.Categories;

    public static Task PreloadAsync()
    {
        lock (SyncRoot)
        {
            preloadTask ??= Task.Run(() =>
            {
                EmojiLog.Information("Emoji catalog preload started. Folder={Folder}", OperatingFolder);
                var loadedData = LoadEmojiCatalogData();
                lock (SyncRoot)
                {
                    catalogData = loadedData;
                }

                EmojiLog.Information(
                    "Emoji catalog preload completed. OptionsCount={OptionsCount}, CategoriesCount={CategoriesCount}",
                    loadedData.Options.Count,
                    loadedData.Categories.Count);
            });
            return preloadTask;
        }
    }

    public static bool HasCachedCatalog()
    {
        try
        {
            var path = GetEmojiJsonPath();
            return File.Exists(path) && new FileInfo(path).Length > 5;
        }
        catch
        {
            return false;
        }
    }

    public static string GetGlyph(string? iconKey, string? categoryType = null)
    {
        var key = string.IsNullOrWhiteSpace(iconKey)
            ? (string.Equals(categoryType, "income", System.StringComparison.OrdinalIgnoreCase) ? "income" : "other")
            : iconKey.Trim().ToLowerInvariant();

        if (LegacyGlyphs.TryGetValue(key, out var legacyGlyph))
        {
            return legacyGlyph;
        }

        if (IsEmojiGlyphKey(key))
        {
            return key;
        }

        var option = Options.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.Ordinal));
        if (option is not null)
        {
            return option.Glyph;
        }

        var fallbackGlyph = string.Equals(categoryType, "income", System.StringComparison.OrdinalIgnoreCase)
            ? LegacyGlyphs["income"]
            : LegacyGlyphs["other"];
        return fallbackGlyph;
    }

    public static Bitmap? GetSampleImage(string? iconKey) => null;

    private static EmojiCatalogData LoadEmojiCatalogData()
    {
        try
        {
            Directory.CreateDirectory(OperatingFolder);
            EmojiLog.Information("Emoji catalog loading started. Folder={Folder}", OperatingFolder);
            var emojiTestPath = GetEmojiTestPath();
            if (!File.Exists(emojiTestPath) || new FileInfo(emojiTestPath).Length == 0)
            {
                EmojiLog.Information("Emoji test file missing or empty. Downloading from {Url}", EmojiTestUrl);
                var emojiTest = DownloadEmojiTestAsync().GetAwaiter().GetResult();
                File.WriteAllText(emojiTestPath, emojiTest, System.Text.Encoding.UTF8);
                EmojiLog.Information(
                    "Emoji test file downloaded. Path={Path}, Length={Length}",
                    emojiTestPath,
                    emojiTest.Length);
            }
            else
            {
                EmojiLog.Information(
                    "Emoji test file found in cache. Path={Path}, Size={Size}",
                    emojiTestPath,
                    new FileInfo(emojiTestPath).Length);
            }

            var options = ParseEmojiTestFile(File.ReadAllLines(emojiTestPath));
            var json = JsonSerializer.Serialize(options, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(GetEmojiJsonPath(), json, System.Text.Encoding.UTF8);
            EmojiLog.Information(
                "Emoji catalog parsed and cached. OptionsCount={OptionsCount}, JsonPath={JsonPath}",
                options.Count,
                GetEmojiJsonPath());

            return new EmojiCatalogData(
                options.Count > 0 ? options : GetFallbackEmojiOptions(),
                options.Select(x => x.MainCategory).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList());
        }
        catch (Exception ex)
        {
            EmojiLog.Error(ex, "Emoji catalog loading failed. Fallback options will be used.");
            return new EmojiCatalogData(GetFallbackEmojiOptions(), []);
        }
    }

    private static async Task<string> DownloadEmojiTestAsync() => await DownloadStringWithRedirectsAsync(EmojiTestUrl);

    private static async Task<string> DownloadStringWithRedirectsAsync(string url)
    {
        using var httpClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
        var requestUri = new Uri(url);

        for (var redirect = 0; redirect < 5; redirect++)
        {
            using var response = await httpClient.GetAsync(requestUri);
            if ((int)response.StatusCode is >= 300 and < 400 && response.Headers.Location is not null)
            {
                requestUri = response.Headers.Location.IsAbsoluteUri
                    ? response.Headers.Location
                    : new Uri(requestUri, response.Headers.Location);

                if (requestUri.Scheme == Uri.UriSchemeHttp)
                {
                    requestUri = new UriBuilder(requestUri) { Scheme = Uri.UriSchemeHttps, Port = -1 }.Uri;
                }

                EmojiLog.Information("Emoji download redirected. From={From}, To={To}", url, requestUri);
                continue;
            }

            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            EmojiLog.Information(
                "Emoji download completed. Url={Url}, FinalUrl={FinalUrl}, Length={Length}",
                url,
                requestUri,
                content.Length);
            return content;
        }

        throw new HttpRequestException($"Too many redirects while downloading {url}.");
    }

    private static EmojiCatalogData LoadCachedCatalogData()
    {
        try
        {
            var emojiJsonPath = GetEmojiJsonPath();
            if (!File.Exists(emojiJsonPath) || new FileInfo(emojiJsonPath).Length <= 5)
            {
                EmojiLog.Information("Emoji cached catalog missing or empty. Path={Path}", emojiJsonPath);
                return new EmojiCatalogData(GetFallbackEmojiOptions(), []);
            }

            EmojiLog.Information("Emoji cached catalog loading. Path={Path}, Size={Size}", emojiJsonPath, new FileInfo(emojiJsonPath).Length);
            using var document = JsonDocument.Parse(File.ReadAllText(emojiJsonPath));
            var options = document.RootElement
                .EnumerateArray()
                .Select(ReadEmojiOptionFromCache)
                .Where(option => option is not null)
                .Cast<CategoryIconOptionViewModel>()
                .ToList();

            EmojiLog.Information(
                "Emoji cached catalog loaded fast. OptionsCount={OptionsCount}",
                options.Count);
            return new EmojiCatalogData(
                options.Count > 0 ? options : GetFallbackEmojiOptions(),
                options.Select(x => x.MainCategory).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList());
        }
        catch (Exception ex)
        {
            EmojiLog.Warning(ex, "Emoji cached catalog loading failed. Fallback options will be used.");
            return new EmojiCatalogData(GetFallbackEmojiOptions(), []);
        }
    }

    private static CategoryIconOptionViewModel? ReadEmojiOptionFromCache(JsonElement emojiElement)
    {
        var key = GetJsonString(emojiElement, "key", "Key", "value", "Value");
        var glyph = GetJsonString(emojiElement, "glyph", "Glyph", "value", "Value");
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(glyph))
        {
            return null;
        }

        return new CategoryIconOptionViewModel(
            key,
            glyph,
            GetJsonString(emojiElement, "displayName", "DisplayName", "name", "Name") ?? key,
            GetJsonString(emojiElement, "mainCategory", "MainCategory") ?? string.Empty,
            GetJsonString(emojiElement, "subCategory", "SubCategory") ?? string.Empty);
    }

    private static string? GetJsonString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String)
            {
                return property.GetString();
            }
        }

        return null;
    }

    private static List<CategoryIconOptionViewModel> ParseEmojiTestFile(IEnumerable<string> lines)
    {
        var options = new List<CategoryIconOptionViewModel>();
        var mainCategory = string.Empty;
        var subCategory = string.Empty;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.StartsWith("# group:", StringComparison.OrdinalIgnoreCase))
            {
                mainCategory = line["# group:".Length..].Trim();
                continue;
            }

            if (line.StartsWith("# subgroup:", StringComparison.OrdinalIgnoreCase))
            {
                subCategory = line["# subgroup:".Length..].Trim();
                continue;
            }

            if (line.Length == 0 || line.StartsWith('#') || !line.Contains("; fully-qualified", StringComparison.Ordinal))
            {
                continue;
            }

            var semicolonIndex = line.IndexOf(';');
            var codePart = line[..semicolonIndex].Trim();
            var codePoints = codePart.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (codePoints.Any(IsIgnoredModifierCode))
            {
                continue;
            }

            var hashIndex = line.IndexOf('#');
            if (hashIndex < 0)
            {
                continue;
            }

            var comment = line[(hashIndex + 1)..].Trim();
            var commentParts = comment.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
            if (commentParts.Length < 3)
            {
                continue;
            }

            var glyph = commentParts[0];
            var name = commentParts[2];
            options.Add(new CategoryIconOptionViewModel(
                glyph,
                glyph,
                name,
                mainCategory,
                subCategory));
        }

        return options
            .GroupBy(x => x.Key)
            .Select(x => x.First())
            .ToList();
    }

    private static bool IsIgnoredModifierCode(string hexCode) =>
        int.TryParse(hexCode, System.Globalization.NumberStyles.HexNumber, null, out var codePoint) &&
        (codePoint is >= 0x1F3FB and <= 0x1F3FF || codePoint is >= 0x1F9B0 and <= 0x1F9B3);

    private static bool IsEmojiGlyphKey(string key) =>
        key.EnumerateRunes().Any(rune => rune.Value is
            >= 0x1F000 and <= 0x1FAFF or
            >= 0x2600 and <= 0x27BF);

    private static string FromCodePoint(int codePoint) => char.ConvertFromUtf32(codePoint);

    private static string GetEmojiJsonPath() => Path.Combine(OperatingFolder, EmojiJsonFileName);
    private static string GetEmojiTestPath() => Path.Combine(OperatingFolder, EmojiTestFileName);

    private static IReadOnlyList<CategoryIconOptionViewModel> GetFallbackEmojiOptions()
    {
        var ranges = new (int Start, int End)[]
        {
            (0x1F300, 0x1F5FF),
            (0x1F600, 0x1F64F),
            (0x1F680, 0x1F6FF),
            (0x1F700, 0x1F77F),
            (0x1F780, 0x1F7FF),
            (0x1F800, 0x1F8FF),
            (0x1F900, 0x1F9FF),
            (0x1FA70, 0x1FAFF),
            (0x2600, 0x26FF),
            (0x2700, 0x27BF)
        };

        var options = new List<CategoryIconOptionViewModel>();
        foreach (var (start, end) in ranges)
        {
            for (var codePoint = start; codePoint <= end; codePoint++)
            {
                if (!IsSupportedEmojiCodePoint(codePoint))
                {
                    continue;
                }

                var glyph = FromCodePoint(codePoint);
                if (glyph.EnumerateRunes().Any(rune =>
                    rune.Value is >= 0x1F3FB and <= 0x1F3FF ||
                    rune.Value is >= 0x1F9B0 and <= 0x1F9B3))
                {
                    continue;
                }

                options.Add(new CategoryIconOptionViewModel(glyph, glyph, $"U+{codePoint:X}", "Unicode", "Fallback"));
            }
        }

        EmojiLog.Information("Emoji fallback catalog generated. OptionsCount={OptionsCount}", options.Count);
        return options;
    }

    private static bool IsSupportedEmojiCodePoint(int codePoint)
    {
        var category = char.GetUnicodeCategory(FromCodePoint(codePoint), 0);
        return category is not System.Globalization.UnicodeCategory.Control
            and not System.Globalization.UnicodeCategory.PrivateUse
            and not System.Globalization.UnicodeCategory.Surrogate;
    }

    private sealed record EmojiCatalogData(
        IReadOnlyList<CategoryIconOptionViewModel> Options,
        IReadOnlyList<string> Categories);
}
