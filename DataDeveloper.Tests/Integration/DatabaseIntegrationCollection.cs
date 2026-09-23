using Xunit;

namespace DataDeveloper.Tests.Integration;

/// <summary>
/// Integration tests share the seeded databases: some write to the <c>customers</c> table while
/// others create and drop tables with foreign keys to it, which deadlocks on Oracle (ORA-00060)
/// and skews row counts when they overlap. Classes in this collection run one at a time.
/// </summary>
[CollectionDefinition(Name)]
public sealed class DatabaseIntegrationCollection
{
    public const string Name = "Database integration";
}
