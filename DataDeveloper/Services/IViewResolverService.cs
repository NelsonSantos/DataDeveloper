namespace DataDeveloper.Services;

using Avalonia.Controls;

public interface IViewResolverService
{
    void Register<TViewModel, TView>()
        where TView : Control, new();

    Control Resolve(object viewModel);
}
