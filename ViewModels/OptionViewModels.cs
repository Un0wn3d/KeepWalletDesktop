using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Linq;

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
        Name = name;
        Type = type;
        TypeDisplay = typeDisplay;
        selectedIconKey = iconKey;
        this.isSelected = isSelected;
    }

    public int Id { get; }
    public string Name { get; }
    public string Type { get; }
    public string TypeDisplay { get; }
    public string Initial => string.IsNullOrWhiteSpace(Name) ? "?" : Name.Trim()[0].ToString().ToUpperInvariant();
    public string IconGlyph => CategoryIconCatalog.GetGlyph(SelectedIconKey, Type);

    [ObservableProperty]
    private bool isSelected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IconGlyph))]
    private string selectedIconKey;
}

public sealed record CategoryIconOptionViewModel(string Key, string Glyph, string DisplayName);

public static class CategoryIconCatalog
{
    public static IReadOnlyList<CategoryIconOptionViewModel> Options { get; } =
    [
        new("groceries", "🛒", "Groceries"),
        new("transport", "⌁", "Transport"),
        new("home", "⌂", "Home"),
        new("entertainment", "✦", "Entertainment"),
        new("health", "+", "Health"),
        new("shopping", "◈", "Shopping"),
        new("education", "▣", "Education"),
        new("cafe", "☕", "Cafe"),
        new("income", "$", "Income"),
        new("work", "⚙", "Work"),
        new("other", "•", "Other")
    ];

    public static string GetGlyph(string? iconKey, string? categoryType = null)
    {
        var key = string.IsNullOrWhiteSpace(iconKey)
            ? (string.Equals(categoryType, "income", System.StringComparison.OrdinalIgnoreCase) ? "income" : "other")
            : iconKey.Trim().ToLowerInvariant();

        return Options.FirstOrDefault(x => x.Key == key)?.Glyph
            ?? (string.Equals(categoryType, "income", System.StringComparison.OrdinalIgnoreCase) ? "$" : "•");
    }
}
