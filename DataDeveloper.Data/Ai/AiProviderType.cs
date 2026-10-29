namespace DataDeveloper.Data.Ai;

/// <summary>How one call relates to the CLI's own stored conversations.</summary>
public enum AiSessionMode
{
    /// <summary>Independent call: nothing is stored and the history travels with the message.</summary>
    None,

    /// <summary>First call of a conversation: the CLI stores it under the session id.</summary>
    Create,

    /// <summary>Later call: the CLI already holds the conversation and gets only the new message.</summary>
    Resume
}

public enum AiProviderType
{
    ClaudeCode = 1
}
