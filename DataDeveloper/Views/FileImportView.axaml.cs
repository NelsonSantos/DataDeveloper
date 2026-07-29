using System;
using Avalonia.Controls;
using Avalonia.Data;

namespace DataDeveloper.Views;

public partial class FileImportView : UserControl
{
    public FileImportView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        CreateScriptPreview.Bind(TextEditorBindingHelper.BindableTextProperty, new Binding("GeneratedCreateScript"));
    }
}
