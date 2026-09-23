namespace DataDeveloper.Data.Models;

/// <summary>
/// How to list a table's triggers. <see cref="Statement"/> returns rows shaped like
/// <see cref="TriggerModel"/>; when the database keeps timing and events only in the trigger's
/// DDL, it also returns Definition and <see cref="CompleteFromDefinition"/> fills them from it.
/// </summary>
public sealed record TriggersQuery(
    string Statement,
    Action<TriggerModel>? CompleteFromDefinition = null);
