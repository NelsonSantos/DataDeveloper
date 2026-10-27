using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using DataDeveloper.Core;
using DataDeveloper.Data.Ai;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.Models;
using DataDeveloper.Interfaces;
using ReactiveUI;
using ReactiveUI.Reactive;
using ReactiveUI.SourceGenerators;

namespace DataDeveloper.ViewModels;

/// <summary>The AI chat of one connection: its conversation (persisted), pending attachments and the call to the provider.</summary>
public partial class AiChatViewModel : ViewModelBase
{
    private const int MaxAttachmentFileBytes = 1_000_000;
    private const int MaxLoadedMessages = 200;

    private readonly IConnectionSettings _connection;
    private readonly AiProviderFactoryService _providerFactory;
    private readonly IAiSettingsRepository _settingsRepository;
    private readonly IAiChatHistoryRepository _historyRepository;
    private readonly AiSqlContextBuilder _contextBuilder;
    private readonly List<AiChatMessage> _history = [];
    private IReadOnlyList<DbObjectRef> _allObjects = [];
    private CancellationTokenSource? _cancellation;

    public AiChatViewModel(
        IConnectionSettings connection,
        AiProviderFactoryService providerFactory,
        IAiSettingsRepository settingsRepository,
        IAiChatHistoryRepository historyRepository,
        AiSqlContextBuilder contextBuilder)
    {
        _connection = connection;
        _providerFactory = providerFactory;
        _settingsRepository = settingsRepository;
        _historyRepository = historyRepository;
        _contextBuilder = contextBuilder;

        _model = settingsRepository.Load(ProviderType).Model ?? string.Empty;

        foreach (var message in historyRepository.Load(connection.Id, MaxLoadedMessages))
        {
            _history.Add(message);
            Messages.Add(new AiChatMessageViewModel(message.Role, message.Content, false, InsertCode));
        }

        var canSend = this.WhenAnyValue(vm => vm.IsBusy, vm => vm.Draft, (busy, draft) => !busy && !string.IsNullOrWhiteSpace(draft));
        SendCommand = ReactiveCommand.CreateFromTask(() => SendAsync(Draft), canSend);
        StopCommand = ReactiveCommand.Create(Stop, this.WhenAnyValue(vm => vm.IsBusy));
        ClearCommand = ReactiveCommand.Create(ClearHistory, this.WhenAnyValue(vm => vm.IsBusy, busy => !busy));
        AttachCurrentQueryCommand = ReactiveCommand.Create(AttachCurrentQuery);

        this.WhenAnyValue(vm => vm.ObjectFilter).Subscribe(_ => ApplyObjectFilter());
    }

    public AiProviderType ProviderType => _providerFactory.SupportedProviders[0].ProviderType;

    public ObservableCollection<AiChatMessageViewModel> Messages { get; } = [];
    public ObservableCollection<AiAttachmentItem> Attachments { get; } = [];
    public ObservableCollection<DbObjectRef> ObjectChoices { get; } = [];
    public IReadOnlyList<string> ModelSuggestions { get; } = ["sonnet", "opus", "haiku"];

    /// <summary>Name and text of the active query (its selection if any); set by the connection view model.</summary>
    public Func<(string Name, string Text)?>? CurrentQueryProvider { get; set; }

    /// <summary>Puts code into the active query editor; returns false when there is none.</summary>
    public Func<string, bool>? InsertIntoEditor { get; set; }

    public bool HasMessages => Messages.Count > 0;

    [Reactive] private string _draft = string.Empty;
    [Reactive] private bool _isBusy;
    [Reactive] private string _statusText = string.Empty;
    [Reactive] private string _model = string.Empty;
    [Reactive] private string _objectFilter = string.Empty;

    public ReactiveCommand<Unit, Unit> SendCommand { get; }
    public ReactiveCommand<Unit, Unit> StopCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearCommand { get; }
    public ReactiveCommand<Unit, Unit> AttachCurrentQueryCommand { get; }

    /// <summary>Forget the cached object list (after a schema refresh).</summary>
    public void InvalidateSchema()
    {
        _contextBuilder.Invalidate();
        _allObjects = [];
    }

    /// <summary>Stops a running request; called when the connection closes.</summary>
    public void CancelPendingRequest() => _cancellation?.Cancel();

    /// <summary>Sends a message built by the app (for example "fix this error") with optional attachments.</summary>
    public async Task AskAsync(string prompt, IEnumerable<AiAttachmentItem>? attachments = null)
    {
        if (IsBusy)
        {
            StatusText = "Wait for the current answer first.";
            return;
        }

        if (attachments is not null)
        {
            foreach (var attachment in attachments)
                Attachments.Add(attachment);
        }

        await SendAsync(prompt);
    }

    public AiAttachmentItem CreateTextAttachment(string name, string content) =>
        new(name, content, null, RemoveAttachment);

