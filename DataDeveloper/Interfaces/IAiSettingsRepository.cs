using DataDeveloper.Data.Ai;

namespace DataDeveloper.Interfaces;

public interface IAiSettingsRepository
{
    AiProviderSettings Load(AiProviderType providerType);
    void Save(AiProviderSettings settings);
}
