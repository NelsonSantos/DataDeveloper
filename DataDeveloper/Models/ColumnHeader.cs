using System;
using DataDeveloper.Data.Enums;
using DataDeveloper.DataGrid;

namespace DataDeveloper.Models;

public class ColumnHeader
{
    public string Name { get; set; } = string.Empty;
    public Type Type { get; set; } = typeof(object);
    public ColumnAlignment  Alignment { get; set; }
}
