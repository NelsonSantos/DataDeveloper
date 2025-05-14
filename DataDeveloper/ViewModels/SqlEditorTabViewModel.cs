//
// using System.Data;
// using DataDeveloper;
// using ReactiveUI;
//
// public class SqlEditorTabViewModel : ReactiveObject
// {
//     private string _queryText;
//     public string QueryText
//     {
//         get => _queryText;
//         set => this.RaiseAndSetIfChanged(ref _queryText, value);
//     }
//
//     private DataTable _resultTable;
//     public DataTable ResultTable
//     {
//         get => _resultTable;
//         set => this.RaiseAndSetIfChanged(ref _resultTable, value);
//     }
//
//     public SqlConnectionInfo ConnectionInfo { get; set; }
//
//     public void ExecuteQuery()
//     {
//         if (!string.IsNullOrWhiteSpace(QueryText))
//         {
//             ResultTable = QueryExecutor.ExecuteQuery(ConnectionInfo, QueryText);
//         }
//     }
// }
