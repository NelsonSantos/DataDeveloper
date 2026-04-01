using System;
using DataDeveloper.Data.Services;

namespace DataDeveloper.EventAggregators;

public record RefreshSchemaExplorerEvent(Guid ConnectionId, string? Statement = null, SchemaRefreshTarget? Target = null);
