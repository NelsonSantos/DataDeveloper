using System;
using Avalonia;
using Avalonia.Controls;

namespace DataDeveloper.Services;

public static class ServicesExtensionMethods
{
    public static Window GetParentWindow(this StyledElement element)
    {
        var target = element.Parent;
        
        if (target == null) throw new Exception("Could not get the parent window");
        if (target is Window window) return window;
        
        return GetParentWindow(target);
    }
}