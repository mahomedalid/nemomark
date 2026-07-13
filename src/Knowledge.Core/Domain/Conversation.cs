namespace Knowledge.Core.Domain;

public class Conversation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string? UserId { get; set; }

    public DateTime StartedUtc { get; set; } = DateTime.UtcNow;

    public DateTime? EndedUtc { get; set; }

    public List<ConversationMessage> Messages { get; set; } = new();
}
