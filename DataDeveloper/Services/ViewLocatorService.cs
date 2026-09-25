using Avalonia.Controls;
using Avalonia.Controls.Templates;
using DataDeveloper.Core;
using DataDeveloper.Interfaces;
using DataDeveloper.Models;

namespace DataDeveloper.Services;

public class ViewLocatorService : IDataTemplate
{
    private readonly IViewResolverService _viewResolver;

    public ViewLocatorService(IViewResolverService viewResolver)
    {
        _viewResolver = viewResolver;
    }

    public Control? Build(object? data)
    {
        if (data is null)
            return null;

        return _viewResolver.ResolveByModel(data);
    }

    public bool Match(object? data)
    {
        // Tab content is always built by TabTemplateSelector (which caches one view per tab). TabControl sets
        // a tab's Content before its ContentTemplate, so matching here would build an orphan duplicate view
        // that stays subscribed to the tab's view model.
        return data is ViewModelBase and not BaseTabContent;
    }
}
