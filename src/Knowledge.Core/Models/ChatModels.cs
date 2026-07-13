using Knowledge.Core.Domain;

namespace Knowledge.Core.Models;

/// <summary>Incoming chat request.</summary>
public class ChatRequest
{
    public Guid? ConversationId { get; set; }

    public string? UserId { get; set; }

    public string Message { get; set; } = string.Empty;
}

/// <summary>A citation pointing back to a source chunk used to answer.</summary>
public record Citation(Guid ChunkId, string? DocumentTitle, string? Heading, double Score);

/// <summary>Chat response returned to the caller.</summary>
public class ChatResponse
{
    public Guid ConversationId { get; set; }

    public string Answer { get; set; } = string.Empty;

    public IntentResult? Intent { get; set; }

    public List<Citation> Citations { get; set; } = new();
}
