using System.Text.RegularExpressions;
using DataDeveloper.Data.Enums;

namespace DataDeveloper.Data.Services.SchemaCompare;

public static class CreateOrReplaceScriptRewriter
{
    private static readonly Regex LeadingCreateRegex = new(
        @"^(?<prefix>\s*create\s+)(?<existing>or\s+(replace|alter)\s+)?(?<kind>view|procedure|function)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string BuildChangedObjectScript(
        DatabaseType databaseType,
        SchemaCompareObjectType objectType,
        string qualifiedObjectName,
        string sourceDdl)
    {
        return databaseType switch
        {
            DatabaseType.SqlServer => RewriteLeadingCreate(sourceDdl, "or alter"),
            DatabaseType.Oracle => RewriteLeadingCreate(sourceDdl, "or replace"),
            DatabaseType.PostgresSql => BuildPostgresScript(objectType, qualifiedObjectName, sourceDdl),
            DatabaseType.MySql => BuildMySqlScript(objectType, qualifiedObjectName, sourceDdl),
            DatabaseType.SqLite => BuildDropThenCreate("view", qualifiedObjectName, sourceDdl),
            _ => sourceDdl
        };
    }

    private static string BuildPostgresScript(SchemaCompareObjectType objectType, string qualifiedObjectName, string sourceDdl)
    {
        return objectType == SchemaCompareObjectType.Procedure
            ? BuildDropThenCreate("procedure", qualifiedObjectName, sourceDdl)
            : RewriteLeadingCreate(sourceDdl, "or replace");
    }

    private static string BuildMySqlScript(SchemaCompareObjectType objectType, string qualifiedObjectName, string sourceDdl)
    {
        return objectType switch
        {
            SchemaCompareObjectType.Procedure => BuildDropThenCreate("procedure", qualifiedObjectName, sourceDdl),
            SchemaCompareObjectType.Function => BuildDropThenCreate("function", qualifiedObjectName, sourceDdl),
            _ => RewriteLeadingCreate(sourceDdl, "or replace")
        };
    }

    private static string BuildDropThenCreate(string kind, string qualifiedObjectName, string sourceDdl)
    {
        return $"drop {kind} if exists {qualifiedObjectName};{Environment.NewLine}{Environment.NewLine}{sourceDdl}";
    }

    private static string RewriteLeadingCreate(string ddl, string modifier)
    {
        return LeadingCreateRegex.Replace(ddl, match =>
        {
            if (match.Groups["existing"].Success)
                return match.Value;

            return match.Groups["prefix"].Value + modifier + " " + match.Groups["kind"].Value;
        }, 1);
    }
}
