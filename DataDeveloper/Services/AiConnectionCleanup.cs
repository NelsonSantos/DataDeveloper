using System;
using System.IO;
using DataDeveloper.Data.Ai;
using DataDeveloper.Interfaces;

namespace DataDeveloper.Services;

/// <summary>Deletes a connection's chats, the CLI sessions behind them and the connection's session folder.</summary>
public sealed class AiConnectionCleanup(
    IAiChatHistoryRepository history,
    IAiSettingsRepository settings,
    AiProviderFactoryService providerFactory) : IAiConnectionCleanup
{
    public void Forget(Guid connectionId)
    {
        try
        {
            var provider = providerFactory.Create(settings.Load(providerFactory.SupportedProviders[0].ProviderType));
            foreach (var chat in history.ListChats(connectionId))
                provider.ForgetSession(chat.Id);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The sessions are only a cache of the conversations; a CLI that is missing must not block the deletion.
        }

        history.DeleteConnectionChats(connectionId);

        try
        {
            if (providerFactory.GetSessionFolder(connectionId) is { } folder && Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}
