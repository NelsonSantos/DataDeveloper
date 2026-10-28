using System;
using System.Linq;
using System.Reactive;
using DataDeveloper.Core;
using DataDeveloper.Data.Ai;
using DataDeveloper.Data.Models;
using ReactiveUI;
using ReactiveUI.Reactive;
using ReactiveUI.SourceGenerators;

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

/// <summary>One row of the chat picker: a conversation of the connection, with inline rename and delete confirmation.</summary>
public sealed partial class AiChatListItem : ViewModelBase
{
    public AiChatListItem(
        AiChatInfo chat,
        bool isCurrent,
        DateTime nowUtc,
        Action<AiChatListItem> select,
        Action<AiChatListItem> rename,
        Action<AiChatListItem> delete)
    {
        Id = chat.Id;
        Title = chat.Title;
        IsCurrent = isCurrent;
        Subtitle = DescribeActivity(chat, nowUtc);

        SelectCommand = ReactiveCommand.Create(() => select(this));
        BeginRenameCommand = ReactiveCommand.Create(() =>
        {
            EditTitle = Title;
            IsConfirmingDelete = false;
            IsEditing = true;
        });
        CommitRenameCommand = ReactiveCommand.Create(() =>
        {
            IsEditing = false;
            if (!string.IsNullOrWhiteSpace(EditTitle) && !string.Equals(EditTitle.Trim(), Title, StringComparison.Ordinal))
                rename(this);
        });
        CancelRenameCommand = ReactiveCommand.Create(() => { IsEditing = false; });
        AskDeleteCommand = ReactiveCommand.Create(() =>
        {
            IsEditing = false;
            IsConfirmingDelete = true;
        });
        ConfirmDeleteCommand = ReactiveCommand.Create(() => delete(this));
        CancelDeleteCommand = ReactiveCommand.Create(() => { IsConfirmingDelete = false; });
    }

    public Guid Id { get; }
    public string Title { get; }
    public string Subtitle { get; }
    public bool IsCurrent { get; }

    [Reactive] private bool _isEditing;
    [Reactive] private bool _isConfirmingDelete;
    [Reactive] private string _editTitle = string.Empty;

    public ReactiveCommand<Unit, Unit> SelectCommand { get; }
    public ReactiveCommand<Unit, Unit> BeginRenameCommand { get; }
    public ReactiveCommand<Unit, Unit> CommitRenameCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelRenameCommand { get; }
    public ReactiveCommand<Unit, Unit> AskDeleteCommand { get; }
    public ReactiveCommand<Unit, Unit> ConfirmDeleteCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelDeleteCommand { get; }

    /// <summary>"6 messages · 2 min ago" style line under the title.</summary>
    public static string DescribeActivity(AiChatInfo chat, DateTime nowUtc)
    {
        var count = chat.MessageCount == 1 ? "1 message" : $"{chat.MessageCount} messages";
        return chat.MessageCount == 0 ? "empty" : $"{count} \u00B7 {FormatAge(nowUtc - chat.UpdatedAtUtc)}";
    }

    public static string FormatAge(TimeSpan age)
    {
        if (age < TimeSpan.FromMinutes(1))
            return "just now";
        if (age < TimeSpan.FromHours(1))
            return $"{(int)age.TotalMinutes} min ago";
        if (age < TimeSpan.FromDays(1))
            return $"{(int)age.TotalHours} h ago";
        if (age < TimeSpan.FromDays(2))
            return "yesterday";
        return $"{(int)age.TotalDays} days ago";
    }
}
