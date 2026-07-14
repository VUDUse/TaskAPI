using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;

namespace TaskApi.Models;

public class TaskItem
{
    public Guid Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public bool IsCompleted { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Priority Priority { get; set; }

    /// <summary>
    /// RowVersion для оптимистичной конкурентности (EF Core).
    /// </summary>
    [Timestamp]
    public byte[]? RowVersion { get; set; }
}
