using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using System;
using System.Collections.Generic;
using SqlServer;

public class SqlCaretContextVisitor : TSqlParserBaseVisitor<object?>
{
    private readonly int _caretPosition;
    private readonly ITokenStream _tokens;
    public SqlContextResult Result { get; } = new();

    public SqlCaretContextVisitor(int caretPosition, ITokenStream tokens)
    {
        _caretPosition = caretPosition;
        _tokens = tokens;

        DetectAfterAliasDot();
    }

    public override object? VisitSelect_list(TSqlParser.Select_listContext context)
    {
        if (IsInside(context))
            Result.InSelectList = true;
        return base.VisitSelect_list(context);
    }

    public override object? VisitTable_sources(TSqlParser.Table_sourcesContext context)
    {
        if (IsInside(context))
            Result.InFromClause = true;
        return base.VisitTable_sources(context);
    }

    public override object? VisitTable_source(TSqlParser.Table_sourceContext context)
    {
        string? tableName = null;
        string? alias = null;

        var children = context.children;
        if (children == null) return base.VisitTable_source(context);

        for (int i = 0; i < children.Count; i++)
        {
            var node = children[i];
            var text = node.GetText();

            if (tableName == null && !IsSqlKeyword(text))
            {
                tableName = GetOriginalText(node);
            }
            else if (text.Equals("AS", StringComparison.OrdinalIgnoreCase) && i + 1 < children.Count)
            {
                alias = GetOriginalText(children[i + 1]);
            }
            else if (tableName != null && alias == null && !IsSqlKeyword(text))
            {
                alias = GetOriginalText(node);
            }
        }

        if (!string.IsNullOrWhiteSpace(tableName))
        {
            var key = !string.IsNullOrWhiteSpace(alias) ? alias : tableName;
            Result.Aliases[key] = tableName;
        }

        return base.VisitTable_source(context);
    }

    public override object? VisitSelect_statement(TSqlParser.Select_statementContext context)
    {
        DetectBetweenSelectAndFrom(context);
        return base.VisitSelect_statement(context);
    }

    private void DetectBetweenSelectAndFrom(TSqlParser.Select_statementContext context)
    {
        bool foundSelect = false;

        int start = context.Start.TokenIndex;
        int stop = context.Stop.TokenIndex;

        for (int i = start; i <= stop; i++)
        {
            var token = _tokens.Get(i);

            if (token.Type == TSqlParser.SELECT)
            {
                foundSelect = true;
                continue;
            }

            if (token.Type == TSqlParser.FROM)
            {
                if (foundSelect && _caretPosition < token.StartIndex)
                {
                    Result.BetweenSelectAndFrom = true;
                }
                break;
            }

            if (foundSelect && token.StartIndex <= _caretPosition)
            {
                Result.BetweenSelectAndFrom = true;
                break;
            }
        }
    }

    private void DetectAfterAliasDot()
    {
        for (int i = 1; i < _tokens.Size - 1; i++)
        {
            var prev = _tokens.Get(i - 1);
            var dot = _tokens.Get(i);
            var next = _tokens.Get(i + 1);

            if (dot.Type == TSqlParser.DOT &&
                prev.StopIndex < _caretPosition &&
                _caretPosition <= next.StartIndex)
            {
                Result.AfterTableAlias = true;
                Result.CurrentAlias = prev.Text;
                break;
            }
        }
    }

    private string GetOriginalText(IParseTree node)
    {
        if (node is ParserRuleContext ctx)
        {
            var start = ctx.Start.TokenIndex;
            var stop = ctx.Stop.TokenIndex;

            var parts = new List<string>();
            for (int i = start; i <= stop; i++)
            {
                parts.Add(_tokens.Get(i).Text);
            }

            return string.Join(" ", parts);
        }

        return node.GetText();
    }

    private bool IsInside(ParserRuleContext ctx) =>
        ctx.Start.StartIndex <= _caretPosition && _caretPosition <= ctx.Stop.StopIndex;

    private bool IsSqlKeyword(string text)
    {
        return text.Equals("AS", StringComparison.OrdinalIgnoreCase)
               || text.Equals("JOIN", StringComparison.OrdinalIgnoreCase)
               || text.Equals("ON", StringComparison.OrdinalIgnoreCase)
               || text.Equals("INNER", StringComparison.OrdinalIgnoreCase)
               || text.Equals("LEFT", StringComparison.OrdinalIgnoreCase)
               || text.Equals("RIGHT", StringComparison.OrdinalIgnoreCase)
               || text.Equals("FULL", StringComparison.OrdinalIgnoreCase)
               || text.Equals("OUTER", StringComparison.OrdinalIgnoreCase)
               || text.Equals("CROSS", StringComparison.OrdinalIgnoreCase);
    }
}
