using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace DataDeveloper.TemplateSelectors;

public class TabQueryEditorTemplateSelector : IDataTemplate
{
    
    private Dictionary<Guid, Control> _controls = new();
    
    public Control? Build(object? param)
    {
        throw new NotImplementedException();
    }

    public bool Match(object? data)
    {
        throw new NotImplementedException();
    }
}