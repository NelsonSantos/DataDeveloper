using System;
using DataDeveloper.Data.Enums;
using DataDeveloper.DataGrid;

namespace DataDeveloper.Models;

public class ColumnHeader
{
    public string Name { get; set; }
    public Type Type { get; set; }
    public ColumnAlignment  Alignment { get; set; }
}