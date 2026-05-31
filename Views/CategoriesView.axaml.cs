using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using KeepWalletDesktop.ViewModels;
using KeepWalletDesktop.Views.Modules;

namespace KeepWalletDesktop.Views;

public partial class CategoriesView : UserControl
{
    private readonly IconPickerModule iconPicker = new();

    public CategoriesView()
    {
        InitializeComponent();
    }

    private void NewCategoryIconButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || DataContext is not MainViewModel viewModel)
        {
            return;
        }

        iconPicker.Show(
            button,
            viewModel.CategoryIconOptions,
            viewModel.CategoryColorOptions,
            iconKey => viewModel.CategoryNewIconKey = iconKey,
            color => viewModel.CategoryNewColor = color,
            viewModel.Texts.IconSearchHint,
            circularIcons: false);
    }

    private void CategoryIconButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: CategoryManagementItemViewModel category } button ||
            DataContext is not MainViewModel viewModel)
        {
            return;
        }

        iconPicker.Show(
            button,
            viewModel.CategoryIconOptions,
            viewModel.CategoryColorOptions,
            iconKey =>
            {
                category.SelectedIconKey = iconKey;
                SaveCategoriesImmediately(viewModel);
            },
            color =>
            {
                category.SelectedColor = color;
                SaveCategoriesImmediately(viewModel);
            },
            viewModel.Texts.IconSearchHint,
            circularIcons: false);
    }

    private void CategorySelection_OnChanged(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            SaveCategoriesImmediately(viewModel);
        }
    }

    private static void SaveCategoriesImmediately(MainViewModel viewModel)
    {
        if (viewModel.SaveCategoriesCommand.CanExecute(null))
        {
            viewModel.SaveCategoriesCommand.Execute(null);
        }
    }

    private void CategoryName_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Control { DataContext: CategoryManagementItemViewModel category } ||
            DataContext is not MainViewModel viewModel ||
            !viewModel.EditCategoryNameCommand.CanExecute(category))
        {
            return;
        }

        viewModel.EditCategoryNameCommand.Execute(category);
    }

    private void CategoryNameTextBox_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        FinishCategoryNameEdit(sender);
    }

    private void CategoryNameTextBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            FinishCategoryNameEdit(sender);
            e.Handled = true;
        }
    }

    private void FinishCategoryNameEdit(object? sender)
    {
        if (sender is not Control { DataContext: CategoryManagementItemViewModel category } ||
            DataContext is not MainViewModel viewModel ||
            !viewModel.FinishCategoryNameEditCommand.CanExecute(category))
        {
            return;
        }

        viewModel.FinishCategoryNameEditCommand.Execute(category);
    }
}
