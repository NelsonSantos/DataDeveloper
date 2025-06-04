using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Dynamic;
using DataDeveloper.Models;
using Dock.Model.Core;
using Dock.Model.ReactiveUI.Controls;
using DynamicData;

namespace DataDeveloper.ViewModels;

public class ResultViewModel : Tool
{
    private readonly EditorDocumentViewModel _document;

    public ResultViewModel(IFactory factory, EditorDocumentViewModel document)
    {
        Factory = factory;
        _document = document;
        //_document.ColumnsClear += DocumentOnColumnsClear;
        //_document.ColumnsChanged += DocumentOnColumnsChanged;
        //_document.RowClear += DocumentOnRowClear;
        //_document.RowAdded += DocumentOnRowAdded;
        //_document.ShowResultTool += DocumentOnShowResultTool;
    }

    private void DocumentOnShowResultTool(object? sender, int e)
    {
        this.Factory?.SetActiveDockable(this);
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

    private void DocumentOnColumnsClear(object? sender, EventArgs e)
    {
        Headers.Clear();
    }

    private void DocumentOnColumnsChanged(object? sender, string[] e)
    {
        Headers.AddRange(e);
    }
}