using System;
using System.ComponentModel;
using DataDeveloper.Events;
using Dock.Model.Core;
using Dock.Model.ReactiveUI.Controls;
using ReactiveUI;

namespace DataDeveloper.ViewModels;

public class MessageViewModel : Tool
{
    private readonly EditorDocumentViewModel _documentViewModel;
    private string _test = String.Empty;

    public MessageViewModel(IFactory factory, EditorDocumentViewModel documentViewModel) 
    {
        Factory = factory;
        _documentViewModel = documentViewModel;
        _documentViewModel.ShowMessage += DocumentViewModelOnShowMessage;
    }

    private void DocumentViewModelOnShowMessage(object? sender, ShowMessageEventArgs e)
    {
        if (e.Focus)
            this.Factory?.SetActiveDockable(this);
        
        this.Test = e.MessageToShow;
    }

    public string Test
    {
        get => _test;
        set => this.RaiseAndSetIfChanged(ref _test, value);
    }
}