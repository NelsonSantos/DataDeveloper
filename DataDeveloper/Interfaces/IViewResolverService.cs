using Avalonia.Controls;

namespace DataDeveloper.Interfaces;

public interface IViewResolverService
{
    void Register<TViewModel, TView>()
        where TView : Control, new();

    Control Resolve(object viewModel);
}
