using System;
using System.Collections.Generic;
using DataDeveloper.Data.Models;

namespace DataDeveloper.Interfaces;

public interface IConnectionGroupRepository
{
    IReadOnlyList<ConnectionGroup> LoadAll();
    void Save(ConnectionGroup group);
    void Delete(Guid groupId);
}
