using DataDeveloper.ViewModels;

namespace DataDeveloper.Models;

/// <summary>
/// ItemsSource entry for a tool (schema explorer, results) in a connection's dock layout.
/// Dock maps <see cref="Title"/> and <see cref="CanClose"/> onto the generated tool.
/// </summary>
public sealed class ConnectionToolItem(string title, TabConnectionViewModel connection)
{
    public string Title { get; } = title;

    public bool CanClose => false;

    public TabConnectionViewModel Connection { get; } = connection;
}
