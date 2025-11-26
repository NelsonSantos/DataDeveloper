using Avalonia.Controls;
using Avalonia.Controls.Templates;
using DataDeveloper.Interfaces;
using ReactiveUI;

namespace DataDeveloper.Services;

public class ViewLocatorService : IDataTemplate
{
    private readonly IViewResolverService _viewResolver;

    public ViewLocatorService(IViewResolverService viewResolver)
    {
        _viewResolver = viewResolver;
    }

    public Control Build(object data)
    {
        return _viewResolver.ResolveByModel(data);
    }

    public bool Match(object data)
    {
        var result = data is ReactiveObject;
        return result;
    }
}