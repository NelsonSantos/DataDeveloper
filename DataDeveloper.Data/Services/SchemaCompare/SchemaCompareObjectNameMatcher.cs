namespace DataDeveloper.Data.Services.SchemaCompare;

public static class SchemaCompareObjectNameMatcher
{
    private static readonly char[] WrapperCharacters = ['[', ']', '`', '"'];

    public static string Normalize(string name)
    {
        return name.Trim().Trim(WrapperCharacters).ToLowerInvariant();
    }

    public static bool AreEqual(string a, string b)
    {
        return Normalize(a) == Normalize(b);
    }
}
