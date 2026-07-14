using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TaskApi.Models;
using Moq;
using Xunit;

namespace TaskApi.Tests;

public class CompleteTaskIntegrationTest : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public CompleteTaskIntegrationTest(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Create_And_Complete_Task_Should_Publish_RabbitMq_Event()
    {
        // ─── 1. Создать задачу ───────────────────────────────────────────
        var createRequest = new CreateTaskRequest
        {
            Title = "Buy milk",
            Priority = Priority.High
        };

        var createResponse = await _client.PostAsJsonAsync("/tasks", createRequest);
        createResponse.EnsureSuccessStatusCode();

        var createdTask = await createResponse.Content.ReadFromJsonAsync<TaskItem>();
        Assert.NotNull(createdTask);
        Assert.False(createdTask.IsCompleted);
        Assert.Null(createdTask.CompletedAt);

        // ─── 2. Завершить задачу ────────────────────────────────────────
        var completeResponse = await _client.PutAsync($"/tasks/{createdTask.Id}/complete", null);
        completeResponse.EnsureSuccessStatusCode();

        var completedTask = await completeResponse.Content.ReadFromJsonAsync<TaskItem>();
        Assert.NotNull(completedTask);
        Assert.True(completedTask.IsCompleted);
        Assert.NotNull(completedTask.CompletedAt);

        // ─── 3. Проверить вызов RabbitMQ ────────────────────────────────
        _factory.RabbitMqPublisherMock.Verify(
            p => p.PublishTaskCompleted(It.Is<TaskCompletedEvent>(e =>
                e.TaskId == createdTask.Id &&
                e.Title == "Buy milk" &&
                e.Priority == "High" &&
                e.CompletedAt != default)),
            Times.Once);
    }

    [Fact]
    public async Task Create_Task_With_Empty_Title_Should_Return_400()
    {
        var createRequest = new CreateTaskRequest
        {
            Title = "   ",
            Priority = Priority.Medium
        };

        var response = await _client.PostAsJsonAsync("/tasks", createRequest);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_Task_With_Too_Long_Title_Should_Return_400()
    {
        var createRequest = new CreateTaskRequest
        {
            Title = new string('x', 201),
            Priority = Priority.Low
        };

        var response = await _client.PostAsJsonAsync("/tasks", createRequest);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Complete_Already_Completed_Task_Should_Return_409()
    {
        // Создаём и завершаем задачу
        var createRequest = new CreateTaskRequest { Title = "Test task" };
        var createResponse = await _client.PostAsJsonAsync("/tasks", createRequest);
        createResponse.EnsureSuccessStatusCode();
        var task = await createResponse.Content.ReadFromJsonAsync<TaskItem>();
        Assert.NotNull(task);

        var firstComplete = await _client.PutAsync($"/tasks/{task.Id}/complete", null);
        firstComplete.EnsureSuccessStatusCode();

        // Повторное завершение → 409
        var secondComplete = await _client.PutAsync($"/tasks/{task.Id}/complete", null);
        Assert.Equal(HttpStatusCode.Conflict, secondComplete.StatusCode);
    }

    [Fact]
    public async Task Delete_NonExistent_Task_Should_Return_404()
    {
        var response = await _client.DeleteAsync($"/tasks/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_All_Tasks_Should_Return_List()
    {
        var response = await _client.GetAsync("/tasks");
        response.EnsureSuccessStatusCode();

        var tasks = await response.Content.ReadFromJsonAsync<List<TaskItem>>();
        Assert.NotNull(tasks);
    }
}
