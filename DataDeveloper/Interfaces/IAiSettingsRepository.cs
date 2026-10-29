using DataDeveloper.Data.Ai;

namespace DataDeveloper.Interfaces;

public interface IAiSettingsRepository
{
    AiProviderSettings Load(AiProviderType providerType);
    void Save(AiProviderSettings settings);

    /// <summary>True when the chat sends with Cmd/Ctrl+Enter (Enter then adds a line); false when Enter sends.</summary>
    bool LoadSendWithModifier();

    void SaveSendWithModifier(bool value);
}
