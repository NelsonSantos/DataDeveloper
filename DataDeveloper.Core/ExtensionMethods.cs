namespace DataDeveloper;

public static class ExtensionMethods
{
    public static bool IsNullOrEmpty(this string value)
    {
        return string.IsNullOrEmpty(value);
    }
}