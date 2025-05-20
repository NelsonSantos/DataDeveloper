using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Dock.Model.ReactiveUI.Controls;
using Dock.Model.ReactiveUI.Core;
using Dock.Model.ReactiveUI;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using ReactiveUI;
using System.Reactive.Linq;
using System;
using System.Reactive;

namespace DataDeveloper.ViewModels;

public class SqlEditorViewModel : Document
{
    private string _queryText = "select top 100 * from Test1";
    private bool _textWasChanged = false;

    public string QueryText
    {
        get => _queryText;
        set
        {
            TextWasChanged = _queryText != value;
            this.RaiseAndSetIfChanged(ref _queryText, value);
        }
    }

    public bool TextWasChanged
    {
        get => _textWasChanged;
        set => this.RaiseAndSetIfChanged(ref _textWasChanged, value);
    }

    public override bool OnClose()
    {
        return !TextWasChanged;
    }

    public SqlEditorViewModel()
    {
    }
}