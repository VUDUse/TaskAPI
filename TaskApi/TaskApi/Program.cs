using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TaskApi.Data;
using TaskApi.Models;
using TaskApi.Services;

var builder = WebApplication.CreateBuilder(args);

// ─── Graceful shutdown timeout ──────────────────────────────────────────────
builder.Services.Configure<HostOptions>(options =>
    options.ShutdownTimeout = TimeSpan.FromSeconds(10));

// ─── Database ─────────────────────────────────────────────────────────────────
// Используем InMemory если указано в конфиге (для тестов), иначе PostgreSQL
var useInMemory = builder.Configuration.GetValue<bool>("UseInMemoryDatabase");
if (useInMemory)
{
    builder.Services.AddDbContext<TaskDbContext>(options =>
        options.UseInMemoryDatabase("TestDb"));
}
else
{
    builder.Services.AddDbContext<TaskDbContext>(options =>
        options.UseNpgsql(
            builder.Configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Database=taskdb;Username=postgres;Password=postgres"));
}

// ─── RabbitMQ ─────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<RabbitMqPublisher>();
builder.Services.AddSingleton<IRabbitMqPublisher>(sp => sp.GetRequiredService<RabbitMqPublisher>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<RabbitMqPublisher>());

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var app = builder.Build();

// ─── Endpoints ──────────────────────────────────────────────────────────────

// POST /tasks  — создать задачу
app.MapPost("/tasks", async (CreateTaskRequest request, TaskDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 200)
    {
        return Results.BadRequest(new { error = "Title is required and must not exceed 200 characters." });
    }

    var task = new TaskItem
    {
        Id = Guid.NewGuid(),
        Title = request.Title.Trim(),
        IsCompleted = false,
        CreatedAt = DateTimeOffset.UtcNow,
        Priority = request.Priority
    };

    db.Tasks.Add(task);
    await db.SaveChangesAsync();

    return Results.Created($"/tasks/{task.Id}", task);
});

// GET /tasks  — получить все задачи
app.MapGet("/tasks", async (TaskDbContext db) =>
{
    var tasks = await db.Tasks.AsNoTracking().ToListAsync();
    return Results.Ok(tasks);
});

// PUT /tasks/{id}/complete  — завершить задачу
app.MapPut("/tasks/{id}/complete", async (
    Guid id,
    TaskDbContext db,
    IRabbitMqPublisher publisher,
    ILogger<Program> logger) =>
{
    var task = await db.Tasks.FindAsync(id);
    if (task is null)
    {
        return Results.NotFound(new { error = "Task not found." });
    }

    if (task.IsCompleted)
    {
        return Results.Conflict(new { error = "Task is already completed." });
    }

    task.IsCompleted = true;
    task.CompletedAt = DateTimeOffset.UtcNow;

    try
    {
        await db.SaveChangesAsync();
    }
    catch (DbUpdateConcurrencyException)
    {
        return Results.Conflict(new { error = "Task was modified by another request." });
    }

    // Публикуем событие ПОСЛЕ успешного сохранения в БД
    var eventMessage = new TaskCompletedEvent
    {
        TaskId = task.Id,
        Title = task.Title,
        CompletedAt = task.CompletedAt.Value,
        Priority = task.Priority.ToString()
    };

    publisher.PublishTaskCompleted(eventMessage);

    return Results.Ok(task);
});

// DELETE /tasks/{id}  — удалить задачу
app.MapDelete("/tasks/{id}", async (Guid id, TaskDbContext db) =>
{
    var task = await db.Tasks.FindAsync(id);
    if (task is null)
    {
        return Results.NotFound(new { error = "Task not found." });
    }

    db.Tasks.Remove(task);
    await db.SaveChangesAsync();

    return Results.NoContent();
});

app.Run();

// Делаем Program доступным для интеграционных тестов (WebApplicationFactory)
public partial class Program { }
