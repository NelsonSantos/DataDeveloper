using Dock.Model.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Reactive;
using System.Reflection.Metadata;
using System.Windows.Input;
using Avalonia.Controls;
using Dapper;
using DataDeveloper.Views;
using Dock.Model.Controls;
using Microsoft.Data.SqlClient;
using ReactiveUI;

namespace DataDeveloper.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private IRootDock? _layout;
    private readonly IFactory? factory; 
    public IRootDock? Layout
    {
        get => _layout;
        set => this.RaiseAndSetIfChanged(ref _layout, value);
    }
    
    public MainWindowViewModel()
    {
        factory = new DockFactory();
        Layout = factory.CreateLayout();
        if (Layout is { } root)
        {
            factory.InitLayout(Layout);
        }
        // Mostrar diálogo de conexão ao iniciar
        // var dialog = new ConnectionDialogViewModel();
        // dialog.OpenDialog();
        //
        // if (dialog.ConnectionInfo == null)
        // {
        //     Environment.Exit(0);
        //     return;
        // }
    
        //_connection = dialog.ConnectionInfo;

        this.NewWindowCommand = ReactiveCommand.Create(() =>
        {
            var newWindow = new MainWindow();
            newWindow.Show();
        });
    }
    
    public ICommand NewWindowCommand { get; }

}
