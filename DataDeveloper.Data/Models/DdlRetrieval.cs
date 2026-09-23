namespace DataDeveloper.Data.Models;

/// <summary>
/// How to read an object's DDL from the database: an optional statement that prepares the
/// session, the query whose rows hold the DDL, and an optional rewrite of the text it returns.
/// </summary>
public sealed record DdlRetrieval(
    string Query,
    string? SessionSetup = null,
    Func<string, string>? PostProcess = null);
