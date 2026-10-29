using System.Text;

namespace DataDeveloper.Data.Ai;

public sealed record AiTextAttachment(string Name, string Content);

public static class AiPromptComposer
{
    public const int MaxAttachmentCharacters = 60_000;

    /// <summary>
    /// Appends text attachments (as fenced blocks, truncated when huge) to the user's message, after the structure of the
    /// relevant database objects when there is any.
    /// </summary>
    public static string Compose(string userText, IReadOnlyList<AiTextAttachment> attachments, string? structureContext = null)
    {
        if (attachments.Count == 0 && string.IsNullOrWhiteSpace(structureContext))
            return userText;

        var builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(structureContext))
            builder.AppendLine(structureContext.TrimEnd()).AppendLine();

        builder.Append(userText);
        foreach (var attachment in attachments)
        {
            var content = attachment.Content;
            var truncated = content.Length > MaxAttachmentCharacters;
            if (truncated)
                content = content[..MaxAttachmentCharacters];

            builder.AppendLine().AppendLine();
            builder.AppendLine($"Attached \"{attachment.Name}\"{(truncated ? " (truncated)" : string.Empty)}:");
            builder.AppendLine("~~~");
            builder.AppendLine(content);
            builder.Append("~~~");
        }

        return builder.ToString();
    }

    /// <summary>What is kept in the chat history for a turn: the typed text plus the names of what was attached.</summary>
    public static string DescribeForHistory(string userText, IReadOnlyList<AiTextAttachment> attachments, IReadOnlyList<string> objectNames)
    {
        var names = attachments.Select(a => a.Name).Concat(objectNames).ToList();
        return names.Count == 0 ? userText : $"{userText}\n\n(Attached: {string.Join(", ", names)})";
    }
}
