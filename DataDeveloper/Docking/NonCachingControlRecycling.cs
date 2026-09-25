using Avalonia.Controls;
using Avalonia.Controls.Recycling.Model;
using Avalonia.Controls.Templates;

namespace DataDeveloper.Docking;

/// <summary>
/// Control recycling that never caches. Dock shares one recycling cache between the main layout and its
/// floating windows and detaches a cached control from its current host when another host asks for it; while
/// a document is floated the new window can briefly request the main area's active document and leave the main
/// area blank. Views are cached per query editor by TabTemplateSelector/CachedViewHost instead.
/// </summary>
public sealed class NonCachingControlRecycling : IControlRecycling
{
    public bool TryToUseIdAsKey { get; set; }

    public bool TryGetValue(object? data, out object? control)
    {
        control = null;
        return false;
    }

    public void Add(object data, object control)
    {
    }

    public object? Build(object? data, object? existing, object? parent)
    {
        if (data is null || parent is not Control parentControl)
            return null;

        return parentControl.FindDataTemplate(data) switch
        {
            IRecyclingDataTemplate recyclingTemplate => recyclingTemplate.Build(data, existing as Control),
            { } template => template.Build(data),
            null => null
        };
    }

    public void Clear()
    {
    }
}
