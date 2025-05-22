using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Dock.Model.ReactiveUI.Controls;
using DynamicData;

namespace DataDeveloper.ViewModels;

public class ResultViewModel : Tool
{
    private readonly EditorDocumentViewModel _document;

    public ResultViewModel(EditorDocumentViewModel document)
    {
        _document = document;
        _document.ColunmsClear += DocumentOnColunmsClear;
        _document.ColunmsChanged += DocumentOnColunmsChanged;
        _document.RowClear += DocumentOnRowClear;
        _document.RowAdded += DocumentOnRowAdded;
    }

    public ObservableCollection<IEnumerable<object>> MyData { get; } = new() { new[] {"A", "B"}, new[] {"C", "D"} };
    public ObservableCollection<string> MyHeaders { get; } = new()  { "Field1", "Field2" };
    
    private void DocumentOnRowAdded(object? sender, object[] e)
    {
        MyData.Add(e);
    }

    private void DocumentOnRowClear(object? sender, EventArgs e)
    {
        MyData.Clear();
    }

    private void DocumentOnColunmsClear(object? sender, EventArgs e)
    {
        MyHeaders.Clear();
    }

    private void DocumentOnColunmsChanged(object? sender, string[] e)
    {
        MyHeaders.AddRange(e);
    }
}