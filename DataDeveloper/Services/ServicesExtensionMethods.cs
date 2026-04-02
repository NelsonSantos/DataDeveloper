using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace DataDeveloper.Services;

public static class ServicesExtensionMethods
{
    public static Window? TryGetParentWindow(this StyledElement element)
    {
        if (element is Visual visual && TopLevel.GetTopLevel(visual) is Window topLevelWindow)
            return topLevelWindow;

        StyledElement? target = element;
        while (target is not null)
        {
            if (target is Window window)
                return window;

            target = target.Parent;
        }

        return null;
    }

    public static Window GetParentWindow(this StyledElement element)
    {
        return element.TryGetParentWindow()
               ?? throw new Exception("Could not get the parent window");
    }
}