    public async Task AttachFileAsync(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (info.Length > MaxAttachmentFileBytes)
            {
                StatusText = $"\"{info.Name}\" is larger than {MaxAttachmentFileBytes / 1_000_000} MB.";
                return;
            }

            var bytes = await File.ReadAllBytesAsync(path);
            if (Array.IndexOf(bytes, (byte)0) >= 0)
            {
                StatusText = $"\"{info.Name}\" is not a text file.";
                return;
            }

            Attachments.Add(CreateTextAttachment(info.Name, System.Text.Encoding.UTF8.GetString(bytes)));
            StatusText = string.Empty;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            StatusText = exception.Message;
        }
    }

    public void AttachObjects(IEnumerable<DbObjectRef> objects)
    {
        foreach (var databaseObject in objects)
        {
            if (Attachments.Any(a => a.DatabaseObject is not null && a.DatabaseObject == databaseObject))
                continue;

            Attachments.Add(new AiAttachmentItem(databaseObject.QualifiedName, null, databaseObject, RemoveAttachment));
        }
    }

    public async Task LoadObjectChoicesAsync()
    {
        try
        {
            _allObjects = await _contextBuilder.GetObjectsAsync();
        }
        catch (Exception exception)
        {
            StatusText = $"The database objects could not be listed: {exception.Message}";
            _allObjects = [];
        }

        ApplyObjectFilter();
    }

    public void InsertCode(string code)
    {
        if (InsertIntoEditor?.Invoke(code) != true)
            StatusText = "Open a query tab to insert code into.";
    }

    private void AttachCurrentQuery()
    {
        var query = CurrentQueryProvider?.Invoke();
        if (query is not { } current || string.IsNullOrWhiteSpace(current.Text))
        {
            StatusText = "The current query is empty.";
            return;
        }

        Attachments.Add(CreateTextAttachment(current.Name, current.Text));
        StatusText = string.Empty;
    }

    private void RemoveAttachment(AiAttachmentItem attachment) => Attachments.Remove(attachment);

    private void ApplyObjectFilter()
    {
        var filter = ObjectFilter.Trim();
        ObjectChoices.Clear();
        foreach (var item in _allObjects.Where(o => filter.Length == 0 || o.QualifiedName.Contains(filter, StringComparison.OrdinalIgnoreCase)).Take(200))
            ObjectChoices.Add(item);
    }

    private void Stop() => _cancellation?.Cancel();

    private void ClearHistory()
    {
        _historyRepository.Clear(_connection.Id);
        _history.Clear();
        Messages.Clear();
        this.RaisePropertyChanged(nameof(HasMessages));
        StatusText = string.Empty;
    }

    private async Task SendAsync(string text)
    {
        text = text.Trim();
        if (text.Length == 0)
            return;

        var files = Attachments.Where(a => a.TextContent is not null).Select(a => new AiTextAttachment(a.Name, a.TextContent!)).ToList();
        var objects = Attachments.Where(a => a.DatabaseObject is not null).Select(a => a.DatabaseObject!).ToList();
        var historyText = AiPromptComposer.DescribeForHistory(text, files, objects.Select(o => o.QualifiedName).ToList());

        Draft = string.Empty;
        Attachments.Clear();
        StatusText = string.Empty;

        var previous = AiTranscriptFormatter.TrimHistory(_history);
        AddMessage(new AiChatMessage(AiChatRole.User, historyText, DateTime.UtcNow), persist: true);

        IsBusy = true;
        _cancellation = new CancellationTokenSource();
        var cancellationToken = _cancellation.Token;

        try
        {
            var settings = _settingsRepository.Load(ProviderType);
            if (!string.IsNullOrWhiteSpace(Model))
                settings.Model = Model.Trim();

            var provider = _providerFactory.Create(settings);

            var relevantText = text + "\n" + string.Join("\n", files.Select(f => f.Content));
            var systemPrompt = await _contextBuilder.BuildSystemPromptAsync(relevantText, objects, cancellationToken);
            var prompt = AiPromptComposer.Compose(text, files);

            var response = await provider.SendMessageAsync(new AiChatRequest(prompt, systemPrompt, previous), cancellationToken);

            if (response.Success)
                AddMessage(new AiChatMessage(AiChatRole.Assistant, response.Content, DateTime.UtcNow), persist: true);
            else
                AddError($"{response.ErrorMessage} (Check Preferences > AI Assistant.)");
        }
        catch (OperationCanceledException)
        {
            AddError("Stopped.");
        }
        catch (Exception exception)
        {
            AddError(exception.Message);
        }
        finally
        {
            IsBusy = false;
            _cancellation.Dispose();
            _cancellation = null;
        }
    }

    private void AddMessage(AiChatMessage message, bool persist)
    {
        _history.Add(message);
        if (persist)
            _historyRepository.Append(_connection.Id, message);

        Messages.Add(new AiChatMessageViewModel(message.Role, message.Content, false, InsertCode));
        this.RaisePropertyChanged(nameof(HasMessages));
    }

    private void AddError(string text)
    {
        Messages.Add(new AiChatMessageViewModel(AiChatRole.Assistant, text, true, null));
        this.RaisePropertyChanged(nameof(HasMessages));
    }
}
