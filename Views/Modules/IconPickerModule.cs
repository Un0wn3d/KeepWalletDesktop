using Avalonia.Controls;
using Avalonia;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using KeepWalletDesktop.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KeepWalletDesktop.Views.Modules;

public sealed class IconPickerModule
{
    private const int IconPickerColumns = 7;
    private const int IconPickerCellSize = 46;
    private const int IconPickerWidth = IconPickerColumns * IconPickerCellSize;

    private readonly Flyout iconFlyout;
    private readonly TextBox iconSearchBox;
    private readonly ScrollViewer iconScrollViewer;
    private readonly ScrollViewer colorScrollViewer;
    private readonly TabControl pickerTabs;
    private readonly Border iconTabHeader;
    private readonly Border colorTabHeader;
    private IReadOnlyList<CategoryIconOptionViewModel> pickerOptions = [];
    private IReadOnlyList<string> colorOptions = [];
    private Action<string>? currentSelectIcon;
    private Action<string>? currentSelectColor;
    private bool isCircularMode;

    public IconPickerModule()
    {
        iconSearchBox = new TextBox
        {
            Watermark = "Search emoji",
            Margin = new Avalonia.Thickness(0, 0, 0, 8)
        };
        iconScrollViewer = new ScrollViewer
        {
            Width = IconPickerWidth,
            Height = 360,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
        };
        colorScrollViewer = new ScrollViewer
        {
            Width = IconPickerWidth,
            Height = 360,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
        };
        iconTabHeader = new Border
        {
            Padding = new Thickness(10, 4),
            Child = new TextBlock { Text = "Icons", FontWeight = FontWeight.SemiBold }
        };
        colorTabHeader = new Border
        {
            Padding = new Thickness(10, 4),
            Child = new TextBlock { Text = "Colors", FontWeight = FontWeight.SemiBold }
        };
        pickerTabs = new TabControl
        {
            Items =
            {
                new TabItem
                {
                    Header = iconTabHeader,
                    Content = new StackPanel
                    {
                        Children = { iconSearchBox, iconScrollViewer }
                    }
                },
                new TabItem
                {
                    Header = colorTabHeader,
                    Content = colorScrollViewer
                }
            }
        };
        iconFlyout = new Flyout
        {
            Placement = PlacementMode.Bottom,
            Content = pickerTabs
        };
        iconSearchBox.TextChanged += (_, _) => RebuildIconGrid(iconSearchBox.Text ?? string.Empty);
    }

    public void Prepare(IReadOnlyList<CategoryIconOptionViewModel> options)
    {
        Dispatcher.UIThread.Post(() => SetOptions(options, false), DispatcherPriority.Background);
    }

    public void Show(Control owner, IReadOnlyList<CategoryIconOptionViewModel> options, Action<string> selectIcon, string searchWatermark)
    {
        iconSearchBox.Watermark = searchWatermark;
        currentSelectIcon = selectIcon;
        currentSelectColor = null;
        isCircularMode = false;
        colorOptions = [];
        colorTabHeader.IsVisible = false;
        SetOptions(options, false);
        iconFlyout.ShowAt(owner);
        iconSearchBox.Focus();
    }

    public void Show(
        Control owner,
        IReadOnlyList<CategoryIconOptionViewModel> options,
        IReadOnlyList<string> colors,
        Action<string> selectIcon,
        Action<string> selectColor,
        string searchWatermark,
        bool circularIcons)
    {
        iconSearchBox.Watermark = searchWatermark;
        currentSelectIcon = selectIcon;
        currentSelectColor = selectColor;
        colorOptions = colors;
        isCircularMode = circularIcons;
        colorTabHeader.IsVisible = true;
        SetOptions(options, true);
        RebuildColorGrid();
        pickerTabs.SelectedIndex = 0;
        iconFlyout.ShowAt(owner);
        iconSearchBox.Focus();
    }

    private void SetOptions(IReadOnlyList<CategoryIconOptionViewModel> options, bool forceRefresh)
    {
        if (!forceRefresh && ReferenceEquals(options, pickerOptions))
        {
            return;
        }

        pickerOptions = options;
        iconSearchBox.Text = string.Empty;
        RebuildIconGrid(string.Empty);
    }

