using System;
using System.Collections.Generic;

namespace DataDeveloper.Models;

public class RecentConnectionsState
{
    public List<Guid> ConnectionIds { get; set; } = new();
}
