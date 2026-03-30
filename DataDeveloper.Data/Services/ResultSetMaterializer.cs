using System.Data;
using System.Data.Common;

namespace DataDeveloper.Data.Services;

public static class ResultSetMaterializer
{
    public static DataTable MaterializeCurrentResult(DbDataReader reader)
    {
        var table = new DataTable();

        for (var i = 0; i < reader.FieldCount; i++)
        {
            var columnType = reader.GetFieldType(i);
            table.Columns.Add(reader.GetName(i), Nullable.GetUnderlyingType(columnType) ?? columnType);
        }

        while (reader.Read())
        {
            var values = new object[reader.FieldCount];
            reader.GetValues(values);
            table.Rows.Add(values);
        }

        return table;
    }
}
