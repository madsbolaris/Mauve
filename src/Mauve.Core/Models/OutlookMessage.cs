namespace Mauve.Core.Models;

public class OutlookMessage(string conversationId)
{
    public string ConversationId { get; set; } = conversationId;
    public string? MessageId { get; set; }
    public string? Subject { get; set; }
    public DateTime Timestamp { get; set; }
    public string? Body { get; set; }
    public string? PreviousMessageId { get; set; }

    public List<Person>? From { get; set; }
    public List<Person>? To { get; set; }
    public List<Person>? Cc { get; set; }
    public List<OutlookEmailImage>? Images { get; set; }
    public string? Summary { get; set; }
}