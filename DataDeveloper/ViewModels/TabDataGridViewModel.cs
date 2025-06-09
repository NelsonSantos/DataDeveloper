using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using DataDeveloper.Data.Models;
using DataDeveloper.Enums;
using DataDeveloper.Models;
using DynamicData;

namespace DataDeveloper.ViewModels;

public class TabDataGridViewModel : BaseTabContent
{
    private readonly StatementResult _statementResult;
    
    // public event EventHandler RowClear; 
    // public event EventHandler<RowValues> RowAdded; 
    //public event EventHandler ColumnsClear;
    //public event EventHandler<string[]> ColumnsChanged;
    
    public TabDataGridViewModel(StatementResult statementResult, string name, bool canClose) 
        : base(TabType.DataGrid, name, canClose)
    {
        _statementResult = statementResult;
    }

    public async Task CloseDataReader()
    {
        await _statementResult.CloseDataReader();
        this.IsClosed = true;
    }

    public Task LoadData()
    {
        Headers.Clear();
        var columns = new List<string>();
        for (int i = 0; i < _statementResult.DataReader.FieldCount; i++)
        {
            columns.Add(_statementResult.DataReader.GetName(i));
        }

        Headers.Add(columns);
        
        this.Rows.Clear();
        var rowNumber = 0;
        while (_statementResult.DataReader.Read())
        {
            rowNumber++;
            var values = new object?[_statementResult.DataReader.FieldCount];
            _statementResult.DataReader.GetValues(values);
            this.Rows.Add(new RowValues(rowNumber, values));
        }

        return Task.CompletedTask;
    }

    public bool IsClosed { get; private set; }
    public ObservableCollection<RowValues> Rows { get; } = new();
    public ObservableCollection<string> Headers { get; } = new();
}