using System;
using System.Collections.Generic;
using System.Linq;
using DataDeveloper.Data.Enums;

namespace DataDeveloper.Services;

public enum SqlFunctionCategory
{
    Aggregate,
    Conversion,
    DateTime,
    Math,
    String,
    NullHandling
}

public sealed record SqlFunctionDefinition(
    string Name,
    string Signature,
    string Description,
    string ReturnType,
    SqlFunctionCategory Category,
    IReadOnlyList<string> Parameters,
    bool AcceptsAdditionalArguments);

public static class SqlFunctionCatalog
{
    private static readonly IReadOnlyDictionary<DatabaseType, IReadOnlyList<SqlFunctionDefinition>> FunctionsByProvider =
        new Dictionary<DatabaseType, IReadOnlyList<SqlFunctionDefinition>>
        {
            [DatabaseType.SqlServer] =
            [
                Function("COUNT", "COUNT(expression)", "Returns the number of items in a group.", SqlFunctionCategory.Aggregate),
                Function("SUM", "SUM(expression)", "Returns the sum of non-null values.", SqlFunctionCategory.Aggregate),
                Function("AVG", "AVG(expression)", "Returns the average of non-null values.", SqlFunctionCategory.Aggregate),
                Function("MIN", "MIN(expression)", "Returns the minimum value.", SqlFunctionCategory.Aggregate),
                Function("MAX", "MAX(expression)", "Returns the maximum value.", SqlFunctionCategory.Aggregate),
                Function("GETDATE", "GETDATE()", "Returns the current database system timestamp.", SqlFunctionCategory.DateTime),
                Function("DATEADD", "DATEADD(datepart, number, date)", "Adds an interval to a date.", SqlFunctionCategory.DateTime),
                Function("DATEDIFF", "DATEDIFF(datepart, startdate, enddate)", "Returns the difference between two dates.", SqlFunctionCategory.DateTime),
                Function("ISNULL", "ISNULL(check_expression, replacement_value)", "Replaces null with the specified value.", SqlFunctionCategory.NullHandling),
                Function("COALESCE", "COALESCE(expression, ...)", "Returns the first non-null expression.", SqlFunctionCategory.NullHandling),
                Function("CAST", "CAST(expression AS data_type)", "Converts an expression to another data type.", SqlFunctionCategory.Conversion),
                Function("CONVERT", "CONVERT(data_type, expression, style)", "Converts an expression to another data type.", SqlFunctionCategory.Conversion),
                Function("SUBSTRING", "SUBSTRING(expression, start, length)", "Returns part of a character or binary expression.", SqlFunctionCategory.String),
                Function("LEN", "LEN(string_expression)", "Returns the number of characters.", SqlFunctionCategory.String),
                Function("ROUND", "ROUND(numeric_expression, length)", "Rounds a numeric value.", SqlFunctionCategory.Math)
            ],
            [DatabaseType.Oracle] =
            [
                Function("COUNT", "COUNT(expression)", "Returns the number of items in a group.", SqlFunctionCategory.Aggregate),
                Function("SUM", "SUM(expression)", "Returns the sum of non-null values.", SqlFunctionCategory.Aggregate),
                Function("AVG", "AVG(expression)", "Returns the average of non-null values.", SqlFunctionCategory.Aggregate),
                Function("MIN", "MIN(expression)", "Returns the minimum value.", SqlFunctionCategory.Aggregate),
                Function("MAX", "MAX(expression)", "Returns the maximum value.", SqlFunctionCategory.Aggregate),
                Function("SYSDATE", "SYSDATE", "Returns the current database date and time.", SqlFunctionCategory.DateTime),
                Function("ADD_MONTHS", "ADD_MONTHS(date, integer)", "Adds months to a date.", SqlFunctionCategory.DateTime),
                Function("MONTHS_BETWEEN", "MONTHS_BETWEEN(date1, date2)", "Returns the number of months between two dates.", SqlFunctionCategory.DateTime),
                Function("NVL", "NVL(expr1, expr2)", "Replaces null with the specified value.", SqlFunctionCategory.NullHandling),
                Function("NVL2", "NVL2(expr1, expr2, expr3)", "Returns one value when expr1 is not null and another when it is null.", SqlFunctionCategory.NullHandling),
                Function("COALESCE", "COALESCE(expression, ...)", "Returns the first non-null expression.", SqlFunctionCategory.NullHandling),
                Function("TO_DATE", "TO_DATE(char, fmt)", "Converts text to a date.", SqlFunctionCategory.Conversion),
                Function("TO_CHAR", "TO_CHAR(value, fmt)", "Converts a value to text.", SqlFunctionCategory.Conversion),
                Function("SUBSTR", "SUBSTR(char, position, substring_length)", "Returns part of a string.", SqlFunctionCategory.String),
                Function("LENGTH", "LENGTH(char)", "Returns the length of a string.", SqlFunctionCategory.String),
                Function("ROUND", "ROUND(value, integer)", "Rounds a number or date.", SqlFunctionCategory.Math)
            ],
            [DatabaseType.PostgresSql] =
            [
                Function("COUNT", "count(expression)", "Returns the number of items in a group.", SqlFunctionCategory.Aggregate),
                Function("SUM", "sum(expression)", "Returns the sum of non-null values.", SqlFunctionCategory.Aggregate),
                Function("AVG", "avg(expression)", "Returns the average of non-null values.", SqlFunctionCategory.Aggregate),
                Function("MIN", "min(expression)", "Returns the minimum value.", SqlFunctionCategory.Aggregate),
                Function("MAX", "max(expression)", "Returns the maximum value.", SqlFunctionCategory.Aggregate),
                Function("NOW", "now()", "Returns the current transaction timestamp.", SqlFunctionCategory.DateTime),
                Function("CURRENT_DATE", "current_date", "Returns the current date.", SqlFunctionCategory.DateTime),
                Function("DATE_PART", "date_part(field, source)", "Returns a subfield from a date/time value.", SqlFunctionCategory.DateTime),
                Function("DATE_TRUNC", "date_trunc(field, source)", "Truncates a date/time value.", SqlFunctionCategory.DateTime),
                Function("COALESCE", "coalesce(expression, ...)", "Returns the first non-null expression.", SqlFunctionCategory.NullHandling),
                Function("NULLIF", "nullif(value1, value2)", "Returns null if two values are equal.", SqlFunctionCategory.NullHandling),
                Function("CAST", "cast(expression AS type)", "Converts an expression to another data type.", SqlFunctionCategory.Conversion),
                Function("SUBSTRING", "substring(string, start, count)", "Returns part of a string.", SqlFunctionCategory.String),
                Function("LENGTH", "length(string)", "Returns the length of a string.", SqlFunctionCategory.String),
                Function("ROUND", "round(numeric, scale)", "Rounds a numeric value.", SqlFunctionCategory.Math)
            ],
            [DatabaseType.MySql] =
            [
                Function("COUNT", "COUNT(expr)", "Returns the number of items in a group.", SqlFunctionCategory.Aggregate),
                Function("SUM", "SUM(expr)", "Returns the sum of non-null values.", SqlFunctionCategory.Aggregate),
                Function("AVG", "AVG(expr)", "Returns the average of non-null values.", SqlFunctionCategory.Aggregate),
                Function("MIN", "MIN(expr)", "Returns the minimum value.", SqlFunctionCategory.Aggregate),
                Function("MAX", "MAX(expr)", "Returns the maximum value.", SqlFunctionCategory.Aggregate),
                Function("NOW", "NOW()", "Returns the current date and time.", SqlFunctionCategory.DateTime),
                Function("CURDATE", "CURDATE()", "Returns the current date.", SqlFunctionCategory.DateTime),
                Function("DATE_ADD", "DATE_ADD(date, INTERVAL expr unit)", "Adds an interval to a date.", SqlFunctionCategory.DateTime),
                Function("DATEDIFF", "DATEDIFF(expr1, expr2)", "Returns the number of days between two dates.", SqlFunctionCategory.DateTime),
                Function("IFNULL", "IFNULL(expr1, expr2)", "Returns expr2 when expr1 is null.", SqlFunctionCategory.NullHandling),
                Function("COALESCE", "COALESCE(expr, ...)", "Returns the first non-null expression.", SqlFunctionCategory.NullHandling),
                Function("CAST", "CAST(expr AS type)", "Converts an expression to another data type.", SqlFunctionCategory.Conversion),
                Function("SUBSTRING", "SUBSTRING(str, pos, len)", "Returns part of a string.", SqlFunctionCategory.String),
                Function("CHAR_LENGTH", "CHAR_LENGTH(str)", "Returns the number of characters.", SqlFunctionCategory.String),
                Function("ROUND", "ROUND(x, d)", "Rounds a numeric value.", SqlFunctionCategory.Math)
            ],
            [DatabaseType.SqLite] =
            [
                Function("COUNT", "count(expr)", "Returns the number of items in a group.", SqlFunctionCategory.Aggregate),
                Function("SUM", "sum(expr)", "Returns the sum of non-null values.", SqlFunctionCategory.Aggregate),
                Function("AVG", "avg(expr)", "Returns the average of non-null values.", SqlFunctionCategory.Aggregate),
                Function("MIN", "min(expr)", "Returns the minimum value.", SqlFunctionCategory.Aggregate),
                Function("MAX", "max(expr)", "Returns the maximum value.", SqlFunctionCategory.Aggregate),
                Function("DATE", "date(timestring, modifier, ...)", "Returns a date string.", SqlFunctionCategory.DateTime),
                Function("TIME", "time(timestring, modifier, ...)", "Returns a time string.", SqlFunctionCategory.DateTime),
                Function("DATETIME", "datetime(timestring, modifier, ...)", "Returns a date and time string.", SqlFunctionCategory.DateTime),
                Function("STRFTIME", "strftime(format, timestring, modifier, ...)", "Formats a date/time value.", SqlFunctionCategory.DateTime),
                Function("IFNULL", "ifnull(X, Y)", "Returns Y when X is null.", SqlFunctionCategory.NullHandling),
                Function("COALESCE", "coalesce(X, ...)", "Returns the first non-null argument.", SqlFunctionCategory.NullHandling),
                Function("CAST", "CAST(expr AS type)", "Converts an expression to another type.", SqlFunctionCategory.Conversion),
                Function("SUBSTR", "substr(X, Y, Z)", "Returns part of a string.", SqlFunctionCategory.String),
                Function("LENGTH", "length(X)", "Returns the length of a string or blob.", SqlFunctionCategory.String),
                Function("ROUND", "round(X, Y)", "Rounds a numeric value.", SqlFunctionCategory.Math)
            ]
        };

