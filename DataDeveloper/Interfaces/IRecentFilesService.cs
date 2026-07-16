using System.Collections.Generic;

namespace DataDeveloper.Interfaces;

public interface IRecentFilesService
{
    IReadOnlyList<string> Load();
    void Save(IReadOnlyList<string> files);
}
