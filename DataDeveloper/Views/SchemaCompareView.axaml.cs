using System;
using Avalonia.Controls;
using Avalonia.Data;

namespace DataDeveloper.Views;

public partial class SchemaCompareView : UserControl
{
    public SchemaCompareView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        ResultScriptPreview.Bind(TextEditorBindingHelper.BindableTextProperty, new Binding("SelectedResultRow.Script"));
        FinalScriptEditor.Bind(TextEditorBindingHelper.BindableTextProperty, new Binding("GeneratedScript"));
    }
}
