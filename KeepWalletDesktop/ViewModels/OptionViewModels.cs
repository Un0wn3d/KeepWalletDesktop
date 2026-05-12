using CommunityToolkit.Mvvm.ComponentModel;

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
    public CategoryManagementItemViewModel(int id, string name, string type, string typeDisplay, bool isSelected)
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

    [ObservableProperty]
    private bool isSelected;
}
