using Antlr4.Runtime;
using DataDeveloper.Data.Models;

namespace DataDeveloper.Data.Providers.SqLite;

/// <summary>
/// Reads a SQLite trigger's timing and event from its CREATE TRIGGER statement.
/// </summary>
public static class SqLiteTriggerParser
{
    public static void CompleteFromDefinition(TriggerModel trigger)
    {
        if (string.IsNullOrWhiteSpace(trigger.Definition))
            return;

        var lexer = new SQLiteLexer(new AntlrInputStream(trigger.Definition));
        lexer.RemoveErrorListeners();
        var parser = new SQLiteParser(new CommonTokenStream(lexer));
        parser.RemoveErrorListeners();

        var createTrigger = parser.create_trigger_stmt();

        // SQLite fires a trigger BEFORE the statement when the timing is omitted.
        trigger.Timing = createTrigger.AFTER_() is not null ? "after"
            : createTrigger.INSTEAD_() is not null ? "instead of"
            : "before";

        trigger.Events = createTrigger.INSERT_() is not null ? "insert"
            : createTrigger.DELETE_() is not null ? "delete"
            : createTrigger.UPDATE_() is not null ? "update"
            : string.Empty;
    }
}
