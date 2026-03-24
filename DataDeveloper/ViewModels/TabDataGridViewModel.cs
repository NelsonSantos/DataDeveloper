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
    public TabDataGridViewModel(StatementResult statementResult, int selectedPage, string name, bool canClose, IServiceProvider serviceProvider) 
        : base(TabType.DataGrid, name, canClose, serviceProvider)
    {
        StatementResult = statementResult;

        SelectedPage = selectedPage;
        LoadNextPageCommand = ReactiveCommand.CreateFromTask(() => LoadNextPage(SelectedPage)
            , outputScheduler: RxApp.MainThreadScheduler
            , canExecute: this.WhenAnyValue(x => x.IsClosed).Select(_ => _ is false));
        LoadAllRecordsCommand = ReactiveCommand.CreateFromTask(() => LoadNextPage()
            , outputScheduler: RxApp.MainThreadScheduler
            , canExecute: this.WhenAnyValue(x => x.IsClosed).Select(_ => _ is false));
    }

    public async Task CloseDataReader()
    {
        await StatementResult.CloseDataReader();
        this.IsClosed = true;
    }

    public async Task LoadData()
    {
        Headers.Clear();
        var columns = new List<ColumnHeader>();
        for (int i = 0; i < StatementResult.DataReader.FieldCount; i++)
        {
            var columnHeader = new ColumnHeader()
            {
                Name = StatementResult.DataReader.GetName(i),
                Type = StatementResult.DataReader.GetFieldType(i),
                Alignment = GetFieldAlignment(StatementResult.DataReader.GetFieldType(i))
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
        StatementResult.Watcher.Start();
        this.IsBusy = true;
        await Task.Delay(100);
        var readedUntilEnd = true;
        while (StatementResult.DataReader.Read())
        {
            RowNumber++;
            countRecords++;
            
            var values = new object[StatementResult.DataReader.FieldCount];
            StatementResult.DataReader.GetValues(values);
            var row = new RowValues(RowNumber, values);
            this.Rows.Add(row);
            
            if (itemsPerPage > 0 && countRecords == itemsPerPage)
            {
                readedUntilEnd = false;
                break;
            }
        }

        if (readedUntilEnd)
            await this.CloseDataReader();
        StatementResult.Watcher.Stop();
        this.TimeElapsed = StatementResult.Watcher.Elapsed;
        this.IsBusy = false;
    }

    public ColumnAlignment GetFieldAlignment(Type fieldType)
    {
        return fieldType.FullName?.ToLowerInvariant() switch
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

    public StatementResult StatementResult { get; }
    [Reactive] public TimeSpan TimeElapsed { get; set; }
    [Reactive] public bool IsClosed { get; set; }
    [Reactive] public int RowNumber { get; set; }
    [Reactive] public int SelectedPage { get; set; }
    public ObservableCollection<RowValues> Rows { get; } = new();
    public ObservableCollection<ColumnHeader> Headers { get; } = new();
    public ObservableCollection<int> Pages { get; } = new() { 100, 200, 500, 1000, 2000, 5000, 10000, 20000, 50000, 100000, };
    public ReactiveCommand<Unit, Unit> LoadNextPageCommand { get; }
    public ReactiveCommand<Unit, Unit> LoadAllRecordsCommand { get; }
}
