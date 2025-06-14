using System;
using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using DataDeveloper.Models;
using DataDeveloper.TemplateSelectors;
using DataDeveloper.ViewModels;

namespace DataDeveloper.Views;

public partial class TabQueryEditorView : UserControl
{
    private GridLength _previousTabHeight = new GridLength(200); // altura padrão
    private TabQueryEditorViewModel _viewModel;
    private TabTemplateSelector _templateSelector;
    public TabQueryEditorView()
    {
        InitializeComponent();
        this.Loaded += OnLoaded;
        _templateSelector = this.Resources["TabContentTemplate"] as TabTemplateSelector;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        _viewModel = DataContext as TabQueryEditorViewModel;
        SqlEditor.Bind(TextEditorBindingHelper.BindableTextProperty, new Binding(nameof(_viewModel.SqlStatement)) { Mode = BindingMode.TwoWay });
        SqlEditor.Bind(TextEditorBindingHelper.BindableSelectedTextProperty, new Binding(nameof(_viewModel.SelectedStatement)));
        SqlEditor.Bind(TextEditorBindingHelper.BindableCaretOffsetProperty, new Binding(nameof(_viewModel.CursorOffSet)));
        SqlEditor.Bind(TextEditorBindingHelper.BindableCaretLineProperty, new Binding(nameof(_viewModel.CursorLine)));
        SqlEditor.Bind(TextEditorBindingHelper.BindableCaretColumnProperty, new Binding(nameof(_viewModel.CursorColumn)));
    }
    
    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _viewModel.EditorHeadHeight = StackPanelEditor.Bounds.Height;
        _viewModel.ResultsHeaderHeight = StackPanelResult.Bounds.Height;
        _viewModel.ShowResultTool += ViewModelOnShowResultTool;
        _viewModel.Tabs.CollectionChanged += TabsOnCollectionChanged;
        this.ToggleTabs();
    }

    private void TabsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Remove || e.Action == NotifyCollectionChangedAction.Replace || e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (var item in e.OldItems)
            {
                _templateSelector?.RemoveControl(item as BaseTabContent);
            }
        }
    }

    private void ViewModelOnShowResultTool(object? sender, int e)
    {
        ToggleTabs();
    }

    private void ToggleTabs_Click(object? sender, RoutedEventArgs e)
    {
        _viewModel.ResultIsMinimized = !_viewModel.ResultIsMinimized;
        ToggleTabs();
    }

    private void ToggleTabs()
    {
        var tabRow = RootGrid.RowDefinitions[3];

        if (!_viewModel.ResultIsMinimized)
        {
            tabRow.Height = _previousTabHeight;
            Splitter.IsVisible = true;
            _viewModel.ResultIsMinimized = false;
        }
        else
        {
            _previousTabHeight = tabRow.Height;
            tabRow.Height = new GridLength(StackPanelResult.Bounds.Height);
            Splitter.IsVisible = false;
            _viewModel.ResultIsMinimized = true;
        }
    }        
}