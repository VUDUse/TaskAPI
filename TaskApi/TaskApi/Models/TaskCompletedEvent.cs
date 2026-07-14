namespace TaskApi.Models;

public class TaskCompletedEvent
{
    public Guid TaskId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTimeOffset CompletedAt { get; set; }
    public string Priority { get; set; } = string.Empty;
}
