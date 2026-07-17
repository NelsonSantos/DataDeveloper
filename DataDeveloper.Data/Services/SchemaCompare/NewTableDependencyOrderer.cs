using DataDeveloper.Data.Models.SchemaCompare;
using DataDeveloper.Data.Models.TableDesigner;

namespace DataDeveloper.Data.Services.SchemaCompare;

/// <summary>
/// Orders newly-created tables so a table is emitted after every other newly-created table
/// its foreign keys reference (Kahn's algorithm). Only considers FKs that point at another
/// table in the same input set — FKs to tables outside this run (already on the destination)
/// aren't ordering constraints. On a cycle, the involved tables are appended in their original
/// order with a warning comment rather than blocking generation.
/// </summary>
public static class NewTableDependencyOrderer
{
    public static IReadOnlyList<SchemaCompareObjectResult> Order(IReadOnlyList<SchemaCompareObjectResult> newTableResults)
    {
        if (newTableResults.Count <= 1)
            return newTableResults;

        var normalizedNames = newTableResults.Select(r => SchemaCompareObjectNameMatcher.Normalize(r.Name)).ToList();
        var indexByName = new Dictionary<string, int>();
        for (var i = 0; i < newTableResults.Count; i++)
            indexByName.TryAdd(normalizedNames[i], i);

        var adjacency = new List<int>[newTableResults.Count];
        for (var i = 0; i < adjacency.Length; i++)
            adjacency[i] = new List<int>();
        var inDegree = new int[newTableResults.Count];

        for (var i = 0; i < newTableResults.Count; i++)
        {
            var foreignKeys = newTableResults[i].NewTableDefinition?.ForeignKeys ?? new List<TableForeignKeyDefinition>();
            foreach (var foreignKey in foreignKeys)
            {
                if (!IsUsableForeignKey(foreignKey))
                    continue;

                var referencedName = SchemaCompareObjectNameMatcher.Normalize(foreignKey.ReferencedTableName);
                if (referencedName == normalizedNames[i])
                    continue; // self-referencing FK: no ordering hazard within one CREATE TABLE

                if (!indexByName.TryGetValue(referencedName, out var referencedIndex))
                    continue; // referenced table isn't part of this run

                adjacency[referencedIndex].Add(i);
                inDegree[i]++;
            }
        }

        var queue = new List<int>();
        for (var i = 0; i < newTableResults.Count; i++)
        {
            if (inDegree[i] == 0)
                queue.Add(i);
        }

        var orderedIndexes = new List<int>();
        var pointer = 0;
        while (pointer < queue.Count)
        {
            var current = queue[pointer];
            pointer++;
            orderedIndexes.Add(current);

            foreach (var dependent in adjacency[current])
            {
                inDegree[dependent]--;
                if (inDegree[dependent] == 0)
                    queue.Add(dependent);
            }
        }

        if (orderedIndexes.Count == newTableResults.Count)
            return orderedIndexes.Select(i => newTableResults[i]).ToList();

        var resolved = new HashSet<int>(orderedIndexes);
        var cycleIndexes = Enumerable.Range(0, newTableResults.Count).Where(i => !resolved.Contains(i)).ToList();
        var cycleResults = cycleIndexes.Select(i => newTableResults[i]).ToList();

        var cycleNames = string.Join(", ", cycleResults.Select(r => r.Name));
        cycleResults[0].Script = BuildCycleWarning(cycleNames) + cycleResults[0].Script;

        return orderedIndexes.Select(i => newTableResults[i]).Concat(cycleResults).ToList();
    }

    private static bool IsUsableForeignKey(TableForeignKeyDefinition foreignKey)
    {
        return foreignKey.ColumnNames.Count > 0 &&
               !string.IsNullOrWhiteSpace(foreignKey.ReferencedTableName) &&
               foreignKey.ReferencedColumnNames.Count > 0;
    }

    private static string BuildCycleWarning(string cycleNames)
    {
        return
            $"-- WARNING: circular foreign key dependency detected among: {cycleNames}.{Environment.NewLine}" +
            $"-- These tables are emitted in selection order (not dependency order) below.{Environment.NewLine}" +
            $"-- You will need to either create them without their FK constraints first and{Environment.NewLine}" +
            $"-- ALTER TABLE ... ADD CONSTRAINT afterward, or temporarily disable FK checks{Environment.NewLine}" +
            $"-- for this section, then re-enable them.{Environment.NewLine}{Environment.NewLine}";
    }
}
