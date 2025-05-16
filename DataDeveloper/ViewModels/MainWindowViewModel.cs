using Dock.Model.ReactiveUI.Controls;
using Dock.Model.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Reactive;
using System.Reflection.Metadata;
using Avalonia.Controls;
using Dapper;
using Dock.Model.Controls;
using Dock.Model.ReactiveUI;
using Microsoft.Data.SqlClient;
using ReactiveUI;
using Document = Dock.Model.ReactiveUI.Controls.Document;

namespace DataDeveloper.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    
    public IDockable Layout { get; set; }
    
    private string _queryText = "select top 100 * from Test1";
    public string QueryText
    {
        get => _queryText;
        set => this.RaiseAndSetIfChanged(ref _queryText, value);
    }

    // private DataView? _resultView;
    // public DataView? ResultView
    // {
    //     get =>  _resultView;
    //     set => this.RaiseAndSetIfChanged(ref _resultView, value);
    // }

    private ObservableCollection<RecordManager> _resultView = new();
    public ObservableCollection<RecordManager> ResultView
    {
        get => _resultView;
        set => this.RaiseAndSetIfChanged(ref _resultView, value);
    }

    public ObservableCollection<string> TableNames { get; } = new();
    
    public ReactiveCommand<Unit, Unit> ExecuteCommand { get; }
    
    private SqlConnectionInfo _connection;

    public MainWindowViewModel()
    {
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
        _connection = new SqlConnectionInfo()
        {
            Database = "test-data-developer",
            Password = "Nass5544@",
            Server = "192.168.68.132",
            User = "sa",
            // Database = "NXGenMarketplace_Payment_Development",
            // Password = "123mudar",
            // Server = "192.168.239.48",
            // User = "user_app",
        };
        LoadTables();

        ExecuteCommand = ReactiveCommand.Create(ExecuteQuery);

        CreateLayout();
    }
    
    private void CreateLayout()
    {
        var factory = new DockFactory();
        Layout = factory.CreateLayout();
        if (Layout is { })
        {
            factory.InitLayout(Layout);
        }
    }    
    
    private void LoadTables()
    {
        try
        {
            using var conn = new SqlConnection(_connection.ConnectionString);
            conn.Open();
            using var cmd = new SqlCommand("SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE'", conn);
            using var reader = cmd.ExecuteReader();
    
            TableNames.Clear();
            while (reader.Read())
                TableNames.Add(reader.GetString(0));
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro ao carregar tabelas: " + ex.Message);
        }
    }

    public ObservableCollection<IEnumerable<object>> MyData { get; } = new() { new[] {"A", "B"}, new[] {"C", "D"} };
    public ObservableCollection<string> MyHeaders { get; } = new()  { "Field1", "Field2" };
    
    private void ExecuteQuery()
    {
        try
        {
            using var conn = new SqlConnection(_connection.ConnectionString);
            conn.Open();

            var data  = conn.ExecuteReader(QueryText, commandType: CommandType.Text);

            MyHeaders.Clear();
            
            for (int i = 0; i < data.FieldCount; i++)
            {
                MyHeaders.Add(data.GetName(i));
            }
            
            MyData.Clear();
            while (data.Read())
            {
                var values = new object[data.FieldCount];
                var count = data.GetValues(values);
                MyData.Add(values);
            }
            
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro ao executar query: " + ex.Message);
        }
    }
}

public class SqlEditorViewModel : Document
{
}

public class ConnectionDataViewModel : Tool
{
}

public class ResultViewModel : Tool
{
}

public class DockFactory : Factory
{
    public override IRootDock CreateLayout()
    {
        var document = new SqlEditorViewModel
        {
            Id = "SqlEditor",
            Title = "Editor SQL AAAAAA",
        };

        var treeTool = new ConnectionDataViewModel
        {
            Id = "Tables",
            Title = "Tabelas",
            CanClose = false,
            CanFloat = false,
        };

        var outputTool = new ResultViewModel
        {
            Id = "Output",
            Title = "Resultados",
            CanClose = false,
            CanFloat = false,
        };

        var documentDock = new DocumentDock
        {
            Id = "Documents",
            Title = "Documentos",
            ActiveDockable = document,
            VisibleDockables = CreateList<IDockable>( document ),
        };

        var left = new ToolDock
        {
            Id = "Left",
            Title = "Left",
            ActiveDockable = treeTool,
            VisibleDockables = CreateList<IDockable>( treeTool ),
            Alignment = Alignment.Left,
            CanClose = false,
            CanFloat = false,
            Proportion = 0.25,
        };

        var bottom = new ToolDock
        {
            Id = "Bottom",
            Title = "Bottom",
            ActiveDockable = outputTool,
            VisibleDockables = CreateList<IDockable>( outputTool ),
            Alignment = Alignment.Bottom,
            CanClose = false,
            CanFloat = false,
            Proportion = 0.25, 
        };

        var windowLayoutContent = new ProportionalDock
        {
            Orientation = Orientation.Vertical,
            CanClose = false,
            VisibleDockables = CreateList<IDockable>(
                new ProportionalDock
                {
                    Orientation = Orientation.Horizontal,
                    VisibleDockables = new List<IDockable> { left, new ProportionalDockSplitter(), documentDock }
                },
                new ProportionalDockSplitter(),
                bottom
            ),
        };

        var rootDock = CreateRootDock();
        
        rootDock.Id = "Root";
        rootDock.Title = "Root";
        rootDock.ActiveDockable = windowLayoutContent;
        rootDock.DefaultDockable = windowLayoutContent;
        rootDock.VisibleDockables = CreateList<IDockable>(windowLayoutContent);

        return rootDock;
    }
}

public class RecordManager
{
    private int _index = -1;
    private readonly object[] _values;
    public RecordManager(object[] values)
    {
        _values = values;
    }
    public object GetValue()
    {
        _index++;
        return _values[_index];
    }
}