using System;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DataDeveloper.Models;
using DataDeveloper.TemplateSelectors;
using DataDeveloper.ViewModels;

namespace DataDeveloper.Views;

public partial class QueryEditorView : UserControl
{
    private GridLength _previousTabHeight = new GridLength(200); // altura padrão
    private EditorDocumentViewModel _viewModel;
    private TabContentTemplateSelector _templateSelector;
    public QueryEditorView()
    {
        InitializeComponent();
        this.Loaded += OnLoaded;
        _templateSelector = this.Resources["TabContentTemplate"] as TabContentTemplateSelector;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        _viewModel = DataContext as EditorDocumentViewModel;
        base.OnDataContextChanged(e);
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
                _templateSelector.RemoveControl(item as TabResult);
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