    private void RebuildIconGrid(string searchText)
    {
        var filteredOptions = pickerOptions
            .Where(option => option.Matches(searchText))
            .ToList();
        iconScrollViewer.Content = CreateIconGrid(filteredOptions);
    }

    private Grid CreateIconGrid(IReadOnlyList<CategoryIconOptionViewModel> options)
    {
        var grid = new Grid
        {
            Width = IconPickerWidth,
            MinHeight = IconPickerCellSize
        };

        for (var column = 0; column < IconPickerColumns; column++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(IconPickerCellSize, GridUnitType.Pixel));
        }

        var rowCount = Math.Max(1, (int)Math.Ceiling(options.Count / (double)IconPickerColumns));
        for (var row = 0; row < rowCount; row++)
        {
            grid.RowDefinitions.Add(new RowDefinition(IconPickerCellSize, GridUnitType.Pixel));
        }

        for (var index = 0; index < options.Count; index++)
        {
            var option = options[index];
            var iconButton = new Button
            {
                Width = 42,
                Height = 42,
                Padding = new Avalonia.Thickness(0),
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.Parse("#D1D5DB")),
                BorderThickness = new Avalonia.Thickness(1),
                CornerRadius = isCircularMode ? new CornerRadius(21) : new CornerRadius(8),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Content = CreateIconContent(option, 26)
            };

            iconButton.Click += (_, _) =>
            {
                currentSelectIcon?.Invoke(option.Key);
                iconFlyout.Hide();
            };

            Grid.SetColumn(iconButton, index % IconPickerColumns);
            Grid.SetRow(iconButton, index / IconPickerColumns);
            grid.Children.Add(iconButton);
        }

        return grid;
    }

    private void RebuildColorGrid()
    {
        const int colorColumns = 4;
        const int cellSize = 70;
        var grid = new Grid
        {
            Width = IconPickerWidth
        };
        for (var i = 0; i < colorColumns; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(cellSize, GridUnitType.Pixel));
        }

        var rowCount = Math.Max(1, (int)Math.Ceiling(colorOptions.Count / (double)colorColumns));
        for (var i = 0; i < rowCount; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition(cellSize, GridUnitType.Pixel));
        }

        for (var i = 0; i < colorOptions.Count; i++)
        {
            var colorHex = colorOptions[i];
            var parsedColor = TryParseColor(colorHex, "#3B82F6");
            var colorButton = new Button
            {
                Width = 52,
                Height = 52,
                Padding = new Thickness(0),
                Background = new SolidColorBrush(parsedColor),
                BorderBrush = new SolidColorBrush(TryParseColor("#9CA3AF", "#9CA3AF")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(26),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            colorButton.Click += (_, _) =>
            {
                currentSelectColor?.Invoke(colorHex);
                iconFlyout.Hide();
            };

            Grid.SetColumn(colorButton, i % colorColumns);
            Grid.SetRow(colorButton, i / colorColumns);
            grid.Children.Add(colorButton);
        }

        colorScrollViewer.Content = grid;
    }

    private static Color TryParseColor(string input, string fallback)
    {
        try
        {
            return Color.Parse(input);
        }
        catch
        {
            return Color.Parse(fallback);
        }
    }

    public static Control CreateIconContent(CategoryIconOptionViewModel option, double size)
    {
        var sampleImage = CategoryIconCatalog.GetSampleImage(option.Key);
        if (sampleImage is not null)
        {
            return new Image
            {
                Source = sampleImage,
                Width = size,
                Height = size,
                Stretch = Stretch.Uniform
            };
        }

        return new TextBlock
        {
            Text = option.Glyph,
            FontFamily = new FontFamily("avares://KeepWalletDesktop/Assets/Fonts/NotoColorEmoji.ttf#Noto Color Emoji"),
            FontSize = 20,
            TextAlignment = Avalonia.Media.TextAlignment.Center
        };
    }
}
