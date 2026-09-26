using System;
using System.Collections.Generic;
using System.Linq;
using DataDeveloper.Core;
using DataDeveloper.Interfaces;
using DataDeveloper.Models;

namespace DataDeveloper.Services;

public class RecentConnectionsService : IRecentConnectionsService
{
    private const string FileName = "recent-connections.json";
    private const string Subfolder = "Config";

    private readonly AppDataFileService _fileService;

    public RecentConnectionsService(AppDataFileService fileService)
    {
        _fileService = fileService;
    }

    public IReadOnlyList<Guid> Load()
    {
        var state = _fileService.LoadJson<RecentConnectionsState>(FileName, Subfolder);
        return state?.ConnectionIds.Distinct().ToList() ?? new List<Guid>();
    }

    public void Save(IReadOnlyList<Guid> connectionIds)
    {
        _fileService.SaveJson(FileName, new RecentConnectionsState { ConnectionIds = connectionIds.ToList() }, Subfolder);
    }
}
