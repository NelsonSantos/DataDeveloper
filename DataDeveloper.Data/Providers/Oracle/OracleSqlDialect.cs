using System.Text.RegularExpressions;
using DataDeveloper.Data.Services.SqlDialects;

namespace DataDeveloper.Data.Providers.Oracle;

public sealed partial class OracleSqlDialect : SqlDialect
{
    public override string QuoteIdentifier(string identifier) => Delimit(identifier, '"', '"');

    public override string FormatIdentifier(string identifier)
    {
        var trimmedIdentifier = identifier.Trim();
        if (OracleRegularIdentifierRegex().IsMatch(trimmedIdentifier) && !IsReservedWord(trimmedIdentifier))
            return trimmedIdentifier.ToUpperInvariant();

        return QuoteIdentifier(trimmedIdentifier);
    }

    public override string FormatParameterReference(string parameterName) => $":{parameterName}";

    private static bool IsReservedWord(string identifier)
    {
        return ReservedWords.Contains(identifier.ToUpperInvariant());
    }

    private static readonly HashSet<string> ReservedWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "ACCESS",
        "ADD",
        "ALL",
        "ALTER",
        "AND",
        "ANY",
        "AS",
        "ASC",
        "AUDIT",
        "BETWEEN",
        "BY",
        "CHAR",
        "CHECK",
        "CLUSTER",
        "COLUMN",
        "COMMENT",
        "COMPRESS",
        "CONNECT",
        "CREATE",
        "CURRENT",
        "DATE",
        "DECIMAL",
        "DEFAULT",
        "DELETE",
        "DESC",
        "DISTINCT",
        "DROP",
        "ELSE",
        "EXCLUSIVE",
        "EXISTS",
        "FILE",
        "FLOAT",
        "FOR",
        "FROM",
        "GRANT",
        "GROUP",
        "HAVING",
        "IDENTIFIED",
        "IMMEDIATE",
        "IN",
        "INCREMENT",
        "INDEX",
        "INITIAL",
        "INSERT",
        "INTEGER",
        "INTERSECT",
        "INTO",
        "IS",
        "LEVEL",
        "LIKE",
        "LOCK",
        "LONG",
        "MAXEXTENTS",
        "MINUS",
        "MLSLABEL",
        "MODE",
        "MODIFY",
        "NOAUDIT",
        "NOCOMPRESS",
        "NOT",
        "NOWAIT",
        "NULL",
        "NUMBER",
        "OF",
        "OFFLINE",
        "ON",
        "ONLINE",
        "OPTION",
        "OR",
        "ORDER",
        "PCTFREE",
        "PRIOR",
        "PRIVILEGES",
        "PUBLIC",
        "RAW",
        "RENAME",
        "RESOURCE",
        "REVOKE",
        "ROW",
        "ROWID",
        "ROWNUM",
        "ROWS",
        "SELECT",
        "SESSION",
        "SET",
        "SHARE",
        "SIZE",
        "SMALLINT",
        "START",
        "SUCCESSFUL",
        "SYNONYM",
        "SYSDATE",
        "TABLE",
        "THEN",
        "TO",
        "TRIGGER",
        "UID",
        "UNION",
        "UNIQUE",
        "UPDATE",
        "USER",
        "VALIDATE",
        "VALUES",
        "VARCHAR",
        "VARCHAR2",
        "VIEW",
        "WHENEVER",
        "WHERE",
        "WITH"
    };

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_$#]*$")]
    private static partial Regex OracleRegularIdentifierRegex();
}
