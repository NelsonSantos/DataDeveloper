using System.Collections.Generic;

public class SqlContextResult
{
    private bool _inSelectList;
    private bool _inFromClause;
    private bool _afterTableAlias;
    private bool _betweenSelectAndFrom;

    public bool InSelectList
    {
        get => _inSelectList;
        set => _inSelectList = value;
    }

    public bool InFromClause
    {
        get => _inFromClause;
        set
        {
            _inFromClause = value;
            _betweenSelectAndFrom = false;
        }
    }

    public bool AfterTableAlias
    {
        get => _afterTableAlias;
        set => _afterTableAlias = value;
    }

    public bool BetweenSelectAndFrom
    {
        get => _betweenSelectAndFrom;
        set => _betweenSelectAndFrom = value;
    }

    public string? CurrentAlias { get; set; }
    public Dictionary<string, string> Aliases { get; set; } = new(); // alias → tabela
}