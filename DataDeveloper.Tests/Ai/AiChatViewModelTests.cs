using DataDeveloper.Services;
using Microsoft.Data.Sqlite;
using DataDeveloper.Data.Ai;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Models;
using DataDeveloper.Data.Providers.SqLite;
using DataDeveloper.Interfaces;
using DataDeveloper.ViewModels;
using Xunit;

namespace DataDeveloper.Tests.Ai;

public class AiChatViewModelTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("ai-vm").FullName;
    private readonly ScriptedRunner _runner = new();
    private readonly SqliteAiChatHistoryRepository _history;
    private readonly SqLiteConnectionSettings _connection = new() { Id = Guid.NewGuid(), Name = "test", Database = "test.db" };

    public AiChatViewModelTests() => _history = new SqliteAiChatHistoryRepository(Path.Combine(_folder, "chat.db"));

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_folder, recursive: true);
    }

    private IReadOnlyList<AiChatMessage> StoredMessages() =>
        _history.ListChats(_connection.Id) is [var chat, ..] ? _history.LoadMessages(chat.Id) : [];

    private string SessionsRoot => Path.Combine(_folder, "sessions");

    private AiChatViewModel CreateViewModel(bool withSessions = false, IAiSchemaSource? source = null)
    {
        var executable = Path.Combine(_folder, "claude");
        File.WriteAllText(executable, string.Empty);

        return new AiChatViewModel(
            _connection,
            new AiProviderFactoryService(_runner, withSessions ? SessionsRoot : null),
            new FixedSettings(new AiProviderSettings { ExecutablePath = executable }),
            _history,
            new AiSqlContextBuilder(source ?? new EmptySource(), DatabaseType.SqLite));
    }

    private const string AnswerWithUsage = """
        {"type":"rate_limit_event","rate_limit_info":{"status":"allowed","resetsAt":1793242800,"rateLimitType":"five_hour"}}
        {"type":"result","is_error":false,"result":"use a join","total_cost_usd":0.01,"usage":{"input_tokens":1000,"cache_read_input_tokens":99000,"cache_creation_input_tokens":0,"output_tokens":0},"modelUsage":{"claude-sonnet-5":{"inputTokens":1000,"cacheReadInputTokens":99000,"outputTokens":0,"contextWindow":1000000}}}
        """;

    [Fact]
    public async Task Ask_FillsTheContextRingAndTheUsagePopover()
    {
        _runner.Answer(AnswerWithUsage);
        _runner.Answer(AnswerWithUsage);
        var viewModel = CreateViewModel();

        Assert.Equal(0, viewModel.ContextFraction);
        Assert.Equal(2, viewModel.UsageLimits.Count);

        await viewModel.AskAsync("first");
        await viewModel.AskAsync("second");

        Assert.Equal(0.1, viewModel.ContextFraction, 3);
        Assert.Equal("100k / 1.0M \u00B7 10%", viewModel.ContextText);
        Assert.False(viewModel.UsageNeedsAttention);

        Assert.Equal(["Session (5h)", "Weekly"], viewModel.UsageLimits.Select(l => l.Label));
        Assert.StartsWith("within the limit", viewModel.UsageLimits[0].Detail);
        Assert.Equal("Not reported by the CLI.", viewModel.UsageLimits[1].Detail);

        Assert.StartsWith("Last answer: claude-sonnet-5 \u00B7 100k in (99.0k cached)", viewModel.LastAnswerText);
        Assert.StartsWith("This session: claude-sonnet-5 \u00B7 200k in", viewModel.SessionUsageText);
    }

    [Fact]
    public async Task Ask_FlagsALimitedUsageWindow()
    {
        _runner.Answer("""
            {"type":"rate_limit_event","rate_limit_info":{"status":"rejected","rateLimitType":"five_hour"}}
            {"type":"result","is_error":true,"result":"limit reached"}
            """);
        var viewModel = CreateViewModel();

        await viewModel.AskAsync("hello");

        Assert.True(viewModel.UsageNeedsAttention);
        Assert.True(viewModel.UsageLimits[0].IsWarning);
        Assert.Contains("rejected", viewModel.UsageLimits[0].Detail);
    }

    [Fact]
    public async Task NewChat_ResetsTheContextButKeepsTheSessionTotal()
    {
        _runner.Answer(AnswerWithUsage);
        var viewModel = CreateViewModel();
        await viewModel.AskAsync("first");

        viewModel.NewChatCommand.Execute().Subscribe();

        Assert.Equal(0, viewModel.ContextFraction);
        Assert.Equal("Appears after the first answer.", viewModel.ContextText);
        Assert.Empty(viewModel.LastAnswerText);
        Assert.StartsWith("This session:", viewModel.SessionUsageText);
    }

    [Fact]
    public void NewChat_StartsOnSonnetUnlessPreferencesChooseAnotherModel()
    {
        Assert.Equal("sonnet", CreateViewModel().Model);
    }

    private const string PlainAnswer = """{"type":"result","is_error":false,"result":"ok"}""";

    [Fact]
    public async Task Session_IsCreatedOnTheFirstQuestionAndResumedAfterwards()
    {
        _runner.Answer(PlainAnswer);
        _runner.Answer(PlainAnswer);
        var viewModel = CreateViewModel(withSessions: true);

        await viewModel.AskAsync("first question");
        await viewModel.AskAsync("second question");

        var chat = _history.ListChats(_connection.Id).Single();
        Assert.True(chat.SessionStarted);

        var first = _runner.Requests[0];
        Assert.Equal(chat.Id.ToString(), first.Arguments[first.Arguments.ToList().IndexOf("--session-id") + 1]);
        Assert.Equal(Path.Combine(SessionsRoot, _connection.Id.ToString("N")), first.WorkingDirectory);

        var second = _runner.Requests[1];
        Assert.Equal(chat.Id.ToString(), second.Arguments[second.Arguments.ToList().IndexOf("--resume") + 1]);
        Assert.Equal("second question", second.StandardInput);
    }

    [Fact]
    public async Task Session_StartsFromTheStoredConversationWhenTheChatHadNoSessionYet()
    {
        var chat = _history.CreateChat(_connection.Id);
        _history.Append(chat.Id, new AiChatMessage(AiChatRole.User, "old question", DateTime.UtcNow));
        _history.Append(chat.Id, new AiChatMessage(AiChatRole.Assistant, "old answer", DateTime.UtcNow));
        _runner.Answer(PlainAnswer);
        var viewModel = CreateViewModel(withSessions: true);

        await viewModel.AskAsync("follow up");

        var call = Assert.Single(_runner.Requests);
        Assert.Contains("--session-id", call.Arguments);
        Assert.Contains("old answer", call.StandardInput);
        Assert.True(_history.ListChats(_connection.Id).Single().SessionStarted);
    }

    [Fact]
    public async Task Session_ClearingTheChatStartsANewOne()
    {
        _runner.Answer(PlainAnswer);
        _runner.Answer(PlainAnswer);
        var viewModel = CreateViewModel(withSessions: true);
        await viewModel.AskAsync("first question");

        viewModel.ClearCommand.Execute().Subscribe();
        Assert.False(_history.ListChats(_connection.Id).Single().SessionStarted);

        await viewModel.AskAsync("after clearing");

        Assert.Contains("--session-id", _runner.Requests[1].Arguments);
        Assert.DoesNotContain("first question", _runner.Requests[1].StandardInput);
    }

    [Fact]
    public async Task Session_ANewChatHasASessionOfItsOwn()
    {
        _runner.Answer(PlainAnswer);
        _runner.Answer(PlainAnswer);
        var viewModel = CreateViewModel(withSessions: true);
        await viewModel.AskAsync("task A");

        viewModel.NewChatCommand.Execute().Subscribe();
        await viewModel.AskAsync("task B");

        var ids = _runner.Requests.Select(r => r.Arguments[r.Arguments.ToList().IndexOf("--session-id") + 1]).ToList();
        Assert.Equal(2, ids.Distinct().Count());
    }

    [Fact]
    public async Task Structure_IsSentOncePerConversationAndAgainAfterASchemaChange()
    {
        _runner.Answer(PlainAnswer);
        _runner.Answer(PlainAnswer);
        _runner.Answer(PlainAnswer);
        var viewModel = CreateViewModel(withSessions: true, new TableSource());

        await viewModel.AskAsync("how many rows does Orders have?");
        await viewModel.AskAsync("and which Orders are late?");
        viewModel.InvalidateSchema();
        await viewModel.AskAsync("what about Orders again?");

        Assert.Contains("Table Orders:", _runner.Requests[0].StandardInput);
        Assert.Contains("amount decimal", _runner.Requests[0].StandardInput);
        Assert.DoesNotContain("Table Orders:", _runner.Requests[1].StandardInput);
        Assert.Contains("Table Orders:", _runner.Requests[2].StandardInput);

        // The system prompt carries no per-message structure, so it stays the same and cacheable.
        string SystemPrompt(CliProcessRequest r) => r.Arguments[r.Arguments.ToList().IndexOf("--system-prompt") + 1];
        Assert.Equal(SystemPrompt(_runner.Requests[0]), SystemPrompt(_runner.Requests[1]));
        Assert.DoesNotContain("amount decimal", SystemPrompt(_runner.Requests[0]));
    }

    [Fact]
    public async Task Structure_WithoutASessionStillGoesWithEveryMessage()
    {
        _runner.Answer(PlainAnswer);
        _runner.Answer(PlainAnswer);
        var viewModel = CreateViewModel(withSessions: false, new TableSource());

        await viewModel.AskAsync("how many rows does Orders have?");
        await viewModel.AskAsync("and which Orders are late?");

        Assert.Contains("--no-session-persistence", _runner.Requests[1].Arguments);
        Assert.EndsWith("Table Orders:\n  - id int, primary key, not null\n  - amount decimal\n\nand which Orders are late?", _runner.Requests[1].StandardInput!.Replace("\r\n", "\n"));
    }

    [Fact]
    public async Task Ask_AddsUserAndAssistantMessagesAndPersistsThem()
    {
        _runner.Answer("""{"result":"use a join"}""");
        var viewModel = CreateViewModel();

        await viewModel.AskAsync("how do I combine tables?");

        Assert.Equal(["how do I combine tables?", "use a join"], viewModel.Messages.Select(m => m.Content));
        Assert.True(viewModel.Messages[0].IsUser);
        Assert.True(viewModel.Messages[1].IsAssistant);
        Assert.Equal(2, StoredMessages().Count);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task Ask_NamesANewChatAfterItsFirstQuestionAndKeepsTheNameAfterwards()
    {
        _runner.Answer("""{"result":"a"}""");
        _runner.Answer("""{"result":"b"}""");
        var viewModel = CreateViewModel();
        Assert.Equal(AiChatTitles.Default, viewModel.ChatTitle);

        await viewModel.AskAsync("find duplicate customers");
        await viewModel.AskAsync("and now by email");

        Assert.Equal("find duplicate customers", viewModel.ChatTitle);
        Assert.Equal("find duplicate customers", Assert.Single(_history.ListChats(_connection.Id)).Title);
        Assert.Equal("find duplicate customers", Assert.Single(viewModel.Chats).Title);
    }

    [Fact]
    public async Task AttachedFiles_AreStillSentOnLaterTurnsButShownOnlyByName()
    {
        _runner.Answer("""{"result":"first"}""");
        _runner.Answer("""{"result":"second"}""");
        var path = Path.Combine(_folder, "notes.sql");
        File.WriteAllText(path, "select secret_column from t");
        var viewModel = CreateViewModel();

        await viewModel.AttachFileAsync(path);
        await viewModel.AskAsync("what does the file do?");
        await viewModel.AskAsync("and is it safe?");

        Assert.Contains("secret_column", _runner.Requests[1].StandardInput!);
        Assert.DoesNotContain("secret_column", viewModel.Messages[0].Content);
        Assert.Contains("(Attached: notes.sql)", viewModel.Messages[0].Content);

        var reopened = CreateViewModel();
        Assert.DoesNotContain("secret_column", reopened.Messages[0].Content);
        Assert.Contains("secret_column", _history.LoadMessages(_history.ListChats(_connection.Id).Single().Id)[0].ModelContent);
    }

    [Fact]
    public async Task NewChat_StartsABlankChatAndTheOldOneStaysInTheList()
    {
        _runner.Answer("""{"result":"a"}""");
        _runner.Answer("""{"result":"b"}""");
        var viewModel = CreateViewModel();
        await viewModel.AskAsync("task A question");

        viewModel.NewChatCommand.Execute().Subscribe();
        Assert.Empty(viewModel.Messages);
        Assert.Equal(AiChatTitles.Default, viewModel.ChatTitle);

        await viewModel.AskAsync("task B question");

        Assert.Equal(["task B question", "task A question"], viewModel.Chats.Select(c => c.Title));
        Assert.Equal(2, _history.ListChats(_connection.Id).Count);
    }

    [Fact]
    public async Task NewChat_DoesNotSendTheOtherChatsMessagesAsContext()
    {
        _runner.Answer("""{"result":"a"}""");
        _runner.Answer("""{"result":"b"}""");
        var viewModel = CreateViewModel();
        await viewModel.AskAsync("secret of task A");

        viewModel.NewChatCommand.Execute().Subscribe();
        await viewModel.AskAsync("task B question");

        Assert.DoesNotContain("secret of task A", _runner.Requests[1].StandardInput!);
    }

    [Fact]
    public async Task SwitchingChats_RestoresTheirMessagesAndModel()
    {
        _runner.Answer("""{"result":"answer A"}""");
        _runner.Answer("""{"result":"answer B"}""");
        var viewModel = CreateViewModel();
        viewModel.Model = "opus";
        await viewModel.AskAsync("question A");
        viewModel.NewChatCommand.Execute().Subscribe();
        viewModel.Model = "haiku";
        await viewModel.AskAsync("question B");

        var chatA = viewModel.Chats.Single(c => c.Title == "question A");
        chatA.SelectCommand.Execute().Subscribe();

        Assert.Equal(["question A", "answer A"], viewModel.Messages.Select(m => m.Content));
        Assert.Equal("opus", viewModel.Model);
        Assert.Equal("question A", viewModel.ChatTitle);
        Assert.True(viewModel.Chats.Single(c => c.Title == "question A").IsCurrent);

        viewModel.Chats.Single(c => c.Title == "question B").SelectCommand.Execute().Subscribe();
        Assert.Equal("haiku", viewModel.Model);
    }

    [Fact]
    public async Task PickingAModel_IsRememberedForThatChatOnly()
    {
        _runner.Answer("""{"result":"a"}""");
        var viewModel = CreateViewModel();
        await viewModel.AskAsync("question");

        viewModel.Model = "sonnet";

        Assert.Equal("sonnet", Assert.Single(_history.ListChats(_connection.Id)).Model);
    }

    [Fact]
    public async Task RenamingAChat_UpdatesTheListAndTheTitleWhenItIsTheCurrentOne()
    {
        _runner.Answer("""{"result":"a"}""");
        var viewModel = CreateViewModel();
        await viewModel.AskAsync("question");

        var item = viewModel.Chats.Single();
        item.BeginRenameCommand.Execute().Subscribe();
        item.EditTitle = "Task A: orders";
        item.CommitRenameCommand.Execute().Subscribe();

        Assert.Equal("Task A: orders", viewModel.ChatTitle);
        Assert.Equal("Task A: orders", viewModel.Chats.Single().Title);
        Assert.Equal("Task A: orders", _history.ListChats(_connection.Id).Single().Title);
    }

    [Fact]
    public async Task DeletingTheCurrentChat_FallsBackToTheNextMostRecentOne()
    {
        _runner.Answer("""{"result":"a"}""");
        _runner.Answer("""{"result":"b"}""");
        var viewModel = CreateViewModel();
        await viewModel.AskAsync("older");
        viewModel.NewChatCommand.Execute().Subscribe();
        await viewModel.AskAsync("newer");

        var current = viewModel.Chats.Single(c => c.IsCurrent);
        current.AskDeleteCommand.Execute().Subscribe();
        current.ConfirmDeleteCommand.Execute().Subscribe();

        Assert.Equal("older", viewModel.ChatTitle);
        Assert.Equal(["older", "a"], viewModel.Messages.Select(m => m.Content));
        Assert.Equal("older", Assert.Single(viewModel.Chats).Title);
    }

    [Fact]
    public async Task DeletingTheOnlyChat_LeavesABlankNewChat()
    {
        _runner.Answer("""{"result":"a"}""");
        var viewModel = CreateViewModel();
        await viewModel.AskAsync("question");

        var item = viewModel.Chats.Single();
        item.ConfirmDeleteCommand.Execute().Subscribe();

        Assert.Empty(viewModel.Messages);
        Assert.Equal(AiChatTitles.Default, viewModel.ChatTitle);
        Assert.Empty(viewModel.Chats);
        Assert.Empty(_history.ListChats(_connection.Id));
    }

    [Fact]
    public async Task ANewViewModel_ReopensTheMostRecentlyActiveChat()
    {
        _runner.Answer("""{"result":"a"}""");
        _runner.Answer("""{"result":"b"}""");
        var first = CreateViewModel();
        await first.AskAsync("older");
        first.NewChatCommand.Execute().Subscribe();
        await first.AskAsync("newer");

        var reopened = CreateViewModel();

        Assert.Equal("newer", reopened.ChatTitle);
        Assert.Equal(["newer", "b"], reopened.Messages.Select(m => m.Content));
    }

    [Theory]
    [InlineData(10, "just now")]
    [InlineData(5 * 60, "5 min ago")]
    [InlineData(3 * 3600, "3 h ago")]
    [InlineData(30 * 3600, "yesterday")]
    [InlineData(5 * 86400, "5 days ago")]
    public void FormatAge_DescribesHowLongAgo(int seconds, string expected) =>
        Assert.Equal(expected, AiChatListItem.FormatAge(TimeSpan.FromSeconds(seconds)));

    [Fact]
    public async Task Ask_SendsEarlierTurnsAsContextAndTheSchemaAsSystemPrompt()
    {
        _runner.Answer("""{"result":"first"}""");
        _runner.Answer("""{"result":"second"}""");
        var viewModel = CreateViewModel();

        await viewModel.AskAsync("one");
        await viewModel.AskAsync("two");

        var stdin = _runner.Requests[1].StandardInput!;
        Assert.Contains("User: one", stdin);
        Assert.Contains("Assistant: first", stdin);
        Assert.EndsWith("User: two", stdin);
        var arguments = _runner.Requests[1].Arguments.ToList();
        Assert.Contains("SQLite", arguments[arguments.IndexOf("--system-prompt") + 1]);
    }

    [Fact]
    public async Task Ask_ShowsProviderErrorsWithoutPersistingThem()
    {
        _runner.Answer("""{"is_error":true,"result":"Failed to authenticate"}""", exitCode: 1);
        var viewModel = CreateViewModel();

        await viewModel.AskAsync("hello");

        var error = viewModel.Messages[^1];
        Assert.True(error.IsError);
        Assert.Contains("Failed to authenticate", error.Content);
        Assert.Single(StoredMessages());
    }

    [Fact]
    public async Task Ask_IncludesTextAttachmentsOnceAndClearsThem()
    {
        _runner.Answer("""{"result":"ok"}""");
        var viewModel = CreateViewModel();

        await viewModel.AskAsync("review", [viewModel.CreateTextAttachment("q.sql", "select * from t")]);

        Assert.Contains("select * from t", _runner.Requests[0].StandardInput);
        Assert.Empty(viewModel.Attachments);
        Assert.Contains("(Attached: q.sql)", viewModel.Messages[0].Content);
        Assert.DoesNotContain("select * from t", viewModel.Messages[0].Content);
    }

    [Fact]
    public async Task Constructor_RestoresThePersistedConversation()
    {
        var chat = _history.CreateChat(_connection.Id);
        _history.Append(chat.Id, new AiChatMessage(AiChatRole.User, "old question", DateTime.UtcNow));
        _history.Append(chat.Id, new AiChatMessage(AiChatRole.Assistant, "old answer", DateTime.UtcNow));

        var viewModel = CreateViewModel();

        Assert.True(viewModel.HasMessages);
        Assert.Equal(["old question", "old answer"], viewModel.Messages.Select(m => m.Content));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Clear_RemovesTheConversationFromScreenAndStorage()
    {
        _runner.Answer("""{"result":"ok"}""");
        var viewModel = CreateViewModel();
        await viewModel.AskAsync("hello");

        viewModel.ClearCommand.Execute().Subscribe();

        Assert.Empty(viewModel.Messages);
        Assert.Empty(StoredMessages());
        Assert.False(viewModel.HasMessages);
    }

    [Fact]
    public async Task AttachFile_AcceptsTextAndRejectsBinary()
    {
        var text = Path.Combine(_folder, "notes.sql");
        var binary = Path.Combine(_folder, "data.bin");
        File.WriteAllText(text, "select 1");
        File.WriteAllBytes(binary, [1, 0, 2]);
        var viewModel = CreateViewModel();

        await viewModel.AttachFileAsync(text);
        await viewModel.AttachFileAsync(binary);

        var attachment = Assert.Single(viewModel.Attachments);
        Assert.Equal("notes.sql", attachment.Name);
        Assert.Contains("not a text file", viewModel.StatusText);
    }

    [Fact]
    public void AttachObjects_IgnoresDuplicatesAndRemoveWorks()
    {
        var viewModel = CreateViewModel();
        var orders = new DbObjectRef(DbObjectKind.Table, "dbo", "Orders");

        viewModel.AttachObjects([orders, orders]);

        var item = Assert.Single(viewModel.Attachments);
        item.RemoveCommand.Execute().Subscribe();
        Assert.Empty(viewModel.Attachments);
    }

    [Fact]
    public void InsertCode_UsesTheEditorHookOrExplainsWhyNot()
    {
        var viewModel = CreateViewModel();
        string? inserted = null;

        viewModel.InsertCode("select 1");
        Assert.Contains("query tab", viewModel.StatusText);

        viewModel.InsertIntoEditor = code => { inserted = code; return true; };
        viewModel.InsertCode("select 2");
        Assert.Equal("select 2", inserted);
    }

    [Fact]
    public void AssessRisk_FlagsScriptsThatCanLoseData_OnlyFromTheModelsAnswer()
    {
        var viewModel = CreateViewModel();

        Assert.Null(viewModel.AssessRisk("select * from orders where id = 1"));
        Assert.Contains("DROP", viewModel.AssessRisk("drop table orders")!);
        Assert.Contains("every row", viewModel.AssessRisk("update orders set total = 0")!);
    }

    [Fact]
    public void RiskyCode_NeedsASecondClickToBeInserted_AndCanBeCancelled()
    {
        var inserted = new List<string>();
        var segment = new AiMessageSegmentViewModel(new AiMessageSegment(true, "delete from orders", "sql"), inserted.Add, _ => "careful");

        Assert.True(segment.HasRisk);

        segment.InsertCommand.Execute().Subscribe();
        Assert.True(segment.IsConfirmingInsert);
        Assert.Empty(inserted);

        segment.CancelInsertCommand.Execute().Subscribe();
        Assert.False(segment.IsConfirmingInsert);
        Assert.Empty(inserted);

        segment.InsertCommand.Execute().Subscribe();
        segment.InsertCommand.Execute().Subscribe();
        Assert.Equal(["delete from orders"], inserted);
        Assert.False(segment.IsConfirmingInsert);
    }

    [Fact]
    public void SafeCode_IsInsertedWithASingleClick()
    {
        var inserted = new List<string>();
        var segment = new AiMessageSegmentViewModel(new AiMessageSegment(true, "select 1", "sql"), inserted.Add, _ => null);

        Assert.False(segment.HasRisk);
        segment.InsertCommand.Execute().Subscribe();

        Assert.Equal(["select 1"], inserted);
    }

    [Fact]
    public void AttachQuery_AsksTheProviderForTheWholeQueryOrTheSelection()
    {
        var viewModel = CreateViewModel();
        viewModel.CurrentQueryProvider = selectionOnly =>
            selectionOnly ? ("Query 2 (selection)", "select 42") : ("Query 2", "select 42; select 43");

        viewModel.AttachWholeQueryCommand.Execute().Subscribe();
        viewModel.AttachSelectionCommand.Execute().Subscribe();

        Assert.Equal(["Query 2", "Query 2 (selection)"], viewModel.Attachments.Select(a => a.Name));
        Assert.Equal("select 42; select 43", viewModel.Attachments[0].TextContent);
    }

    [Fact]
    public void AttachSelection_ExplainsWhenNothingIsSelected()
    {
        var viewModel = CreateViewModel();
        viewModel.CurrentQueryProvider = _ => null;

        viewModel.AttachSelectionCommand.Execute().Subscribe();

        Assert.Empty(viewModel.Attachments);
        Assert.Contains("Select some text", viewModel.StatusText);
    }

    [Fact]
    public void SendWithModifier_IsRestoredAndPersistedAndChangesTheHint()
    {
        var settings = new FixedSettings(new AiProviderSettings());
        settings.SaveSendWithModifier(true);
        var viewModel = new AiChatViewModel(_connection, new AiProviderFactoryService(_runner), settings, _history,
            new AiSqlContextBuilder(new EmptySource(), DatabaseType.SqLite));

        Assert.True(viewModel.SendWithModifier);
        Assert.Contains("Enter adds a line", viewModel.InputHint);

        viewModel.SendWithModifier = false;

        Assert.False(settings.SendWithModifier);
        Assert.Contains("Shift+Enter adds a line", viewModel.InputHint);
    }

    [Fact]
    public void ModelLabel_ShowsTheFriendlyNameForAliasesAndTheRawIdOtherwise()
    {
        var viewModel = new AiChatViewModel(_connection, new AiProviderFactoryService(_runner), new FixedSettings(new AiProviderSettings()),
            _history, new AiSqlContextBuilder(new EmptySource(), DatabaseType.SqLite));

        Assert.Equal("Sonnet (latest)", viewModel.ModelLabel);

        viewModel.Model = string.Empty;
        Assert.Equal("CLI default", viewModel.ModelLabel);

        viewModel.Model = "claude-sonnet-5-5";
        Assert.Equal("claude-sonnet-5-5", viewModel.ModelLabel);

        viewModel.Model = "sonnet";
        Assert.Equal("Sonnet (latest)", viewModel.ModelLabel);

        viewModel.Model = "some-custom-model";
        Assert.Equal("some-custom-model", viewModel.ModelLabel);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void ModelMenu_HasAnItemForEveryOptionBeforeItOpens()
    {
        var viewModel = new AiChatViewModel(_connection, new AiProviderFactoryService(_runner), new FixedSettings(new AiProviderSettings()),
            _history, new AiSqlContextBuilder(new EmptySource(), DatabaseType.SqLite));
        var view = new DataDeveloper.Views.AiChatView { DataContext = viewModel };
        var window = new Avalonia.Controls.Window { Content = view };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var button = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(view)
            .OfType<Avalonia.Controls.Button>().First(b => b.Name == "ModelButton");
        var menu = Assert.IsType<Avalonia.Controls.MenuFlyout>(button.Flyout);
        var labels = menu.Items.OfType<Avalonia.Controls.MenuItem>().Select(i => i.Header as string).ToList();

        Assert.Contains("CLI default", labels);
        Assert.Contains("Other model...", labels);
        foreach (var option in AiChatViewModel.ModelOptions)
            Assert.Contains(option.Label, labels);
    }

    private sealed class ScriptedRunner : ICliProcessRunner
    {
        private readonly Queue<CliProcessResult> _answers = new();
        public List<CliProcessRequest> Requests { get; } = [];

        public void Answer(string json, int exitCode = 0) => _answers.Enqueue(new CliProcessResult(true, exitCode, json, string.Empty, false));

        public Task<CliProcessResult> RunAsync(CliProcessRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(_answers.Dequeue());
        }
    }

    private sealed class FixedSettings(AiProviderSettings settings) : IAiSettingsRepository
    {
        public AiProviderSettings Load(AiProviderType providerType) => settings;

        public void Save(AiProviderSettings value) { }

        public bool SendWithModifier { get; private set; }

        public bool LoadSendWithModifier() => SendWithModifier;

        public void SaveSendWithModifier(bool value) => SendWithModifier = value;
    }

    private sealed class TableSource : IAiSchemaSource
    {
        private static readonly DbObjectRef Orders = new(DbObjectKind.Table, null, "Orders");

        public Task<IReadOnlyList<DbObjectRef>> ListTablesAndViewsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DbObjectRef>>([Orders]);

        public Task<IReadOnlyList<ColumnModel>> GetColumnsAsync(DbObjectRef table, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ColumnModel>>(
            [
                new ColumnModel { Name = "id", DataType = "int", IsPrimaryKey = true },
                new ColumnModel { Name = "amount", DataType = "decimal", IsNullable = true }
            ]);

        public Task<TableKeys> GetKeysAsync(DbObjectRef table, CancellationToken cancellationToken = default) =>
            Task.FromResult(new TableKeys());
    }

    private sealed class EmptySource : IAiSchemaSource
    {
        public Task<IReadOnlyList<DbObjectRef>> ListTablesAndViewsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DbObjectRef>>([]);

        public Task<IReadOnlyList<ColumnModel>> GetColumnsAsync(DbObjectRef table, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ColumnModel>>([]);

        public Task<TableKeys> GetKeysAsync(DbObjectRef table, CancellationToken cancellationToken = default) =>
            Task.FromResult(new TableKeys());
    }
}
