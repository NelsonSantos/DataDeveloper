using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Dynamic;
using DataDeveloper.Models;
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

    public ObservableCollection<RowValues> Rows { get; } = new();
    public ObservableCollection<string> Headers { get; } = new();
    
    private void DocumentOnRowAdded(object? sender, RowValues e)
    {
        Rows.Add(e);
    }

    private void DocumentOnRowClear(object? sender, EventArgs e)
    {
        Rows.Clear();
    }

    private void DocumentOnColunmsClear(object? sender, EventArgs e)
    {
        Headers.Clear();
    }

    private void DocumentOnColunmsChanged(object? sender, string[] e)
    {
        Headers.AddRange(e);
    }
}