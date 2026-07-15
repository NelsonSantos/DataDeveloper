using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using AvaloniaEdit;
using AvaloniaEdit.Search;
using DataDeveloper.Core;
using DataDeveloper.Data.Enums;
using DataDeveloper.EventAggregators;
using DataDeveloper.Interfaces;
using DataDeveloper.NextGrid.UI;
using DataDeveloper.Services;
using DataDeveloper.Views;
using ReactiveUI;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI.Fody.Helpers;

namespace DataDeveloper.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConnectionDialogService _connectionDialogService;
    private readonly IEventAggregatorService _eventAggregatorService;
    private readonly IDialogService _dialogService;
    private readonly IReleaseUpdateService _releaseUpdateService;
    private readonly KeyModifiers _primaryShortcutModifier = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
    private TextEditor? _activeEditor;
    private DatabaseType _activeDatabaseType;
    private NextGridControl? _activeGrid;
    private ClipboardFocusTarget _clipboardFocusTarget = ClipboardFocusTarget.None;

    private enum ClipboardFocusTarget
    {
        None,
        Editor,
        Grid
    }

    public MainWindowViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _connectionDialogService = _serviceProvider.GetRequiredService<IConnectionDialogService>();
        _eventAggregatorService = _serviceProvider.GetRequiredService<IEventAggregatorService>();
        _dialogService = _serviceProvider.GetRequiredService<IDialogService>();
        _releaseUpdateService = _serviceProvider.GetRequiredService<IReleaseUpdateService>();
        
        _eventAggregatorService.Subscribe<ShowCursorDataEvent>(this, ShowCursorDataEvent);
        _eventAggregatorService.Subscribe<ShowExecutionStatusEvent>(this, ShowExecutionStatusEvent);

        this.NewWindowCommand = ReactiveCommand.Create(NewWindow);
        this.NewConnectionCommand = ReactiveCommand.CreateFromTask<StyledElement>(NewConnection);
        this.CloseTabConnectionCommand = ReactiveCommand.CreateFromTask<TabConnectionViewModel, bool>(CloseTabConnection);
        
        this.NewQueryTabCommand = ReactiveCommand.CreateFromTask(NewQueryTab, this.WhenAnyValue(vm => vm.HasConnections));
        this.OpenFileCommand = ReactiveCommand.CreateFromTask(OpenFile, this.WhenAnyValue(vm => vm.HasConnections));
        this.SaveCurrentEditorTabCommand = ReactiveCommand.CreateFromTask(() => SaveCurrentEditorTab(), this.WhenAnyValue(vm => vm.HasEditor));
        this.SaveAsCurrentEditorTabCommand = ReactiveCommand.CreateFromTask(() => SaveCurrentEditorTab(isSaveAs: true), this.WhenAnyValue(vm => vm.HasEditor));
        this.AboutCommand = ReactiveCommand.CreateFromTask(ShowAboutAsync);
        this.CutCommand = ReactiveCommand.Create(Cut, this.WhenAnyValue(vm => vm.CanCut));
        this.CopyCommand = ReactiveCommand.CreateFromTask(Copy, this.WhenAnyValue(vm => vm.CanCopy));
        this.PasteCommand = ReactiveCommand.Create(Paste, this.WhenAnyValue(vm => vm.CanPaste));
        this.UndoCommand = ReactiveCommand.Create(Undo, this.WhenAnyValue(vm => vm.CanUndo));
        this.RedoCommand = ReactiveCommand.Create(Redo, this.WhenAnyValue(vm => vm.CanRedo));
        this.FindCommand = ReactiveCommand.Create(Find, this.WhenAnyValue(vm => vm.HasActiveEditor));
        this.ReplaceCommand = ReactiveCommand.Create(Replace, this.WhenAnyValue(vm => vm.HasActiveEditor));
        this.UpperCommand = ReactiveCommand.Create(Upper, this.WhenAnyValue(vm => vm.HasSelection));
        this.LowerCommand = ReactiveCommand.Create(Lower, this.WhenAnyValue(vm => vm.HasSelection));
        this.BeautifyCommand = ReactiveCommand.Create(Beautify, this.WhenAnyValue(vm => vm.HasActiveEditor));
        this.IndentCommand = ReactiveCommand.Create(Indent, this.WhenAnyValue(vm => vm.HasActiveEditor));
        this.UnindentCommand = ReactiveCommand.Create(Unindent, this.WhenAnyValue(vm => vm.HasActiveEditor));
        this.CommentCommand = ReactiveCommand.Create(Comment, this.WhenAnyValue(vm => vm.HasSelection));
        this.UncommentCommand = ReactiveCommand.Create(Uncomment, this.WhenAnyValue(vm => vm.HasSelection));
        
        this.WhenAnyValue(vm => vm.SelectedTabConnectionIndex).Subscribe(_ => OnSelectedTabConnectionIndexChanged());
    }

    private TabQueryEditorViewModel? GetCurrentTabQueryEditorViewModel()
    {
        if (!HasConnections) return null;
        
        var connection = this.Connections[this.SelectedTabConnectionIndex];

        if (!connection.QueryEditors.Any()) return null;
        
        var queryEditor = connection.QueryEditors[connection.SelectedEditor];
        
        return queryEditor;
    }

    private async Task SaveCurrentEditorTab(bool isSaveAs = false)
    {
        if (GetCurrentTabQueryEditorViewModel() is null)
            return;

        var connection = this.Connections[this.SelectedTabConnectionIndex];
        var queryEditor = connection.QueryEditors[connection.SelectedEditor];

        await connection.SaveChanges(queryEditor, isSaveAs);
    }

    private async Task OpenFile()
    {
        if (!HasConnections)
            return;

        var fileToOpen = await _dialogService.ShowOpenFileAsync();
        if (fileToOpen != null)
        {
            await this.Connections[this.SelectedTabConnectionIndex].AddQueryEditorCommand.Execute(fileToOpen).ToTask();
        }

    }

    private async Task NewQueryTab()
    {
        if (!HasConnections)
            return;

        await this.Connections[this.SelectedTabConnectionIndex].AddQueryEditorCommand.Execute().ToTask();
    }

    private async Task<bool> CloseTabConnection(TabConnectionViewModel connection)
    {
        connection.SuspendAutosave();
        connection.PersistSessionSnapshot();

        var count = connection.QueryEditors.Count;
        var countClosed = 0;
        for (var indexQueryEditor = count - 1; indexQueryEditor >= 0; indexQueryEditor--)
        {
            connection.SelectedEditor = indexQueryEditor;
            await Task.Delay(100);
            var queryEditor = connection.QueryEditors[indexQueryEditor];
            if (!(await connection.CloseTabQueryEditor(queryEditor, showDialog: false, persistSession: true)))
                break;
            countClosed++;
        }

        if (countClosed == count)
            Connections.Remove(connection);
        else
            connection.ResumeAutosave();

        this.RaisePropertyChanged(nameof(HasConnections));
        this.RaisePropertyChanged(nameof(HasEditor));
        
        return countClosed == count;
    }

    public ReactiveCommand<Unit, Unit> NewWindowCommand { get; }
    public ReactiveCommand<Unit, Unit> NewQueryTabCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenFileCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveCurrentEditorTabCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveAsCurrentEditorTabCommand { get; }
    public ReactiveCommand<Unit, Unit> AboutCommand { get; }
    public ReactiveCommand<Unit, Unit> CutCommand { get; }
    public ReactiveCommand<Unit, Unit> CopyCommand { get; }
    public ReactiveCommand<Unit, Unit> PasteCommand { get; }
    public ReactiveCommand<Unit, Unit> UndoCommand { get; }
    public ReactiveCommand<Unit, Unit> RedoCommand { get; }
    public ReactiveCommand<Unit, Unit> FindCommand { get; }
    public ReactiveCommand<Unit, Unit> ReplaceCommand { get; }
    public ReactiveCommand<Unit, Unit> UpperCommand { get; }
    public ReactiveCommand<Unit, Unit> LowerCommand { get; }
    public ReactiveCommand<Unit, Unit> BeautifyCommand { get; }
    public ReactiveCommand<Unit, Unit> IndentCommand { get; }
    public ReactiveCommand<Unit, Unit> UnindentCommand { get; }
    public ReactiveCommand<Unit, Unit> CommentCommand { get; }
    public ReactiveCommand<Unit, Unit> UncommentCommand { get; }
    public ReactiveCommand<TabConnectionViewModel, bool> CloseTabConnectionCommand { get; }
    public ReactiveCommand<StyledElement, Unit> NewConnectionCommand { get; }
    public ObservableCollection<TabConnectionViewModel> Connections { get; } =  new();
    [Reactive] public int SelectedTabConnectionIndex { get; set; }
    [Reactive] public int CursorOffSet { get; set; }
    [Reactive] public int CursorLine { get; set; }
    [Reactive] public int CursorColumn { get; set; }
    [Reactive] public bool IsExecutionStatusVisible { get; set; }
    [Reactive] public string ExecutionStatusMessage { get; set; } = string.Empty;
    [Reactive] public bool HasActiveEditor { get; private set; }
    [Reactive] public bool HasSelection { get; private set; }
    [Reactive] public bool CanCut { get; private set; }
    [Reactive] public bool CanCopy { get; private set; }
    [Reactive] public bool CanPaste { get; private set; }
    [Reactive] public bool CanUndo { get; private set; }
    [Reactive] public bool CanRedo { get; private set; }
    public KeyGesture OpenFileGesture => CreatePrimaryGesture(Key.O);
    public KeyGesture SaveGesture => CreatePrimaryGesture(Key.S);
    public KeyGesture SaveAsGesture => CreatePrimaryGesture(Key.S, KeyModifiers.Shift);
    public KeyGesture NewQueryTabGesture => CreatePrimaryGesture(Key.T);
    public KeyGesture CutGesture => CreatePrimaryGesture(Key.X);
    public KeyGesture CopyGesture => CreatePrimaryGesture(Key.C);
    public KeyGesture PasteGesture => CreatePrimaryGesture(Key.V);
    public KeyGesture UndoGesture => CreatePrimaryGesture(Key.Z);
    public KeyGesture RedoGesture => CreatePrimaryGesture(Key.Y);
    public KeyGesture FindGesture => CreatePrimaryGesture(Key.F);
    public KeyGesture ReplaceGesture => CreatePrimaryGesture(Key.H);
    public KeyGesture UpperGesture => CreatePrimaryGesture(Key.U, KeyModifiers.Shift);
    public KeyGesture LowerGesture => CreatePrimaryGesture(Key.U);
    public KeyGesture BeautifyGesture => CreatePrimaryGesture(Key.B);
    public KeyGesture IndentGesture => new(Key.Tab);
    public KeyGesture UnindentGesture => new(Key.Tab, KeyModifiers.Shift);
    public KeyGesture CommentGesture => CreatePrimaryGesture(Key.K);
    public KeyGesture UncommentGesture => CreatePrimaryGesture(Key.K, KeyModifiers.Shift);
    public bool UsesMacShortcuts => OperatingSystem.IsMacOS();
    public bool ShowHelpMenu => !OperatingSystem.IsMacOS();

    public bool HasConnections
    {
        get => Connections.Any();
        set { }
    }

    public bool HasEditor
    {
        get => GetCurrentTabQueryEditorViewModel() != null;
        set { }
    }

    private void OnSelectedTabConnectionIndexChanged()
    {
        if (!Connections.Any()) return;
        var connection = Connections[this.SelectedTabConnectionIndex];
        var queryEditor = connection.QueryEditors[connection.SelectedEditor];
        queryEditor.ShowCursorData();
        this.RaisePropertyChanged(nameof(HasConnections));
        this.RaisePropertyChanged(nameof(HasEditor));
    }

    private void NewWindow()
    {
        var newWindow = (MainWindow)_serviceProvider.CreateScope().ServiceProvider.GetRequiredService<IMainWindow>();
        newWindow.Show();
    }

    private async Task ShowAboutAsync()
    {
        var version = ApplicationVersionHelper.GetCurrentVersion(typeof(MainWindowViewModel).Assembly);
        await _dialogService.ShowAboutAsync(version, () => _releaseUpdateService.CheckForUpdatesAsync());
    }

    public void SetActiveEditor(TextEditor editor, DatabaseType databaseType)
    {
        _activeEditor = editor;
        _activeDatabaseType = databaseType;
        _clipboardFocusTarget = ClipboardFocusTarget.Editor;
        RefreshActiveEditorState();
    }

    public void ClearActiveEditor(TextEditor editor)
    {
        if (!ReferenceEquals(_activeEditor, editor))
            return;

        _activeEditor = null;
        if (_clipboardFocusTarget == ClipboardFocusTarget.Editor)
            _clipboardFocusTarget = ClipboardFocusTarget.None;

        RefreshActiveEditorState();
    }

    public void SetActiveGrid(NextGridControl grid)
    {
        _activeGrid = grid;
        _clipboardFocusTarget = ClipboardFocusTarget.Grid;
        RefreshActiveEditorState();
    }

    public void ClearActiveGrid(NextGridControl grid)
    {
        if (!ReferenceEquals(_activeGrid, grid))
            return;

        _activeGrid = null;
        if (_clipboardFocusTarget == ClipboardFocusTarget.Grid)
            _clipboardFocusTarget = ClipboardFocusTarget.None;

        RefreshActiveEditorState();
    }

    public void ClearActiveClipboardFocus()
    {
        _clipboardFocusTarget = ClipboardFocusTarget.None;
        RefreshActiveEditorState();
    }

    public void RefreshActiveEditorState()
    {
        var isEditorFocus = _clipboardFocusTarget == ClipboardFocusTarget.Editor;
        var isGridFocus = _clipboardFocusTarget == ClipboardFocusTarget.Grid;

        HasActiveEditor = isEditorFocus && _activeEditor is not null;
        HasSelection = isEditorFocus && _activeEditor?.SelectionLength > 0;
        CanCut = isEditorFocus && _activeEditor?.CanCut == true;
        CanCopy = isGridFocus ? _activeGrid?.CanCopy == true : isEditorFocus && _activeEditor?.CanCopy == true;
        CanPaste = isEditorFocus && _activeEditor is not null;
        CanUndo = isEditorFocus && _activeEditor?.CanUndo == true;
        CanRedo = isEditorFocus && _activeEditor?.CanRedo == true;
        this.RaisePropertyChanged(nameof(HasEditor));
        this.RaisePropertyChanged(nameof(HasConnections));
    }

    private void Cut()
    {
        if (_activeEditor?.CanCut == true)
            _activeEditor.Cut();

        RefreshActiveEditorState();
    }

    private async Task Copy()
    {
        if (_clipboardFocusTarget == ClipboardFocusTarget.Grid && _activeGrid?.CanCopy == true)
            await _activeGrid.CopySelectionAsync();
        else if (_activeEditor?.CanCopy == true)
            _activeEditor.Copy();
    }

    private void Paste()
    {
        if (_activeEditor?.CanPaste == true)
            _activeEditor.Paste();
    }

    private void Undo()
    {
        if (_activeEditor?.CanUndo == true)
            _activeEditor.Undo();

        RefreshActiveEditorState();
    }

    private void Redo()
    {
        if (_activeEditor?.CanRedo == true)
            _activeEditor.Redo();

        RefreshActiveEditorState();
    }

    private void Find()
    {
        if (_activeEditor is null)
            return;

        var panel = SearchPanel.Install(_activeEditor);
        InitializeSearchPattern(_activeEditor, panel);
        panel.IsReplaceMode = false;
        panel.Open();
        panel.Reactivate();
    }

    private void Replace()
    {
        if (_activeEditor is null)
            return;

        var panel = SearchPanel.Install(_activeEditor);
        InitializeSearchPattern(_activeEditor, panel);
        panel.IsReplaceMode = true;
        panel.Open();
        panel.Reactivate();
    }

    private void Upper()
    {
        if (_activeEditor is null)
            return;

        ApplyTextEdit(_activeEditor, SqlEditorTextOperations.ToUpper(_activeEditor.Text ?? string.Empty, _activeEditor.SelectionStart, _activeEditor.SelectionLength));
    }

    private void Lower()
    {
        if (_activeEditor is null)
            return;

        ApplyTextEdit(_activeEditor, SqlEditorTextOperations.ToLower(_activeEditor.Text ?? string.Empty, _activeEditor.SelectionStart, _activeEditor.SelectionLength));
    }

    private void Beautify()
    {
        if (_activeEditor is null)
            return;

        var indentation = GetIndentation(_activeEditor);
        ApplyTextEdit(_activeEditor, SqlEditorTextOperations.Beautify(_activeEditor.Text ?? string.Empty, _activeEditor.SelectionStart, _activeEditor.SelectionLength, indentation, _activeDatabaseType));
    }

    private void Indent()
    {
        if (_activeEditor is null)
            return;

        var indentation = GetIndentation(_activeEditor);
        ApplyTextEdit(_activeEditor, SqlEditorTextOperations.Indent(_activeEditor.Text ?? string.Empty, _activeEditor.SelectionStart, _activeEditor.SelectionLength, indentation));
    }

    private void Unindent()
    {
        if (_activeEditor is null)
            return;

        var indentation = GetIndentation(_activeEditor);
        ApplyTextEdit(_activeEditor, SqlEditorTextOperations.Unindent(_activeEditor.Text ?? string.Empty, _activeEditor.SelectionStart, _activeEditor.SelectionLength, indentation));
    }

    private void Comment()
    {
        if (_activeEditor is null)
            return;

        ApplyTextEdit(_activeEditor, SqlEditorTextOperations.Comment(_activeEditor.Text ?? string.Empty, _activeEditor.SelectionStart, _activeEditor.SelectionLength));
    }

    private void Uncomment()
    {
        if (_activeEditor is null)
            return;

        ApplyTextEdit(_activeEditor, SqlEditorTextOperations.Uncomment(_activeEditor.Text ?? string.Empty, _activeEditor.SelectionStart, _activeEditor.SelectionLength));
    }

    private async Task NewConnection(StyledElement control)
    {
        try
        {
            await Task.Delay(100);
            var window = ServicesExtensionMethods.GetParentWindow(control);
            var connectionSettings = await _connectionDialogService.ShowDialogAsync(window);

            if (connectionSettings is not null)
            {
                var existingIndex = FindConnectionIndex(Connections, connectionSettings.Id);

                if (existingIndex >= 0)
                {
                    SelectedTabConnectionIndex = existingIndex;
                }
                else
                {
                    var tab = new TabConnectionViewModel(connectionSettings, true, _serviceProvider);
                    Connections.Add(tab);
                    SelectedTabConnectionIndex = Connections.Count - 1;
                }

                this.RaisePropertyChanged(nameof(HasConnections));
                this.RaisePropertyChanged(nameof(HasEditor));
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public static int FindConnectionIndex(IReadOnlyList<TabConnectionViewModel> connections, Guid connectionId)
    {
        for (var i = 0; i < connections.Count; i++)
        {
            if (connections[i].ConnectionSettings.Id == connectionId)
                return i;
        }

        return -1;
    }
    
    private void ShowCursorDataEvent(ShowCursorDataEvent message)
    {
        this.CursorOffSet = message.CursorOffSet;
        this.CursorLine = message.CursorLine;
        this.CursorColumn = message.CursorColumn;
    }

    private void ShowExecutionStatusEvent(ShowExecutionStatusEvent message)
    {
        IsExecutionStatusVisible = message.IsVisible;
        ExecutionStatusMessage = message.Message;
    }

    private static void InitializeSearchPattern(TextEditor editor, SearchPanel panel)
    {
        if (!string.IsNullOrWhiteSpace(editor.SelectedText))
            panel.SearchPattern = editor.SelectedText;
    }

    private static string GetIndentation(TextEditor editor)
    {
        var options = editor.Options;
        if (options.ConvertTabsToSpaces)
            return new string(' ', Math.Max(1, options.IndentationSize));

        return string.IsNullOrEmpty(options.IndentationString)
            ? "\t"
            : options.IndentationString;
    }

    private static void ApplyTextEdit(TextEditor editor, TextEditResult? edit)
    {
        if (edit is null || editor.Document is null)
            return;

        using (editor.Document.RunUpdate())
        {
            editor.Document.Replace(edit.Value.ReplaceStart, edit.Value.ReplaceLength, edit.Value.ReplacementText);
        }

        if (edit.Value.SelectionLength == 0)
        {
            editor.SelectionLength = 0;
            editor.SelectionStart = edit.Value.SelectionStart;
            editor.Focus();
            return;
        }

        editor.Select(edit.Value.SelectionStart, edit.Value.SelectionLength);
        editor.Focus();
    }

    private KeyGesture CreatePrimaryGesture(Key key, KeyModifiers modifiers = KeyModifiers.None)
    {
        return new KeyGesture(key, _primaryShortcutModifier | modifiers);
    }

    public bool HasPrimaryShortcutModifier(KeyEventArgs e)
    {
        return e.KeyModifiers.HasFlag(_primaryShortcutModifier);
    }
}
