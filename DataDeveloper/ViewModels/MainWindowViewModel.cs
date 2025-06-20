using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using DataDeveloper.Core;
using DataDeveloper.EventAggregators;
using DataDeveloper.Interfaces;
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
    public MainWindowViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _connectionDialogService = _serviceProvider.GetService<IConnectionDialogService>();
        _eventAggregatorService = _serviceProvider.GetService<IEventAggregatorService>();
        _dialogService = _serviceProvider.GetService<IDialogService>();
        
        _eventAggregatorService.Subscribe<ShowCursorDataEvent>(this, ShowCursorDataEvent);

        this.NewWindowCommand = ReactiveCommand.Create(NewWindow);
        this.NewConnectionCommand = ReactiveCommand.CreateFromTask<StyledElement>(NewConnection);
        this.CloseTabConnectionCommand = ReactiveCommand.CreateFromTask<TabConnectionViewModel, bool>(CloseTabConnection);
        
        this.NewQueryTabCommand = ReactiveCommand.CreateFromTask(NewQueryTab);
        this.OpenFileCommand = ReactiveCommand.CreateFromTask(OpenFile);
        this.SaveCurrentEditorTabCommand = ReactiveCommand.CreateFromTask(() => SaveCurrentEditorTab());
        this.SaveAsCurrentEditorTabCommand = ReactiveCommand.CreateFromTask(() => SaveCurrentEditorTab(isSaveAs: true));
        
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
        var connection = this.Connections[this.SelectedTabConnectionIndex];
        var queryEditor = connection.QueryEditors[connection.SelectedEditor];

        if (File.Exists(queryEditor.File))
            await connection.SaveChanges(queryEditor, isSaveAs);
        else
            await connection.CloseTabQueryEditor(queryEditor, showDialog: false);
    }

    private async Task OpenFile()
    {
        var fileToOpen = await _dialogService.ShowOpenFileAsync();
        if (fileToOpen != null)
        {
            await this.Connections[this.SelectedTabConnectionIndex].AddQueryEditorCommand.Execute(fileToOpen);
        }

    }

    private async Task NewQueryTab()
    {
        await this.Connections[this.SelectedTabConnectionIndex].AddQueryEditorCommand.Execute();
    }

    private async Task<bool> CloseTabConnection(TabConnectionViewModel connection)
    {
        var count = connection.QueryEditors.Count;
        var countClosed = 0;
        for (var indexQueryEditor = count - 1; indexQueryEditor >= 0; indexQueryEditor--)
        {
            connection.SelectedEditor = indexQueryEditor;
            await Task.Delay(100);
            var queryEditor = connection.QueryEditors[indexQueryEditor];
            if (!(await connection.CloseTabQueryEditorCommand.Execute(queryEditor)))
                break;
            countClosed++;
        }

        if (countClosed == count)
            Connections.Remove(connection);
        
        return countClosed == count;
    }

    public ReactiveCommand<Unit, Unit> NewWindowCommand { get; }
    public ReactiveCommand<Unit, Unit> NewQueryTabCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenFileCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveCurrentEditorTabCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveAsCurrentEditorTabCommand { get; }
    public ReactiveCommand<TabConnectionViewModel, bool> CloseTabConnectionCommand { get; }
    public ReactiveCommand<StyledElement, Unit> NewConnectionCommand { get; }
    public ObservableCollection<TabConnectionViewModel> Connections { get; } =  new();
    [Reactive] public int SelectedTabConnectionIndex { get; set; }
    [Reactive] public int CursorOffSet { get; set; }
    [Reactive] public int CursorLine { get; set; }
    [Reactive] public int CursorColumn { get; set; }

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
    }

    private void NewWindow()
    {
        var newWindow = (MainWindow)_serviceProvider.CreateScope().ServiceProvider.GetService<IMainWindow>();
        newWindow.Show();
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
                var tab = new TabConnectionViewModel(connectionSettings, true, _serviceProvider);
                Connections.Add(tab);
                SelectedTabConnectionIndex = Connections.Count - 1;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
    
    private void ShowCursorDataEvent(ShowCursorDataEvent message)
    {
        this.CursorOffSet = message.CursorOffSet;
        this.CursorLine = message.CursorLine;
        this.CursorColumn = message.CursorColumn;
    }
}