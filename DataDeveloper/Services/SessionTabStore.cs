using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DataDeveloper.Core;
using DataDeveloper.Interfaces;
using DataDeveloper.Models;

namespace DataDeveloper.Services;

public class SessionTabStore : ISessionTabStore
{
    private const string Subfolder = "state/sessions";
    private readonly AppDataFileService _fileService;

    public SessionTabStore(AppDataFileService fileService)
    {
        _fileService = fileService;
    }

    public ConnectionSessionState? Get(Guid connectionId)
    {
        var path = GetFilePath(connectionId);
        if (!File.Exists(path))
            return null;

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<ConnectionSessionState>(json, MappingExtensions.GetJsonSerializerOptions());
    }

    public void Save(Guid connectionId, IReadOnlyList<EditorTabState> editors)
    {
        var state = new ConnectionSessionState
        {
            ConnectionId = connectionId,
            Editors = editors.ToList()
        };

        var path = GetFilePath(connectionId);
        var tempPath = path + ".tmp";
        var json = JsonSerializer.Serialize(state, MappingExtensions.GetJsonSerializerOptions());

        File.WriteAllText(tempPath, json);
        File.Move(tempPath, path, overwrite: true);
    }

    public void Remove(Guid connectionId)
    {
        var path = GetFilePath(connectionId);
        if (File.Exists(path))
            File.Delete(path);
    }

    private string GetFilePath(Guid connectionId)
    {
        return _fileService.GetFullPath($"{connectionId}.json", Subfolder);
    }
}
