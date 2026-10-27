using System;
using System.Linq;
using System.Reactive;
using DataDeveloper.Core;
using DataDeveloper.Data.Ai;
using DataDeveloper.Data.Models;
using ReactiveUI;
using ReactiveUI.Reactive;

namespace DataDeveloper.ViewModels;

public sealed class AiMessageSegmentViewModel : ViewModelBase
{
    public AiMessageSegmentViewModel(AiMessageSegment segment, Action<string>? insert)
    {
        Text = segment.Text;
        IsCode = segment.IsCode;
        Language = segment.Language;
        CanInsert = segment.IsCode && insert is not null;
        InsertCommand = ReactiveCommand.Create(() => insert?.Invoke(Text));
    }

    public string Text { get; }
    public bool IsCode { get; }
    public bool IsProse => !IsCode;
    public string? Language { get; }
    public bool CanInsert { get; }
    public ReactiveCommand<Unit, Unit> InsertCommand { get; }
}

public sealed class AiChatMessageViewModel : ViewModelBase
{
    public AiChatMessageViewModel(AiChatRole role, string content, bool isError, Action<string>? insert)
    {
        Role = role;
        Content = content;
        IsError = isError;
        Segments = role == AiChatRole.Assistant && !isError
            ? AiMessageSegments.Parse(content).Select(segment => new AiMessageSegmentViewModel(segment, insert)).ToList()
            : [new AiMessageSegmentViewModel(new AiMessageSegment(false, content), insert)];
    }

    public AiChatRole Role { get; }
    public string Content { get; }
    public bool IsError { get; }
    public bool IsUser => Role == AiChatRole.User;
    public bool IsAssistant => Role == AiChatRole.Assistant && !IsError;
    public string Author => IsUser ? "You" : IsError ? "Assistant (error)" : "Assistant";
    public System.Collections.Generic.IReadOnlyList<AiMessageSegmentViewModel> Segments { get; }
}

/// <summary>Something added to the next message: the text of a file, the current query, or a database object.</summary>
public sealed class AiAttachmentItem : ViewModelBase
{
    public AiAttachmentItem(string name, string? textContent, DbObjectRef? databaseObject, Action<AiAttachmentItem> remove)
    {
        Name = name;
        TextContent = textContent;
        DatabaseObject = databaseObject;
        RemoveCommand = ReactiveCommand.Create(() => remove(this));
    }

    public string Name { get; }
    public string? TextContent { get; }
    public DbObjectRef? DatabaseObject { get; }
    public ReactiveCommand<Unit, Unit> RemoveCommand { get; }
}
