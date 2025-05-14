using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using DataDeveloper.ViewModels;

// using System;
// using Microsoft.Data.SqlClient;
// using System.Collections.Generic;
// using System.Data;
// using System.Linq;
// using Avalonia.Controls;
// using Avalonia.Controls.Templates;
// using DataDeveloper.ViewModels;
//
namespace DataDeveloper;
//
// public static class QueryExecutor
// {
//     public static DataTable ExecuteQuery(SqlConnectionInfo connectionInfo, string sql)
//     {
//         using var conn = new SqlConnection(connectionInfo.ConnectionString);
//         conn.Open();
//
//         using var cmd = new SqlCommand(sql, conn);
//         using var adapter = new SqlDataAdapter(cmd);
//         var table = new DataTable();
//         adapter.Fill(table);
//
//         return table;
//     }
//
//     public static List<string> GetTableNames(SqlConnectionInfo connectionInfo)
//     {
//         var list = new List<string>();
//
//         using var conn = new SqlConnection(connectionInfo.ConnectionString);
//         conn.Open();
//
//         using var cmd = new SqlCommand("SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'", conn);
//         using var reader = cmd.ExecuteReader();
//
//         while (reader.Read())
//             list.Add(reader.GetString(0));
//
//         return list;
//     }
// }
//


public class ViewLocator : IDataTemplate
{
    public Control Build(object data)
    {
        var name = data.GetType().FullName!.Replace("ViewModel", "View");
        var type = Type.GetType(name);

        if (type != null)
        {
            return (Control)Activator.CreateInstance(type)!;
        }

        return new TextBlock { Text = "Not Found: " + name };
    }

    public bool Match(object data)
    {
        return data is ViewModelBase;
    }
}