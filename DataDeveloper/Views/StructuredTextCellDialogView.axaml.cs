using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.VisualTree;
using AvaloniaEdit.Folding;
using AvaloniaEdit.Highlighting;
using DataDeveloper.NextGrid.Renderers;
using DataDeveloper.Services;
using DataDeveloper.ViewModels;
using ReactiveUI;

namespace DataDeveloper.Views;

public partial class StructuredTextCellDialogView : UserControl
{
    private readonly JsonFoldingStrategy _jsonFoldingStrategy = new();
    private readonly XmlFoldingStrategy _xmlFoldingStrategy = new();
    private readonly FoldingManager _foldingManager;
    private StructuredTextKind _currentKind;

    public StructuredTextCellDialogView()
    {
        InitializeComponent();

        Editor.Bind(TextEditorBindingHelper.BindableTextProperty, new Binding(nameof(StructuredTextCellDialogViewModel.Text)) { Mode = BindingMode.TwoWay });
        Editor.TemplateApplied += (_, _) =>
        {
            var scrollViewer = Editor.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
            if (scrollViewer is not null)
                scrollViewer.AllowAutoHide = false;
        };

        _foldingManager = FoldingManager.Install(Editor.TextArea);
        Editor.TextChanged += (_, _) => UpdateFoldings();

        // Kind can change after the dialog opens (e.g. it opens in plain-text mode for a
        // previously empty cell, then the user pastes a recognizable JSON/XML value); react to
        // that instead of only applying it once when the dialog is constructed.
        DataContextChanged += (_, _) =>
        {
            if (DataContext is StructuredTextCellDialogViewModel viewModel)
                viewModel.WhenAnyValue(x => x.Kind).Subscribe(ApplyKind);
            else
                ApplyKind(StructuredTextKind.None);
        };
    }

    private void ApplyKind(StructuredTextKind kind)
    {
        _currentKind = kind;
        Editor.SyntaxHighlighting = kind switch
        {
            StructuredTextKind.Xml => HighlightingManager.Instance.GetDefinition("XML"),
            StructuredTextKind.Json => HighlightingManager.Instance.GetDefinition("JSON"),
            _ => null
        };
        UpdateFoldings();
    }

    private void UpdateFoldings()
    {
        switch (_currentKind)
        {
            case StructuredTextKind.Xml:
                _xmlFoldingStrategy.UpdateFoldings(_foldingManager, Editor.Document);
                break;
            case StructuredTextKind.Json:
                _jsonFoldingStrategy.UpdateFoldings(_foldingManager, Editor.Document);
                break;
            default:
                _foldingManager.Clear();
                break;
        }
    }
}
