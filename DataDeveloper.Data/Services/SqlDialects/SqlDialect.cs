using System.Text;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.Providers.MySql;
using DataDeveloper.Data.Providers.Oracle;
using DataDeveloper.Data.Providers.PostgresSql;
using DataDeveloper.Data.Providers.SqLite;
using DataDeveloper.Data.Providers.SqlServer;

namespace DataDeveloper.Data.Services.SqlDialects;

public abstract class SqlDialect : ISqlDialect
{
    private static readonly ISqlDialect SqlServer = new SqlServerSqlDialect();
    private static readonly ISqlDialect Oracle = new OracleSqlDialect();
    private static readonly ISqlDialect PostgresSql = new PostgresSqlDialect();
    private static readonly ISqlDialect MySql = new MySqlSqlDialect();
    private static readonly ISqlDialect SqLite = new SqLiteSqlDialect();

    public static ISqlDialect For(DatabaseType databaseType)
    {
        return databaseType switch
        {
            DatabaseType.SqlServer => SqlServer,
            DatabaseType.Oracle => Oracle,
            DatabaseType.PostgresSql => PostgresSql,
            DatabaseType.MySql => MySql,
            DatabaseType.SqLite => SqLite,
            _ => throw new ArgumentOutOfRangeException(nameof(databaseType), databaseType, "Unsupported database type.")
        };
    }

    public abstract string QuoteIdentifier(string identifier);

    public virtual string FormatIdentifier(string identifier) => QuoteIdentifier(identifier);

    public virtual string FormatParameterReference(string parameterName) => $"@{parameterName}";

    public string QuoteQualifiedName(string qualifiedName)
    {
        return string.Join(".", SplitQualifiedName(qualifiedName).Select(QuoteIdentifier));
    }

    public IReadOnlyList<string> SplitQualifiedName(string qualifiedName)
    {
        return SplitParts(qualifiedName).Select(part => part.Text).ToList();
    }

    public IReadOnlyList<string> ResolveQualifiedName(string sqlName)
    {
        return SplitParts(sqlName)
            .Select(part => part.IsDelimited ? part.Text : FoldUnquotedIdentifier(part.Text))
            .ToList();
    }

    public virtual string ToFormattableName(string storedName) => storedName;

    /// <summary>
    /// How the database stores an identifier written without delimiters.
    /// </summary>
    protected virtual string FoldUnquotedIdentifier(string identifier) => identifier;

    private static List<(string Text, bool IsDelimited)> SplitParts(string qualifiedName)
    {
        var result = new List<(string Text, bool IsDelimited)>();
        var current = new StringBuilder();
        var isDelimited = false;
        char? quote = null;

        foreach (var ch in qualifiedName)
        {
            if (quote is null)
            {
                if (ch == '.')
                {
                    FlushCurrent();
                    continue;
                }

                if (ch is '[' or '"' or '`')
                {
                    quote = ch;
                    isDelimited = true;
                    continue;
                }

                current.Append(ch);
                continue;
            }

            var isClosing = quote switch
            {
                '[' => ch == ']',
                '"' => ch == '"',
                '`' => ch == '`',
                _ => false
            };

            if (isClosing)
            {
                quote = null;
                continue;
            }

            current.Append(ch);
        }

        FlushCurrent();
        return result;

        void FlushCurrent()
        {
            var part = current.ToString().Trim();
            current.Clear();
            if (!string.IsNullOrWhiteSpace(part))
                result.Add((part, isDelimited));
            isDelimited = false;
        }
    }

    protected static string Delimit(string identifier, char open, char close)
    {
        return $"{open}{identifier.Replace(close.ToString(), $"{close}{close}", StringComparison.Ordinal)}{close}";
    }
}
