using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using DataDeveloper.Core;
using ReactiveUI;
using ReactiveUI.Reactive;
using ReactiveUI.SourceGenerators;

namespace DataDeveloper.ViewModels;

/// <summary>
/// Ctrl+Tab switcher: the open connections on the left and the highlighted connection's query editors on the right,
/// most recently used first. Opening it highlights the previously used query, so a quick Ctrl+Tab flips between the
/// two most recent queries.
/// </summary>
public partial class QuerySwitcherViewModel : ViewModelBase
{
    private readonly Action<QuerySwitcherViewModel> _commit;
    private readonly Action _cancel;

    [Reactive(SetModifier = AccessModifier.Private)] private int _selectedConnectionIndex;
    [Reactive(SetModifier = AccessModifier.Private)] private IReadOnlyList<TabQueryEditorViewModel> _editors = [];
    [Reactive(SetModifier = AccessModifier.Private)] private int _selectedEditorIndex = -1;
    [Reactive(SetModifier = AccessModifier.Private)] private bool _isConnectionColumnActive;

    public QuerySwitcherViewModel(
        IReadOnlyList<TabConnectionViewModel> connections,
        int connectionIndex,
        int step,
        Action<QuerySwitcherViewModel> commit,
        Action cancel)
    {
        Connections = connections;
        ConnectionItems = connections.Select((connection, index) => new SwitcherConnectionItem(connection, index < 9 ? index + 1 : null)).ToList();
        _commit = commit;
        _cancel = cancel;
        this.WhenAnyValue(vm => vm.SelectedEditorIndex, vm => vm.Editors)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(SelectedEditor));
                this.RaisePropertyChanged(nameof(Details));
            });

        HighlightConnection(Math.Clamp(connectionIndex, 0, Math.Max(connections.Count - 1, 0)));

        // Forward starts at the previous query (index 1), backward at the least recently used one.
        if (Editors.Count > 1)
            SelectedEditorIndex = step >= 0 ? 1 : Editors.Count - 1;
    }

    public IReadOnlyList<TabConnectionViewModel> Connections { get; }

    /// <summary>The connections as listed in the switcher; the first nine are numbered.</summary>
    public IReadOnlyList<SwitcherConnectionItem> ConnectionItems { get; }

    public TabConnectionViewModel? SelectedConnection =>
        SelectedConnectionIndex >= 0 && SelectedConnectionIndex < Connections.Count ? Connections[SelectedConnectionIndex] : null;

    public TabQueryEditorViewModel? SelectedEditor =>
        SelectedEditorIndex >= 0 && SelectedEditorIndex < Editors.Count ? Editors[SelectedEditorIndex] : null;

    /// <summary>Keyboard legend shown at the bottom, in the platform's notation.</summary>
    public string ShortcutHints { get; } = OperatingSystem.IsMacOS()
        ? "⌃Tab / ⇧⌃Tab  move    ⌃1–9  connection    release ⌃ or ↩  open    esc  cancel"
        : "Ctrl+Tab / Ctrl+Shift+Tab  move    Ctrl+1–9 or ← →  connection    release Ctrl or Enter  open    Esc  cancel";

    /// <summary>Footer text: the highlighted query's file, or its connection when the query has no file.</summary>
    public string? Details => string.IsNullOrWhiteSpace(SelectedEditor?.File) ? SelectedConnection?.Name : SelectedEditor.File;

    /// <summary>Moves the highlight within the active column, wrapping around.</summary>
    public void Move(int step)
    {
        if (IsConnectionColumnActive)
        {
            if (Connections.Count > 0)
                HighlightConnection(Wrap(SelectedConnectionIndex + step, Connections.Count));
            return;
        }

        if (Editors.Count > 0)
            SelectedEditorIndex = Wrap(SelectedEditorIndex + step, Editors.Count);
    }

    public void ActivateConnectionColumn(bool active) => IsConnectionColumnActive = active && Connections.Count > 0;

    public void HighlightConnection(int index)
    {
        if (index < 0 || index >= Connections.Count)
            return;

        SelectedConnectionIndex = index;
        Editors = Connections[index].EditorsByRecentUse;
        SelectedEditorIndex = Editors.Count > 0 ? 0 : -1;
        this.RaisePropertyChanged(nameof(SelectedConnection));
    }

    /// <summary>
    /// Highlights the connection shown with <paramref name="number"/> and lists its queries, so Tab walks them next.
    /// Numbers work while Ctrl is held, unlike Ctrl+Left/Right, which macOS keeps for switching Spaces.
    /// </summary>
    public void HighlightConnectionNumber(int number)
    {
        if (number < 1 || number > Math.Min(Connections.Count, 9))
            return;

        HighlightConnection(number - 1);
        IsConnectionColumnActive = false;
    }

    public void HighlightEditor(int index)
    {
        if (index >= 0 && index < Editors.Count)
            SelectedEditorIndex = index;
    }

    public void Commit() => _commit(this);

    public void Cancel() => _cancel();

    private static int Wrap(int index, int count) => ((index % count) + count) % count;
}

/// <summary>A connection in the switcher's left column, with its shortcut number (1-9) when it has one.</summary>
public sealed record SwitcherConnectionItem(TabConnectionViewModel Connection, int? Number);
