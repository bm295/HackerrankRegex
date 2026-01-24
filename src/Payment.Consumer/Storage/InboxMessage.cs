namespace Payment.Consumer.Storage;

public class InboxMessage
{
    public Guid MessageId { get; set; }
    public string Consumer { get; set; } = string.Empty;
    public DateTime ReceivedOn { get; set; }
}
