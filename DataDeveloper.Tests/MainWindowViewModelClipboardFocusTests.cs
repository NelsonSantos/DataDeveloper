using System;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using AvaloniaEdit;
using DataDeveloper.Data.Enums;
using DataDeveloper.Enums;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Interfaces;
using DataDeveloper.NextGrid;
using DataDeveloper.NextGrid.UI;
using DataDeveloper.Services;
using DataDeveloper.ViewModels;
using DataDeveloper.Views;
using Xunit;

namespace DataDeveloper.Tests;

public class MainWindowViewModelClipboardFocusTests
{
    [AvaloniaFact]
    public void SetActiveGrid_MakesGridTheCopyTarget_EvenWithStaleEditorSelection()
    {
        var viewModel = CreateViewModel();
        var editor = new TextEditor { Text = "select 1", SelectionStart = 0, SelectionLength = 8 };
        var (grid, window) = CreateWindowedGridWithSelectedCell();

        viewModel.SetActiveEditor(editor, DatabaseType.SqlServer);
        Assert.True(viewModel.CanCopy);

        viewModel.SetActiveGrid(grid);

        Assert.True(viewModel.CanCopy);
        Assert.False(viewModel.HasActiveEditor);
        window.Close();
    }

    [AvaloniaFact]
    public void SetActiveGrid_WithNoSelection_ReportsCanCopyFalse()
    {
        var viewModel = CreateViewModel();
        var grid = CreateGrid();

        viewModel.SetActiveGrid(grid);

        Assert.False(viewModel.CanCopy);
    }

    [AvaloniaFact]
    public void ClearActiveClipboardFocus_StopsEditorFromClaimingCutCopyPaste()
    {
        // Regression test: focusing an unrelated control elsewhere in the same view
        // (e.g. a "Create Variable" parameter value TextBox) must stop the SQL editor
        // from swallowing Ctrl/Cmd+V, so that control can paste into itself instead.
        var viewModel = CreateViewModel();
        var editor = new TextEditor { Text = "select 1", SelectionStart = 0, SelectionLength = 8 };

        viewModel.SetActiveEditor(editor, DatabaseType.SqlServer);
        Assert.True(viewModel.CanPaste);
        Assert.True(viewModel.CanCopy);

        viewModel.ClearActiveClipboardFocus();

        Assert.False(viewModel.CanPaste);
        Assert.False(viewModel.CanCopy);
        Assert.False(viewModel.HasActiveEditor);
    }

    [AvaloniaFact]
    public void ClearActiveClipboardFocus_ThenRefocusingEditor_RestoresItAsCopyPasteTarget()
    {
        var viewModel = CreateViewModel();
        var editor = new TextEditor { Text = "select 1", SelectionStart = 0, SelectionLength = 8 };

        viewModel.SetActiveEditor(editor, DatabaseType.SqlServer);
        viewModel.ClearActiveClipboardFocus();
        viewModel.SetActiveEditor(editor, DatabaseType.SqlServer);

        Assert.True(viewModel.CanPaste);
        Assert.True(viewModel.CanCopy);
    }

    [AvaloniaFact]
    public async Task Copy_WhenGridIsActiveTarget_CopiesGridSelectionNotEditorText()
    {
        var viewModel = CreateViewModel();
        var editor = new TextEditor { Text = "select 1", SelectionStart = 0, SelectionLength = 8 };
        var (grid, window) = CreateWindowedGridWithSelectedCell();

        viewModel.SetActiveEditor(editor, DatabaseType.SqlServer);
        viewModel.SetActiveGrid(grid);

        await viewModel.CopyCommand.Execute().ToTask();

        var text = await window.Clipboard!.GetTextAsync();
        Assert.Equal("R1C1", text);
        window.Close();
    }

    [AvaloniaFact]
    public void FocusReturningToEditor_AfterGridCleared_MakesEditorTheCopyTargetAgain()
    {
        var viewModel = CreateViewModel();
        var editor = new TextEditor { Text = "select 1", SelectionStart = 0, SelectionLength = 8 };
        var grid = CreateGrid();

        viewModel.SetActiveEditor(editor, DatabaseType.SqlServer);
        viewModel.SetActiveGrid(grid);
        viewModel.ClearActiveGrid(grid);

        Assert.False(viewModel.CanCopy);
        Assert.False(viewModel.HasActiveEditor);

        viewModel.SetActiveEditor(editor, DatabaseType.SqlServer);

        Assert.True(viewModel.CanCopy);
        Assert.True(viewModel.HasActiveEditor);
    }