    public static IReadOnlyList<SqlFunctionDefinition> GetFunctions(DatabaseType databaseType)
    {
        return FunctionsByProvider.TryGetValue(databaseType, out var functions)
            ? functions
            : [];
    }

    public static SqlFunctionDefinition? FindFunction(DatabaseType databaseType, string name)
    {
        return GetFunctions(databaseType)
            .FirstOrDefault(function => string.Equals(function.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private static SqlFunctionDefinition Function(
        string name,
        string signature,
        string description,
        SqlFunctionCategory category)
    {
        var parameters = ParseParameters(signature, out var acceptsAdditionalArguments);
        return new SqlFunctionDefinition(name, signature, description, InferReturnType(name, category), category, parameters, acceptsAdditionalArguments);
    }

    private static string InferReturnType(string name, SqlFunctionCategory category)
    {
        return name.ToUpperInvariant() switch
        {
            "COUNT" => "number",
            "SUM" or "AVG" or "ROUND" => "number",
            "DATEDIFF" or "MONTHS_BETWEEN" or "DATE_PART" or "LEN" or "LENGTH" or "CHAR_LENGTH" => "number",
            "GETDATE" or "NOW" or "SYSDATE" or "DATETIME" or "DATEADD" or "ADD_MONTHS" or "DATE_TRUNC" => "date/time",
            "CURRENT_DATE" or "CURDATE" or "DATE" or "TO_DATE" => "date",
            "TIME" => "time",
            "SUBSTRING" or "SUBSTR" or "TO_CHAR" or "STRFTIME" => "string",
            "CAST" or "CONVERT" => "target type",
            "MIN" or "MAX" or "ISNULL" or "IFNULL" or "NVL" or "NVL2" or "COALESCE" or "NULLIF" => "same as expression",
            _ => category switch
            {
                SqlFunctionCategory.DateTime => "date/time",
                SqlFunctionCategory.String => "string",
                SqlFunctionCategory.Math or SqlFunctionCategory.Aggregate => "number",
                SqlFunctionCategory.Conversion => "target type",
                SqlFunctionCategory.NullHandling => "same as expression",
                _ => "value"
            }
        };
    }

    private static IReadOnlyList<string> ParseParameters(string signature, out bool acceptsAdditionalArguments)
    {
        acceptsAdditionalArguments = false;
        var openParenIndex = signature.IndexOf('(');
        var closeParenIndex = signature.LastIndexOf(')');
        if (openParenIndex < 0 || closeParenIndex <= openParenIndex)
            return [];

        var parameterList = signature[(openParenIndex + 1)..closeParenIndex].Trim();
        if (string.IsNullOrWhiteSpace(parameterList))
            return [];

        var parameters = parameterList
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        if (parameters.Count > 0 && parameters[^1] == "...")
        {
            acceptsAdditionalArguments = true;
            parameters.RemoveAt(parameters.Count - 1);
        }
        else if (parameters.Count > 0 && parameters[^1].EndsWith("...", StringComparison.Ordinal))
        {
            acceptsAdditionalArguments = true;
            parameters[^1] = parameters[^1].Replace("...", string.Empty, StringComparison.Ordinal).Trim();
            if (string.IsNullOrWhiteSpace(parameters[^1]))
                parameters.RemoveAt(parameters.Count - 1);
        }

        return parameters;
    }
}
