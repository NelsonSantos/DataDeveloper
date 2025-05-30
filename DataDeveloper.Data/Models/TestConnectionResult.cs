namespace DataDeveloper.Data.Models;

public class TestConnectionResult
{
    public TestConnectionResult(bool success, string resultMessage)
    {
        Success = success;
        ResultMessage = resultMessage;
    }
    public bool Success { get; }
    public string ResultMessage { get; }
}