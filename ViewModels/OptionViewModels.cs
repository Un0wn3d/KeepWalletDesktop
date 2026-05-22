using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Media.Imaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

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
    public CategoryManagementItemViewModel(int id, string name, string type, string typeDisplay, string iconKey, bool isSelected)
    {
        Id = id;
        this.name = name;
        Type = type;
        TypeDisplay = typeDisplay;
        nameInput = name;
        selectedIconKey = iconKey;
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
    string SubCategory = "",
    string? SampleImageSource = null)
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
    private static readonly Lock SyncRoot = new();
    private static readonly string OperatingFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KeepWallet",
        "UnicodeEmoji");
    private const string EmojiTestUrl = "https://www.unicode.org/Public/latest/emoji/emoji-test.txt";
    private const string EmojiJsonFileName = "filtered-emojis.json";
    private const string EmojiTestFileName = "emoji-test.txt";
    private static readonly (string Url, string FileName)[] SampleChartSources =
    [
        ("https://www.unicode.org/emoji/charts/full-emoji-list.html", "unicode-full-list.html"),
        ("https://www.unicode.org/emoji/charts/full-emoji-modifiers.html", "unicode-modifier-list.html")
    ];
    private static EmojiCatalogData catalogData = LoadCachedCatalogData();
    private static Task? preloadTask;
    private static readonly Dictionary<string, Bitmap?> SampleImageCache = [];

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
                var loadedData = LoadEmojiCatalogData();
                lock (SyncRoot)
                {
                    catalogData = loadedData;
                }
            });
            return preloadTask;
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

        return Options.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.Ordinal))?.Glyph
            ?? (string.Equals(categoryType, "income", System.StringComparison.OrdinalIgnoreCase) ? LegacyGlyphs["income"] : LegacyGlyphs["other"]);
    }

    public static Bitmap? GetSampleImage(string? iconKey)
    {
        if (string.IsNullOrWhiteSpace(iconKey))
        {
            return null;
        }

        var sampleImageSource = Options.FirstOrDefault(x => string.Equals(x.Key, iconKey, StringComparison.Ordinal))?.SampleImageSource;
        if (string.IsNullOrWhiteSpace(sampleImageSource))
        {
            return null;
        }

        lock (SampleImageCache)
        {
            if (SampleImageCache.TryGetValue(sampleImageSource, out var cachedImage))
            {
                return cachedImage;
            }

            var image = CreateBitmap(sampleImageSource);
            SampleImageCache[sampleImageSource] = image;
            return image;
        }
    }

    private static EmojiCatalogData LoadEmojiCatalogData()
    {
        try
        {
            Directory.CreateDirectory(OperatingFolder);
            var emojiTestPath = GetEmojiTestPath();
            if (!File.Exists(emojiTestPath) || new FileInfo(emojiTestPath).Length == 0)
            {
                var emojiTest = DownloadEmojiTestAsync().GetAwaiter().GetResult();
                File.WriteAllText(emojiTestPath, emojiTest, System.Text.Encoding.UTF8);
            }

            EnsureSampleChartsAsync().GetAwaiter().GetResult();
            var sampleImages = LoadSampleImages();
            var options = ParseEmojiTestFile(File.ReadAllLines(emojiTestPath), sampleImages);
            var json = JsonSerializer.Serialize(options, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(GetEmojiJsonPath(), json, System.Text.Encoding.UTF8);

            return new EmojiCatalogData(
                options.Count > 0 ? options : GetFallbackEmojiOptions(),
                options.Select(x => x.MainCategory).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList());
        }
        catch
        {
            return new EmojiCatalogData(GetFallbackEmojiOptions(), []);
        }
    }

    private static async Task<string> DownloadEmojiTestAsync() => await DownloadStringWithRedirectsAsync(EmojiTestUrl);

    private static async Task EnsureSampleChartsAsync()
    {
        foreach (var (url, fileName) in SampleChartSources)
        {
            var path = Path.Combine(OperatingFolder, fileName);
            if (File.Exists(path) && new FileInfo(path).Length > 0)
            {
                continue;
            }

            var html = await DownloadStringWithRedirectsAsync(url);
            File.WriteAllText(path, html, System.Text.Encoding.UTF8);
        }
    }

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

                continue;
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
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
                return new EmojiCatalogData(GetFallbackEmojiOptions(), []);
            }

            using var document = JsonDocument.Parse(File.ReadAllText(emojiJsonPath));
            var sampleImages = LoadSampleImages();
            var options = document.RootElement
                .EnumerateArray()
                .Select(emojiElement => ReadEmojiOption(emojiElement, sampleImages))
                .Where(option => option is not null)
                .Cast<CategoryIconOptionViewModel>()
                .ToList();

            return new EmojiCatalogData(
                options.Count > 0 ? options : GetFallbackEmojiOptions(),
                options.Select(x => x.MainCategory).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList());
        }
        catch
        {
            return new EmojiCatalogData(GetFallbackEmojiOptions(), []);
        }
    }

    private static CategoryIconOptionViewModel? ReadEmojiOption(
        JsonElement emojiElement,
        IReadOnlyDictionary<string, string> sampleImages)
    {
        var value = GetJsonString(emojiElement, "value", "Value");
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return new CategoryIconOptionViewModel(
            value,
            value,
            GetJsonString(emojiElement, "name", "Name") ?? value,
            GetJsonString(emojiElement, "mainCategory", "MainCategory") ?? string.Empty,
            GetJsonString(emojiElement, "subCategory", "SubCategory") ?? string.Empty,
            sampleImages.GetValueOrDefault(value) ?? GetJsonString(emojiElement, "sampleImageSource", "SampleImageSource"));
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

    private static List<CategoryIconOptionViewModel> ParseEmojiTestFile(
        IEnumerable<string> lines,
        IReadOnlyDictionary<string, string> sampleImages)
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
                subCategory,
                sampleImages.GetValueOrDefault(glyph)));
        }

        return options
            .GroupBy(x => x.Key)
            .Select(x => x.First())
            .ToList();
    }

    private static bool IsIgnoredModifierCode(string hexCode) =>
        int.TryParse(hexCode, System.Globalization.NumberStyles.HexNumber, null, out var codePoint) &&
        (codePoint is >= 0x1F3FB and <= 0x1F3FF || codePoint is >= 0x1F9B0 and <= 0x1F9B3);

    private static string FromCodePoint(int codePoint) => char.ConvertFromUtf32(codePoint);

    private static string GetEmojiJsonPath() => Path.Combine(OperatingFolder, EmojiJsonFileName);
    private static string GetEmojiTestPath() => Path.Combine(OperatingFolder, EmojiTestFileName);

    private static IReadOnlyDictionary<string, string> LoadSampleImages()
    {
        var images = new Dictionary<string, string>();
        foreach (var (_, fileName) in SampleChartSources)
        {
            var htmlPath = Path.Combine(OperatingFolder, fileName);
            if (!File.Exists(htmlPath))
            {
                continue;
            }

            var html = File.ReadAllText(htmlPath);
            foreach (Match match in Regex.Matches(
                html,
                "<img\\s+(?=[^>]*\\balt='(?<alt>[^']+)')(?=[^>]*\\bsrc='(?<src>data:image/png[^']+)')[^>]*>",
                RegexOptions.IgnoreCase))
            {
                var glyph = DecodeHtmlEmoji(match.Groups["alt"].Value);
                if (string.IsNullOrWhiteSpace(glyph) || images.ContainsKey(glyph))
                {
                    continue;
                }

                images[glyph] = match.Groups["src"].Value;
            }
        }

        return images;
    }

    private static string DecodeHtmlEmoji(string htmlEmoji)
    {
        return Regex.Replace(htmlEmoji, "&#x(?<hex>[0-9A-Fa-f]+);", match =>
            char.ConvertFromUtf32(Convert.ToInt32(match.Groups["hex"].Value, 16)));
    }

    private static Bitmap? CreateBitmap(string dataUri)
    {
        var commaIndex = dataUri.IndexOf(',');
        if (commaIndex < 0)
        {
            return null;
        }

        try
        {
            var bytes = Convert.FromBase64String(dataUri[(commaIndex + 1)..]);
            return new Bitmap(new MemoryStream(bytes));
        }
        catch
        {
            return null;
        }
    }

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
