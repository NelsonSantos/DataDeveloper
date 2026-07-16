using System.Collections.Generic;
using System.IO;
using System.Linq;
using DataDeveloper.Core;
using DataDeveloper.Interfaces;
using DataDeveloper.Models;

namespace DataDeveloper.Services;

public class RecentFilesService : IRecentFilesService
{
    private const string FileName = "recent-files.json";
    private const string Subfolder = "Config";

    private readonly AppDataFileService _fileService;

    public RecentFilesService(AppDataFileService fileService)
    {
        _fileService = fileService;
    }

    public IReadOnlyList<string> Load()
    {
        var state = _fileService.LoadJson<RecentFilesState>(FileName, Subfolder);
        return state?.Files.Where(File.Exists).ToList() ?? new List<string>();
    }

    public void Save(IReadOnlyList<string> files)
    {
        _fileService.SaveJson(FileName, new RecentFilesState { Files = files.ToList() }, Subfolder);
    }
}