    private static (NextGridControl Grid, Window Window) CreateWindowedGridWithSelectedCell()
    {
        var grid = CreateGrid();
        var window = new Window { Content = grid, Width = 400, Height = 200 };
        window.Show();
        window.Measure(new Avalonia.Size(400, 200));
        window.Arrange(new Avalonia.Rect(0, 0, 400, 200));
        window.UpdateLayout();

        var cell = grid.GetCellBoundsForTest(0, 0);
        grid.SelectCellAtLocalPointForTest(new Avalonia.Point(cell.X + 8, cell.Y + 8));
        return (grid, window);
    }

    private static NextGridControl CreateGrid()
    {
        var headers = new System.Collections.ObjectModel.ObservableCollection<string> { "Field1" };
        var types = new System.Collections.ObjectModel.ObservableCollection<Type> { typeof(string) };
        var rows = new System.Collections.ObjectModel.ObservableCollection<System.Collections.Generic.IReadOnlyList<object?>>
        {
            new object?[] { "R1C1" }
        };

        return new NextGridControl { Headers = headers, ColumnTypes = types, Rows = rows };
    }

    private static NextGridControl CreateGridWithSelectedCell()
    {
        var grid = CreateGrid();
        var window = new Window { Content = grid, Width = 400, Height = 200 };
        window.Show();
        window.Measure(new Avalonia.Size(400, 200));
        window.Arrange(new Avalonia.Rect(0, 0, 400, 200));
        window.UpdateLayout();

        var cell = grid.GetCellBoundsForTest(0, 0);
        grid.SelectCellAtLocalPointForTest(new Avalonia.Point(cell.X + 8, cell.Y + 8));
        return grid;
    }

    private static MainWindowViewModel CreateViewModel() => new(new FakeServiceProvider());

    private sealed class FakeServiceProvider : IServiceProvider
    {
        private readonly IEventAggregatorService _eventAggregatorService = new EventAggregatorService();

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IConnectionDialogService))
                return new FakeConnectionDialogService();
            if (serviceType == typeof(IEventAggregatorService))
                return _eventAggregatorService;
            if (serviceType == typeof(IDialogService))
                return new FakeDialogService();
            if (serviceType == typeof(IReleaseUpdateService))
                return new FakeReleaseUpdateService();

            throw new NotSupportedException($"Service not configured for test: {serviceType}");
        }
    }

    private sealed class FakeConnectionDialogService : IConnectionDialogService
    {
        public Task<IConnectionSettings?> ShowDialogAsync(Window parentWindow) => Task.FromResult<IConnectionSettings?>(null);
    }

    private sealed class FakeReleaseUpdateService : IReleaseUpdateService
    {
        public Task NotifyIfUpdateAvailableAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CheckForUpdatesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeDialogService : IDialogService
    {
        public Task<DialogResult> ShowDialogAsync(string message, string? title = null, DialogButtons buttons = DialogButtons.Ok, DialogIcon icon = DialogIcon.Info) =>
            Task.FromResult(DialogResult.Ok);

        public Task<DialogResult> ShowDialogResult(string message, string? title = null) => Task.FromResult(DialogResult.Ok);

        public Task ShowMessageAsync(string message, string? title = null) => Task.CompletedTask;

        public Task ShowAboutAsync(string version, Func<Task> checkForUpdatesAsync) => Task.CompletedTask;

        public Task<DialogResult> ShowReleaseUpdateAsync(string message, string? title = null) => Task.FromResult(DialogResult.Ok);

        public Task<string?> ShowSaveFileDialogAsync(string? suggestedName = null, string? title = null) => Task.FromResult<string?>(null);

        public Task<string?> ShowOpenFileAsync(string? title = null) => Task.FromResult<string?>(null);

        public Task<string?> ShowOpenDatabaseFileAsync(string? title = null) => Task.FromResult<string?>(null);

        public Task<string?> ShowCreateDatabaseFileAsync(string? suggestedName = null, string? title = null) => Task.FromResult<string?>(null);

        public Task<string?> ShowSaveJsonFileDialogAsync(string? suggestedName = null, string? title = null) => Task.FromResult<string?>(null);

        public Task<string?> ShowOpenJsonFileDialogAsync(string? title = null) => Task.FromResult<string?>(null);
    }
}
