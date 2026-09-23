using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Models;
using DataDeveloper.Data.Services.SqlDialects;

namespace DataDeveloper.Data.Providers.SqLite;

/// <summary>
/// Extracts the column and table CHECK constraints from a SQLite CREATE TABLE statement.
/// </summary>
public static class SqLiteCheckConstraintParser
{
    public static IReadOnlyList<CheckConstraintModel> Parse(string createTableSql)
    {
        if (string.IsNullOrWhiteSpace(createTableSql))
            return [];

        var lexer = new SQLiteLexer(new AntlrInputStream(createTableSql));
        lexer.RemoveErrorListeners();
        var parser = new SQLiteParser(new CommonTokenStream(lexer));
        parser.RemoveErrorListeners();

        var createTable = parser.create_table_stmt();
        var constraints = new List<CheckConstraintModel>();

        // Column constraints come first in the statement, then table constraints.
        foreach (var column in createTable.column_def())
        {
            foreach (var constraint in column.column_constraint())
            {
                if (constraint.CHECK_() is not null && constraint.expr() is not null)
                    constraints.Add(CreateModel(constraint.name(), constraint.expr()));
            }
        }

        foreach (var constraint in createTable.table_constraint())
        {
            if (constraint.CHECK_() is not null && constraint.expr() is not null)
                constraints.Add(CreateModel(constraint.name(), constraint.expr()));
        }

        return constraints;
    }

    private static CheckConstraintModel CreateModel(SQLiteParser.NameContext? name, SQLiteParser.ExprContext expression)
    {
        var constraintName = name is null
            ? string.Empty
            : SqlDialect.For(DatabaseType.SqLite).SplitQualifiedName(OriginalText(name)).FirstOrDefault() ?? string.Empty;

        return new CheckConstraintModel
        {
            ConstraintName = constraintName,
            Definition = OriginalText(expression)
        };
    }

    // The source text keeps the user's spacing, which the parser's GetText() drops.
    private static string OriginalText(ParserRuleContext context)
    {
        return context.Start.InputStream.GetText(Interval.Of(context.Start.StartIndex, context.Stop.StopIndex));
    }
}
