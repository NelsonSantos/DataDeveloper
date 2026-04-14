using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaEdit.CodeCompletion;

namespace DataDeveloper.Services;

public sealed class SqlFunctionOverloadProvider : IOverloadProvider, INotifyPropertyChanged
{
    private readonly SqlFunctionDefinition _function;
    private int _selectedIndex;
    private int _argumentIndex;

    public SqlFunctionOverloadProvider(SqlFunctionDefinition function, int argumentIndex)
    {
        _function = function;
        _argumentIndex = Math.Max(0, argumentIndex);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            var safeValue = Math.Clamp(value, 0, Count - 1);
            if (_selectedIndex == safeValue)
                return;

            _selectedIndex = safeValue;
            RaiseCurrentPropertiesChanged();
        }
    }

    public int Count => 1;

    public string CurrentIndexText => string.Empty;

    public object CurrentHeader => BuildHeader();

    public object CurrentContent => _function.Description;

    public void UpdateArgumentIndex(int argumentIndex)
    {
        var safeValue = Math.Max(0, argumentIndex);
        if (_argumentIndex == safeValue)
            return;

        _argumentIndex = safeValue;
        RaiseCurrentPropertiesChanged();
    }

    private object BuildHeader()
    {
        var monospace = TryGetFont("MonospaceFont");
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 0
        };

        panel.Children.Add(CreateText(_function.Name, Brushes.DeepSkyBlue, monospace));
        panel.Children.Add(CreateText("(", Brushes.LightGray, monospace));

        for (var index = 0; index < _function.Parameters.Count; index++)
        {
            if (index > 0)
                panel.Children.Add(CreateText(", ", Brushes.LightGray, monospace));

            var parameter = _function.Parameters[index];
            var isActive = index == Math.Min(_argumentIndex, _function.Parameters.Count - 1);
            panel.Children.Add(CreateText(parameter, isActive ? Brushes.Khaki : Brushes.White, monospace, isActive ? FontWeight.Bold : FontWeight.Normal));
        }

        if (_function.AcceptsAdditionalArguments && _function.Parameters.Count > 0)
        {
            panel.Children.Add(CreateText(", ...", _argumentIndex >= _function.Parameters.Count ? Brushes.Khaki : Brushes.LightGray, monospace));
        }
        else if (_function.AcceptsAdditionalArguments)
        {
            panel.Children.Add(CreateText("...", Brushes.Khaki, monospace));
        }

        panel.Children.Add(CreateText(")", Brushes.LightGray, monospace));
        return panel;
    }

    private static TextBlock CreateText(string text, IBrush foreground, FontFamily? fontFamily, FontWeight fontWeight = FontWeight.Normal)
    {
        var textBlock = new TextBlock
        {
            Text = text,
            Foreground = foreground,
            FontWeight = fontWeight,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (fontFamily is not null)
            textBlock.FontFamily = fontFamily;

        return textBlock;
    }

    private static FontFamily? TryGetFont(string key)
    {
        return Application.Current?.Resources[key] as FontFamily;
    }

    private void RaiseCurrentPropertiesChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentHeader)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentContent)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentIndexText)));
    }
}
