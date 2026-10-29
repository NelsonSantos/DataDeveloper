using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
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
    private const int RelevantHistoryMessages = 6;

    private readonly IConnectionSettings _connection;
    private readonly AiProviderFactoryService _providerFactory;
    private readonly IAiSettingsRepository _settingsRepository;
    private readonly IAiChatHistoryRepository _historyRepository;
    private readonly AiSqlContextBuilder _contextBuilder;
    private readonly List<AiChatMessage> _history = [];
    private Guid? _chatId;
    private bool _isApplyingChat;
    private IReadOnlyList<DbObjectRef> _allObjects = [];
    private CancellationTokenSource? _cancellation;
    private AiUsage? _sessionUsage;
    private AiUsage? _lastUsage;
    private bool _sessionStarted;
    private bool _redescribeSchema;
    private long? _contextWindow;
    private readonly Dictionary<string, AiRateLimit> _rateLimits = new(StringComparer.OrdinalIgnoreCase);

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

        // The panel opens the connection's most recently active conversation; a new one is created on first use.
        LoadChat(historyRepository.ListChats(connection.Id).FirstOrDefault());
        RefreshChats();

        var canSend = this.WhenAnyValue(vm => vm.IsBusy, vm => vm.Draft, (busy, draft) => !busy && !string.IsNullOrWhiteSpace(draft));
        SendCommand = ReactiveCommand.CreateFromTask(() => SendAsync(Draft), canSend);
        StopCommand = ReactiveCommand.Create(Stop, this.WhenAnyValue(vm => vm.IsBusy));
        ClearCommand = ReactiveCommand.Create(ClearHistory, this.WhenAnyValue(vm => vm.IsBusy, busy => !busy));
        NewChatCommand = ReactiveCommand.Create(StartNewChat, this.WhenAnyValue(vm => vm.IsBusy, busy => !busy));
        AttachWholeQueryCommand = ReactiveCommand.Create(() => AttachCurrentQuery(selectionOnly: false));
        AttachSelectionCommand = ReactiveCommand.Create(() => AttachCurrentQuery(selectionOnly: true));

        _sendWithModifier = settingsRepository.LoadSendWithModifier();
        this.WhenAnyValue(vm => vm.SendWithModifier).Skip(1).Subscribe(settingsRepository.SaveSendWithModifier);

        this.WhenAnyValue(vm => vm.ObjectFilter).Subscribe(_ => ApplyObjectFilter());
        this.WhenAnyValue(vm => vm.SendWithModifier).Subscribe(_ =>
        {
            this.RaisePropertyChanged(nameof(InputHint));
        });
        this.WhenAnyValue(vm => vm.Model).Subscribe(_ => this.RaisePropertyChanged(nameof(ModelLabel)));
        this.WhenAnyValue(vm => vm.Model).Skip(1).Subscribe(model =>
        {
            if (!_isApplyingChat && _chatId is { } chatId)
                _historyRepository.SetChatModel(chatId, model);
        });
    }

    public AiProviderType ProviderType => _providerFactory.SupportedProviders[0].ProviderType;

    public ObservableCollection<AiChatMessageViewModel> Messages { get; } = [];
    public ObservableCollection<AiAttachmentItem> Attachments { get; } = [];
    public ObservableCollection<DbObjectRef> ObjectChoices { get; } = [];

    /// <summary>The connection's conversations as listed in the picker, most recently active first.</summary>
    public ObservableCollection<AiChatListItem> Chats { get; } = [];

    /// <summary>
    /// Models offered in the picker. The CLI has no command that lists models (and the vendor API needs a key), so this
    /// is a short list of aliases, which always resolve to the newest model of a family and so never go stale. Any other
    /// model or pinned version can be typed through "Other model...".
    /// </summary>
    /// <summary>
    /// The model a chat starts with when neither the chat nor Preferences chose one. The CLI's own default depends on the
    /// plan (some default to the most expensive family), so the app picks a balanced one instead.
    /// </summary>
    public const string DefaultModel = "sonnet";

    public static IReadOnlyList<AiModelOption> ModelOptions { get; } =
    [
        new("opus", "Opus (latest)", IsAlias: true),
        new("sonnet", "Sonnet (latest)", IsAlias: true),
        new("haiku", "Haiku (latest)", IsAlias: true)
    ];

    /// <summary>Text of the model button: the friendly name when the model is known, otherwise the id typed by the user.</summary>
    public string ModelLabel => string.IsNullOrWhiteSpace(Model)
        ? "CLI default"
        : ModelOptions.FirstOrDefault(option => string.Equals(option.Id, Model.Trim(), StringComparison.OrdinalIgnoreCase))?.Label
          ?? Model.Trim();

    /// <summary>
    /// Name and text of the active query; the argument asks for the selection only (null when nothing is selected).
    /// Set by the connection view model.
    /// </summary>
    public Func<bool, (string Name, string Text)?>? CurrentQueryProvider { get; set; }

    /// <summary>Puts code into the active query editor; returns false when there is none.</summary>
    public Func<string, bool>? InsertIntoEditor { get; set; }

    public bool HasMessages => Messages.Count > 0;

    /// <summary>The account's usage windows as last reported by the CLI; the CLI gives their state and reset time, not a percentage.</summary>
    public ObservableCollection<AiUsageLimitItem> UsageLimits { get; } = [];

    private const string NoUsageYet = "Appears after the first answer.";
    private const double ContextWarningFraction = 0.8;

    [Reactive] private string _draft = string.Empty;
    [Reactive] private bool _isBusy;
    [Reactive] private string _statusText = string.Empty;
    [Reactive] private string _model = string.Empty;
    [Reactive] private string _objectFilter = string.Empty;

    /// <summary>How full the model's context is for this chat, 0 to 1; drives the ring next to the model button.</summary>
    [Reactive] private double _contextFraction;

    /// <summary>"35.1k / 1.0M · 3%", or a hint until the first answer reports it.</summary>
    [Reactive] private string _contextText = NoUsageYet;

    /// <summary>True when a usage window is not "allowed", extra usage is being spent or the context is nearly full.</summary>
    [Reactive] private bool _usageNeedsAttention;

    /// <summary>What the latest answer consumed, empty until the first answer.</summary>
    [Reactive] private string _lastAnswerText = string.Empty;

    /// <summary>What the answers of this session consumed so far, empty until the first answer.</summary>
    [Reactive] private string _sessionUsageText = string.Empty;

    /// <summary>Title of the conversation being shown ("New chat" until its first question names it).</summary>
    [Reactive] private string _chatTitle = AiChatTitles.Default;

    /// <summary>True when Cmd/Ctrl+Enter sends and Enter adds a line; false (default) when Enter sends.</summary>
    [Reactive] private bool _sendWithModifier;

    public ReactiveCommand<Unit, Unit> SendCommand { get; }
    public ReactiveCommand<Unit, Unit> StopCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearCommand { get; }
    public ReactiveCommand<Unit, Unit> NewChatCommand { get; }
    public ReactiveCommand<Unit, Unit> AttachWholeQueryCommand { get; }
    public ReactiveCommand<Unit, Unit> AttachSelectionCommand { get; }

    /// <summary>The key that sends together with Enter on this platform: Cmd on macOS, Ctrl elsewhere.</summary>
    public static string ModifierName => OperatingSystem.IsMacOS() ? "Cmd+" : "Ctrl+";

    public string InputHint => SendWithModifier
        ? $"Ask something... ({ModifierName}Enter sends, Enter adds a line)"
        : "Ask something... (Enter sends, Shift+Enter adds a line)";

    /// <summary>Forget the cached object list (after a schema refresh).</summary>
    public void InvalidateSchema()
    {
        _contextBuilder.Invalidate();
        _allObjects = [];

        // Structures the conversation already holds may be out of date now, so the next message describes them again.
        _redescribeSchema = true;
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

    private void AttachCurrentQuery(bool selectionOnly)
    {
        var query = CurrentQueryProvider?.Invoke(selectionOnly);
        if (query is not { } current || string.IsNullOrWhiteSpace(current.Text))
        {
            StatusText = selectionOnly ? "Select some text in the query first." : "The current query is empty.";
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

    /// <summary>Reloads the picker's list; call when it opens so the times and counts are current.</summary>
    public void RefreshChats()
    {
        var now = DateTime.UtcNow;
        Chats.Clear();
        foreach (var chat in _historyRepository.ListChats(_connection.Id))
            Chats.Add(new AiChatListItem(chat, chat.Id == _chatId, now, SwitchToChat, RenameChat, DeleteChat));
    }

    // Shows a conversation (null = a brand new one that is only stored when its first question is sent).
    private void LoadChat(AiChatInfo? chat)
    {
        _isApplyingChat = true;
        _chatId = chat?.Id;
        _sessionStarted = chat?.SessionStarted ?? false;
        ChatTitle = chat?.Title ?? AiChatTitles.Default;
        Model = !string.IsNullOrWhiteSpace(chat?.Model) ? chat.Model
            : !string.IsNullOrWhiteSpace(_settingsRepository.Load(ProviderType).Model) ? _settingsRepository.Load(ProviderType).Model!
            : DefaultModel;
        _isApplyingChat = false;

        _history.Clear();
        Messages.Clear();
        Attachments.Clear();
        StatusText = string.Empty;

        // The context belongs to the conversation, so it starts over until the next answer reports it.
        _lastUsage = null;
        RefreshUsage();

        if (chat is not null)
        {
            foreach (var message in _historyRepository.LoadMessages(chat.Id, MaxLoadedMessages))
            {
                _history.Add(message);
                Messages.Add(new AiChatMessageViewModel(message.Role, message.Content, false, InsertCode));
            }
        }

        this.RaisePropertyChanged(nameof(HasMessages));
    }

    private void StartNewChat()
    {
        // An empty conversation is already a blank page.
        if (_chatId is null || _history.Count == 0)
        {
            if (_chatId is null)
                LoadChat(null);
            return;
        }

        LoadChat(null);
        RefreshChats();
    }

    private void SwitchToChat(AiChatListItem item)
    {
        if (IsBusy || item.Id == _chatId)
            return;

        LoadChat(_historyRepository.ListChats(_connection.Id).FirstOrDefault(chat => chat.Id == item.Id));
        RefreshChats();
    }

    private void RenameChat(AiChatListItem item)
    {
        if (IsBusy && item.Id == _chatId)
            return;

        _historyRepository.RenameChat(item.Id, item.EditTitle);
        if (item.Id == _chatId)
            ChatTitle = AiChatTitles.FromText(item.EditTitle);

        RefreshChats();
    }

    private void DeleteChat(AiChatListItem item)
    {
        if (IsBusy && item.Id == _chatId)
            return;

        _historyRepository.DeleteChat(item.Id);
        ForgetCliSession(item.Id);
        if (item.Id == _chatId)
            LoadChat(_historyRepository.ListChats(_connection.Id).FirstOrDefault());

        RefreshChats();
    }

    private void ClearHistory()
    {
        if (_chatId is { } chatId)
        {
            // The CLI session remembers the messages that are being cleared, so it has to go too.
            _historyRepository.Clear(chatId);
            ForgetCliSession(chatId);
            _sessionStarted = false;
        }

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

        // Structures already in the conversation (in an earlier message the model saw) are not sent again.
        var knownStructure = _redescribeSchema
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : AiSqlContextBuilder.FindDescribedObjects(_history.Select(m => m.ModelContent));
        _redescribeSchema = false;

        // The question shows at once; it is stored when the model's view of it is known.
        var userMessage = new AiChatMessage(AiChatRole.User, historyText, DateTime.UtcNow);
        Messages.Add(new AiChatMessageViewModel(AiChatRole.User, historyText, false, InsertCode));
        this.RaisePropertyChanged(nameof(HasMessages));
        var remembered = false;

        IsBusy = true;
        _cancellation = new CancellationTokenSource();
        var cancellationToken = _cancellation.Token;

        try
        {
            var settings = _settingsRepository.Load(ProviderType);
            if (!string.IsNullOrWhiteSpace(Model))
                settings.Model = Model.Trim();

            var provider = _providerFactory.Create(settings);

            // Tables mentioned or attached in recent turns stay relevant, so their structure keeps being offered.
            var recentText = string.Join("\n", previous.TakeLast(RelevantHistoryMessages).Select(m => m.ModelContent ?? m.Content));
            var relevantText = text + "\n" + string.Join("\n", files.Select(f => f.Content)) + "\n" + recentText;

            // The system prompt is the same on every call (the CLI caches it); the structure of the relevant objects
            // goes with the message instead, and only what the conversation doesn't have yet.
            var systemPrompt = await _contextBuilder.BuildStableSystemPromptAsync(cancellationToken);
            var newStructure = await _contextBuilder.BuildStructureContextAsync(relevantText, objects, knownStructure, cancellationToken);
            var prompt = AiPromptComposer.Compose(text, files, newStructure);

            // A call that starts from nothing (a new session, a lost one, or no session at all) needs every structure.
            var freshPrompt = knownStructure.Count == 0
                ? prompt
                : AiPromptComposer.Compose(text, files, await _contextBuilder.BuildStructureContextAsync(relevantText, objects, null, cancellationToken));

            // Keep what the model saw, so later turns and a rebuilt session still have the attachments and structures.
            var modelContent = files.Count > 0 || newStructure.Length > 0 ? prompt : null;
            Remember(userMessage with { ModelContent = modelContent });
            remembered = true;
            NameChatAfterFirstQuestion(text);

            var session = CreateSession();
            var request = new AiChatRequest(session is null ? freshPrompt : prompt, systemPrompt, previous, session, freshPrompt);
            var response = await provider.SendMessageAsync(request, cancellationToken);
            RecordUsage(response);

            if (response.Success)
            {
                if (session is not null && !_sessionStarted)
                {
                    _sessionStarted = true;
                    _historyRepository.SetSessionStarted(session.Id, true);
                }

                AddMessage(new AiChatMessage(AiChatRole.Assistant, response.Content, DateTime.UtcNow), persist: true);
            }
            else
            {
                AddError($"{response.ErrorMessage} (Check Preferences > AI Assistant.)");
            }
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
            if (!remembered)
            {
                Remember(userMessage);
                NameChatAfterFirstQuestion(text);
            }

            IsBusy = false;
            _cancellation.Dispose();
            _cancellation = null;
        }
    }

    // The CLI keeps the conversation under the chat's id, in a folder of its own for the connection.
    private AiSession? CreateSession()
    {
        var folder = _providerFactory.GetSessionFolder(_connection.Id);
        return folder is null || _chatId is not { } chatId ? null : new AiSession(chatId, _sessionStarted, folder);
    }

    private void ForgetCliSession(Guid chatId)
    {
        try
        {
            _providerFactory.Create(_settingsRepository.Load(ProviderType)).ForgetSession(chatId);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The stored session is only a cache of the conversation; failing to remove it must not block the user.
        }
    }

    private void NameChatAfterFirstQuestion(string text)
    {
        if (_chatId is not { } chatId || ChatTitle != AiChatTitles.Default)
            return;

        var title = AiChatTitles.FromText(text);
        _historyRepository.RenameChat(chatId, title);
        ChatTitle = title;
        RefreshChats();
    }

    // Keeps the running total of the session, the size of the context and the latest state of each usage window the CLI reported.
    private void RecordUsage(AiChatResponse response)
    {
        if (response.Usage is { } usage)
        {
            _sessionUsage = _sessionUsage is null ? usage : _sessionUsage.Add(usage);
            _lastUsage = usage;
            _contextWindow = usage.ContextWindow ?? _contextWindow;
        }

        if (response.RateLimits is { Count: > 0 } limits)
        {
            foreach (var limit in limits)
                _rateLimits[limit.WindowType ?? string.Empty] = limit;
        }

        RefreshUsage();
    }

    private void RefreshUsage()
    {
        if (_lastUsage is { } last)
        {
            ContextFraction = AiUsageFormatter.ContextFraction(last.ContextTokens, _contextWindow);
            ContextText = _contextWindow is { } window
                ? AiUsageFormatter.DescribeContext(last.ContextTokens, window)
                : $"{AiUsageFormatter.FormatTokens(last.ContextTokens)} tokens";
            LastAnswerText = "Last answer: " + AiUsageFormatter.Describe(last);
        }
        else
        {
            ContextFraction = 0;
            ContextText = NoUsageYet;
            LastAnswerText = string.Empty;
        }

        SessionUsageText = _sessionUsage is { } session ? "This session: " + AiUsageFormatter.Describe(session) : string.Empty;

        var now = DateTime.Now;
        UsageLimits.Clear();
        AddLimitRow("five_hour", now);
        AddLimitRow("seven_day", now);
        foreach (var other in _rateLimits.Values.Where(l => l.WindowType is not ("five_hour" or "seven_day")).OrderBy(l => l.WindowType))
            UsageLimits.Add(new AiUsageLimitItem(AiUsageFormatter.DescribeWindow(other.WindowType), AiUsageFormatter.DescribeRateLimitState(other, now), !other.IsAllowed || other.IsUsingOverage));

        UsageNeedsAttention = ContextFraction >= ContextWarningFraction || UsageLimits.Any(l => l.IsWarning);
    }

    private void AddLimitRow(string windowType, DateTime now)
    {
        var label = AiUsageFormatter.DescribeWindow(windowType);
        if (_rateLimits.TryGetValue(windowType, out var limit))
            UsageLimits.Add(new AiUsageLimitItem(label, AiUsageFormatter.DescribeRateLimitState(limit, now), !limit.IsAllowed || limit.IsUsingOverage));
        else
            UsageLimits.Add(new AiUsageLimitItem(label, _rateLimits.Count == 0 ? NoUsageYet : "Not reported by the CLI.", false));
    }

    // Adds the message to the conversation the model sees and stores it, creating the chat on first use.
    private void Remember(AiChatMessage message)
    {
        _history.Add(message);
        _chatId ??= _historyRepository.CreateChat(_connection.Id, model: Model).Id;
        _historyRepository.Append(_chatId.Value, message);
    }

    private void AddMessage(AiChatMessage message, bool persist)
    {
        if (persist)
            Remember(message);
        else
            _history.Add(message);

        Messages.Add(new AiChatMessageViewModel(message.Role, message.Content, false, InsertCode));
        this.RaisePropertyChanged(nameof(HasMessages));
    }

    private void AddError(string text)
    {
        Messages.Add(new AiChatMessageViewModel(AiChatRole.Assistant, text, true, null));
        this.RaisePropertyChanged(nameof(HasMessages));
    }
}

/// <summary>A model the picker offers: the id sent to the CLI and the name shown to the user.</summary>
public sealed record AiModelOption(string Id, string Label, bool IsAlias = false);
