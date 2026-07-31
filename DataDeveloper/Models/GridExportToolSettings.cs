using System;
using System.Collections.Generic;
using DataDeveloper.Enums;

namespace DataDeveloper.Models;

public class GridExportToolSettings
{
    public Dictionary<Guid, GridExportFormat> PreferredFormatByConnectionId { get; set; } = new();
}
