namespace Order.Infrastructure.Entities;

public class OutboxMessage
{
    public Guid Id { get; set; }
    public DateTime OccurredOn { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string? TraceParent { get; set; }
    public string? CorrelationId { get; set; }
    public DateTime? ProcessedOn { get; set; }
    public int Attempts { get; set; }
}
