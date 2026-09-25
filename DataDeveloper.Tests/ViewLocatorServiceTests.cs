using System;
using Avalonia.Controls;
using DataDeveloper.Core;
using DataDeveloper.Enums;
using DataDeveloper.Interfaces;
using DataDeveloper.Models;
using DataDeveloper.Services;
using Xunit;

namespace DataDeveloper.Tests;

public class ViewLocatorServiceTests
{
    [Fact]
    public void Match_TabContent_IsLeftToTabTemplateSelector()
    {
        // TabControl sets Content before ContentTemplate; matching tab content here built an orphan duplicate
        // view (e.g. a second TabConnectionView with its own dock layout) that stayed subscribed to the view model.
        var locator = new ViewLocatorService(new NoViewsResolver());

        Assert.False(locator.Match(new FakeTab()));
    }

    [Fact]
    public void Match_OtherViewModels_StillResolvesViews()
    {
        var locator = new ViewLocatorService(new NoViewsResolver());

        Assert.True(locator.Match(new FakeViewModel()));
        Assert.False(locator.Match("not a view model"));
    }

    private sealed class FakeTab() : BaseTabContent(TabType.Document, "tab", canClose: true, new NoServices());

    private sealed class FakeViewModel : ViewModelBase;

    private sealed class NoServices : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private sealed class NoViewsResolver : IViewResolverService
    {
        public Control ResolveByModel(object viewModel) => throw new NotSupportedException();
        public Control ResolveByType<TType>() => throw new NotSupportedException();
        public Control ResolveByType(Type viewType) => throw new NotSupportedException();
        public void Register<TViewModel, TView>() where TView : Control, new() => throw new NotSupportedException();
    }
}
