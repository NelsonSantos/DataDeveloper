using System;

namespace DataDeveloper.EventAggregators;

/// <summary>Asks the connection's AI chat a question prepared by the app, such as "fix this error".</summary>
public class AskAiEvent(Guid connectionId, string prompt, string attachmentName, string attachmentContent)
{
    public Guid ConnectionId { get; } = connectionId;
    public string Prompt { get; } = prompt;
    public string AttachmentName { get; } = attachmentName;
    public string AttachmentContent { get; } = attachmentContent;
}
