using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;

namespace TaskApi.Models;

public class CreateTaskRequest
{
    [Required(ErrorMessage = "Title is required.")]
    [MaxLength(200, ErrorMessage = "Title must not exceed 200 characters.")]
    public string Title { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Priority Priority { get; set; } = Priority.Medium;
}
