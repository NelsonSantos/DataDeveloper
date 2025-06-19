using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Models;
using DataDeveloper.DataGrid;
using DataDeveloper.Enums;
using DataDeveloper.Models;
using DynamicData;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace DataDeveloper.ViewModels;

public class TabDataGridViewModel : BaseTabContent
{
    private readonly StatementResult _statementResult;
    
    public TabDataGridViewModel(StatementResult statementResult, int selectedPage, string name, bool canClose, IServiceProvider serviceProvider) 
        : base(TabType.DataGrid, name, canClose, serviceProvider)
    {
        _statementResult = statementResult;

        SelectedPage = selectedPage;
        LoadNextPageCommand = ReactiveCommand.CreateFromTask(() => LoadNextPage(SelectedPage),
            canExecute: this.WhenAnyValue(x => x.IsClosed).Select(_ => _ is false));
        LoadAllRecordsCommand = ReactiveCommand.CreateFromTask(() => LoadNextPage(),
            canExecute: this.WhenAnyValue(x => x.IsClosed).Select(_ => _ is false));
    }

    public async Task CloseDataReader()
    {
        await _statementResult.CloseDataReader();
        this.IsClosed = true;
    }

    public async Task LoadData()
    {
        Headers.Clear();
        var columns = new List<ColumnHeader>();
        for (int i = 0; i < _statementResult.DataReader.FieldCount; i++)
        {
            var columnHeader = new ColumnHeader()
            {
                Name = _statementResult.DataReader.GetName(i),
                Type = _statementResult.DataReader.GetFieldType(i),
                Alignment = GetFieldAlignment(_statementResult.DataReader.GetFieldType(i))
            };
            columns.Add(columnHeader);
        }

        Headers.Add(columns);
        
        this.Rows.Clear();
        await LoadNextPage(SelectedPage);
        
    }

    private async Task LoadNextPage(int itemsPerPage = 0)
    {
        var countRecords = 0;
        while (_statementResult.DataReader.Read())
        {
            RowNumber++;
            countRecords++;
            
            var values = new object?[_statementResult.DataReader.FieldCount];
            _statementResult.DataReader.GetValues(values);
            this.Rows.Add(new RowValues(RowNumber, values));
            
            if (itemsPerPage > 0 && countRecords == itemsPerPage) return;
        }

        await this.CloseDataReader();
    }

    public ColumnAlignment GetFieldAlignment(Type fieldType)
    {
        return fieldType.FullName.ToLower() switch
        {
            "system.boolean" or
            "system.byte" or
            "system.sbyte" => ColumnAlignment.Center,

            "system.int16" or 
            "system.int32" or 
            "system.int64" or 
            "system.uint16" or 
            "system.uint32" or
            "system.double" or
            "system.decimal" => ColumnAlignment.Far,
            
            "system.string" or "system.char" or "system.guid" => ColumnAlignment.Near,
            
            "system.datetime" or
            "system.datetimeoffset" or
            "system.timespan" => ColumnAlignment.Center,
            
            "system.byte[]" or "system.dbnull" or "system.object" or "system.xml.xmldocument" or _ => ColumnAlignment.Near,
        };
    }

    [Reactive] public bool IsClosed { get; set; }
    [Reactive] public int RowNumber { get; set; }
    [Reactive] public int SelectedPage { get; set; }
    public ObservableCollection<RowValues> Rows { get; } = new();
    public ObservableCollection<ColumnHeader> Headers { get; } = new();
    public ObservableCollection<int> Pages { get; } = new() { 100, 200, 500, 1000, 2000, 5000, 10000, 20000, 50000, 100000, };
    public ReactiveCommand<Unit, Unit> LoadNextPageCommand { get; }
    public ReactiveCommand<Unit, Unit> LoadAllRecordsCommand { get; }
}