namespace DataDeveloper.Data.Ai;

public interface IAiProvider
{
    AiProviderType ProviderType { get; }
    string DisplayName { get; }

    bool SupportsSignIn { get; }

    Task<AiProviderAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken = default);

    Task<AiChatResponse> SendMessageAsync(AiChatRequest request, CancellationToken cancellationToken = default);

    /// <summary>Removes what the CLI stored for a conversation (its session); no-op when there is none.</summary>
    void ForgetSession(Guid sessionId)
    {
    }

    /// <summary>Starts the vendor's own sign-in flow (usually opens the browser) and waits for it to finish.</summary>
    Task<AiChatResponse> SignInAsync(CancellationToken cancellationToken = default);
}
