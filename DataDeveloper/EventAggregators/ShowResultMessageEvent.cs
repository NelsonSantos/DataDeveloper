using System;

namespace DataDeveloper.EventAggregators;

public class ShowResultMessageEvent
{
    public ShowResultMessageEvent(Guid id, string message, string? failedStatement = null)
    {
        Id = id;
        Message = message;
        FailedStatement = failedStatement;
    }

    public Guid Id { get; }
    public string Message { get; }

    /// <summary>The SQL that raised the error shown in <see cref="Message"/>; null for ordinary messages.</summary>
    public string? FailedStatement { get; }
}