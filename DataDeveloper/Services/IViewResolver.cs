namespace DataDeveloper.Services;

using Avalonia.Controls;

public interface IViewResolver
{
    void Register<TViewModel, TView>()
        where TView : Control, new();

    Control Resolve(object viewModel);
}
