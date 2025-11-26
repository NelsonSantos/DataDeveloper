using System;
using Avalonia.Controls;

namespace DataDeveloper.Interfaces;

public interface IViewResolverService
{
    void Register<TViewModel, TView>()
        where TView : Control, new();

    Control ResolveByModel(object viewModel);
    Control ResolveByType<TType>();
    Control ResolveByType(Type viewType);
}
