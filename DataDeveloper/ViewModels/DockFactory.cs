using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using DataDeveloper.Views;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Core.Events;
using Dock.Model.ReactiveUI;
using Dock.Model.ReactiveUI.Controls;
using Dock.Model.ReactiveUI.Core;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using ReactiveUI;

namespace DataDeveloper.ViewModels;

public class DockFactory : Factory
{
    private int _countSqlEditors = 0;

    public DockFactory()
    {
    }

    private SqlEditorViewModel GetNewSqlEditorViewModel()
    {
        _countSqlEditors++;
        var document = new SqlEditorViewModel
        {
            Id = $"SqlEditor{_countSqlEditors}",
            Title = $"Sql Statement {_countSqlEditors}",
        };
        return document;
    }
    
    public override IRootDock CreateLayout()
    {

        var treeTool = new ConnectionDataViewModel
        {
            Id = "Tables",
            Title = "Tabelas",
            CanClose = false,
            CanFloat = false,
        };

        var outputTool = new ResultViewModel
        {
            Id = "Output",
            Title = "Resultados",
            CanClose = false,
            CanFloat = false,
        };

        var document = GetNewSqlEditorViewModel();
        var documentDock = new DocumentDock
        {
            Id = "Documents",
            Title = "Documentos",
            ActiveDockable = document,
            VisibleDockables = CreateList<IDockable>( document ),
            CanCreateDocument = true,
        };
        documentDock.CreateDocument = ReactiveCommand.Create(() =>
        {
            var newDocument = GetNewSqlEditorViewModel();
            documentDock.VisibleDockables.Add(newDocument);
            documentDock.ActiveDockable = newDocument;
        });
        
        //this.DockableWillBeClosed += OnDockableWillBeClosed;
        this.DockableWillBeClosed+= OnDockableWillBeClosed; 
        // documentDock.ConfirmCloseCommand = ReactiveCommand.CreateFromTask<SqlEditorViewModel>(async doc =>
        // {
        //     var confirmar = await doc.ConfirmCloseCommand.Execute().FirstAsync();
        //
        //     if (!confirmar) return;
        //     
        //     documentDock.Close.Execute(true);
        // });
    
        var left = new ToolDock
        {
            Id = "Left",
            Title = "Left",
            ActiveDockable = treeTool,
            VisibleDockables = CreateList<IDockable>( treeTool ),
            Alignment = Alignment.Left,
            CanClose = false,
            CanFloat = false,
            Proportion = 0.25,
        };

        var bottom = new ToolDock
        {
            Id = "Bottom",
            Title = "Bottom",
            ActiveDockable = outputTool,
            VisibleDockables = CreateList<IDockable>( outputTool ),
            Alignment = Alignment.Bottom,
            CanClose = false,
            CanFloat = false,
            Proportion = 0.25, 
        };

        var windowLayoutContent = new ProportionalDock
        {
            Orientation = Orientation.Vertical,
            CanClose = false,
            VisibleDockables = CreateList<IDockable>(
                new ProportionalDock
                {
                    Orientation = Orientation.Horizontal,
                    VisibleDockables = new List<IDockable> { left, new ProportionalDockSplitter(), documentDock }
                },
                new ProportionalDockSplitter(),
                bottom
            ),
        };

        var rootDock = CreateRootDock();
        
        rootDock.Id = "Root";
        rootDock.Title = "Root";
        rootDock.ActiveDockable = windowLayoutContent;
        rootDock.DefaultDockable = windowLayoutContent;
        rootDock.VisibleDockables = CreateList<IDockable>(windowLayoutContent);

        return rootDock;
    }

    private async Task<bool> OnDockableWillBeClosed(DockableWillBeClosedEventArgs e)
    {
        return await CanCloseDocument(e.Dockable).ConfigureAwait(true);
    }

    
    public async Task<bool> CanCloseDocument(IDockable? dockable)
    {
        if (dockable == null)
            return false;
        
        // Localiza o DocumentDock pai
        if (dockable.Owner is DocumentDock parent)
        {
            var count = parent.VisibleDockables?.OfType<Document>().Count() ?? 0;

            // É o último documento aberto?
            bool isLast = count <= 1;

            // Exibe uma MessageBox de confirmação
            var result = await Dispatcher.UIThread.InvokeAsync(async ()=> await ShowConfirmationAsync(isLast));

            return result; // true para permitir fechar, false para cancelar
        }

        return true;
    }

    private async Task<bool> ShowConfirmationAsync(bool isLast)
    {
        string message = isLast
            ? "Esta é a última aba aberta. Deseja realmente fechá-la?"
            : "Deseja fechar esta aba?";

        var box = MessageBoxManager
            .GetMessageBoxStandard("Fechar Documento", message,
                ButtonEnum.YesNo, Icon.Question);

        var result = await box.ShowAsync();

        return result == ButtonResult.Yes;
    }

    private Window? GetHostWindow()
    {
        // Tenta encontrar uma janela associada
        var window = App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.Windows.FirstOrDefault()
            : null;
        return window;
    }
    
}

public class Teste : DocumentDock
{

}