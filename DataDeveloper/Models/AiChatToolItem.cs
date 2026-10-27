using DataDeveloper.ViewModels;

namespace DataDeveloper.Models;

/// <summary>ItemsSource entry for the AI chat tool in a connection's dock layout.</summary>
public sealed class AiChatToolItem(string title, AiChatViewModel chat)
{
    public string Title { get; } = title;

    public bool CanClose => false;

    public AiChatViewModel Chat { get; } = chat;
}
