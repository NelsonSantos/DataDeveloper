namespace DataDeveloper.Data.Ai;

public interface IAiProvider
{
    AiProviderType ProviderType { get; }
    string DisplayName { get; }

    Task<AiProviderAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken = default);

    Task<AiChatResponse> SendMessageAsync(AiChatRequest request, CancellationToken cancellationToken = default);
}
