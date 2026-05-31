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
    private const int IconPickerPageSize = 150;
    private const int IconPickerBatchSize = 25;
    private const double IconPickerLoadMoreThreshold = 48;

    private readonly Flyout iconFlyout;
    private readonly TextBox iconSearchBox;
    private readonly ScrollViewer iconScrollViewer;
    private readonly ScrollViewer colorScrollViewer;
    private readonly TabControl pickerTabs;
    private readonly Border iconTabHeader;
    private readonly Border colorTabHeader;
    private readonly DispatcherTimer searchDebounce;
    private IReadOnlyList<CategoryIconOptionViewModel> pickerOptions = [];
    private List<CategoryIconOptionViewModel> currentFilteredOptions = [];
    private Grid? currentIconGrid;
    private IReadOnlyList<string> colorOptions = [];
    private Action<string>? currentSelectIcon;
    private Action<string>? currentSelectColor;
    private int currentIconGridChildCount;
    private bool isIconLoadMoreQueued;
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
        searchDebounce = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        searchDebounce.Tick += (_, _) =>
        {
            searchDebounce.Stop();
            RebuildIconGrid(iconSearchBox.Text ?? string.Empty);
        };
        iconSearchBox.TextChanged += (_, _) =>
        {
            searchDebounce.Stop();
            searchDebounce.Start();
        };
        iconScrollViewer.ScrollChanged += IconScrollViewer_OnScrollChanged;
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
        currentIconGrid = null;
        currentIconGridChildCount = 0;
        isIconLoadMoreQueued = false;
        currentFilteredOptions = pickerOptions
            .Where(option => option.Matches(searchText))
            .ToList();

        var panel = new StackPanel
        {
            Width = IconPickerWidth
        };
        currentIconGrid = CreateEmptyIconGrid();
        panel.Children.Add(currentIconGrid);

        iconScrollViewer.Content = panel;

        var to = Math.Min(IconPickerPageSize, currentFilteredOptions.Count);
        AppendIconsBatch(currentIconGrid, 0, to, IconPickerBatchSize);
        currentIconGridChildCount = to;
    }

    private void IconScrollViewer_OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer ||
            currentIconGrid is null ||
            currentIconGridChildCount >= currentFilteredOptions.Count)
        {
            return;
        }

        var remaining = scrollViewer.Extent.Height - scrollViewer.Viewport.Height - scrollViewer.Offset.Y;
        if (remaining <= IconPickerLoadMoreThreshold)
        {
            QueueAppendNextIconPage();
        }
    }

    private void QueueAppendNextIconPage()
    {
        if (isIconLoadMoreQueued)
        {
            return;
        }

        isIconLoadMoreQueued = true;
        var gridSnapshot = currentIconGrid;
        Dispatcher.UIThread.Post(() =>
        {
            isIconLoadMoreQueued = false;
            if (gridSnapshot != currentIconGrid ||
                !IsIconScrollNearBottom())
            {
                return;
            }

            AppendNextIconPage();
        }, DispatcherPriority.Background);
    }

    private bool IsIconScrollNearBottom()
    {
        if (currentIconGrid is null ||
            currentIconGridChildCount >= currentFilteredOptions.Count)
        {
            return false;
        }

        var remaining = iconScrollViewer.Extent.Height - iconScrollViewer.Viewport.Height - iconScrollViewer.Offset.Y;
        return remaining <= IconPickerLoadMoreThreshold;
    }

    private void AppendNextIconPage()
    {
        if (currentIconGrid is null)
        {
            return;
        }

        var grid = currentIconGrid;
        var from = currentIconGridChildCount;
        if (from >= currentFilteredOptions.Count)
        {
            return;
        }

        var to = Math.Min(from + IconPickerPageSize, currentFilteredOptions.Count);
        var newRowCount = Math.Max(1, (int)Math.Ceiling(to / (double)IconPickerColumns));
        while (grid.RowDefinitions.Count < newRowCount)
        {
            grid.RowDefinitions.Add(new RowDefinition(IconPickerCellSize, GridUnitType.Pixel));
        }

        AppendIconsBatch(grid, from, to, IconPickerBatchSize);
        currentIconGridChildCount = to;
    }

    private Grid CreateEmptyIconGrid()
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

        var rowCount = Math.Max(1, (int)Math.Ceiling(
            Math.Min(IconPickerPageSize, currentFilteredOptions.Count) / (double)IconPickerColumns));
        for (var row = 0; row < rowCount; row++)
        {
            grid.RowDefinitions.Add(new RowDefinition(IconPickerCellSize, GridUnitType.Pixel));
        }

        return grid;
    }

    private void AppendIconsBatch(Grid grid, int from, int to, int batchSize)
    {
        if (grid != currentIconGrid)
        {
            return;
        }

        var batchEnd = Math.Min(from + batchSize, to);
        for (var index = from; index < batchEnd; index++)
        {
            var option = currentFilteredOptions[index];
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

        if (batchEnd < to)
        {
            Dispatcher.UIThread.Post(
                () => AppendIconsBatch(grid, batchEnd, to, batchSize),
                DispatcherPriority.Background);
        }
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
