using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using DataDeveloper.ViewModels;
using DataDeveloper.ViewModels.Docks;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Core.Events;
using Dock.Model.ReactiveUI;
using Dock.Model.ReactiveUI.Controls;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace DataDeveloper.Services;

public class DockFactory : Factory
{
    private int _countSqlEditors = 0;

    public DockFactory()
    {
    }

    public override IDocumentDock CreateDocumentDock()
    {
        return new EditorDocumentDock();
    }

    public override IRootDock CreateLayout()
    {

        var treeTool = new ConnectionDetailsViewModel
        {
            Id = "connectionDetails",
            Title = "Tables",
            CanClose = false,
            CanFloat = false,
        };

        var left = new ProportionalDock()
        {
            Proportion = 0.25,
            Orientation = Orientation.Vertical,
            Id = "Left",
            Title = "Left",
            ActiveDockable = null,
            VisibleDockables = CreateList<IDockable>
            ( 
                new ToolDock
                {
                    ActiveDockable = treeTool,
                    VisibleDockables = CreateList<IDockable>(treeTool),
                    Alignment = Alignment.Left
                }
            ),
        };
        
        var entireDocument = EditorDocumentDock.GetNewEditorDocument(this);
        var documentDock = new EditorDocumentDock()
        {
            IsCollapsable = false,
            Id = "Documents",
            Title = "Documentos",
            ActiveDockable = entireDocument,
            VisibleDockables = CreateList<IDockable>(entireDocument),
            CanCreateDocument = true,
        };

        this.DockableWillBeClosed += OnDockableWillBeClosed;
        
        var mainLayout = new ProportionalDock
        {
            Orientation = Orientation.Horizontal,
            VisibleDockables = CreateList<IDockable>( left, new ProportionalDockSplitter(), documentDock ),
        };

        var homeView = new RootDock
        {
            Id = "Home",
            Title = "Home",
            ActiveDockable = mainLayout,
            VisibleDockables = CreateList<IDockable>(mainLayout)
        };

        var rootDock = CreateRootDock();
        
        rootDock.IsCollapsable = false;
        rootDock.ActiveDockable = homeView;
        rootDock.DefaultDockable = homeView;
        rootDock.VisibleDockables = CreateList<IDockable>(homeView);

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