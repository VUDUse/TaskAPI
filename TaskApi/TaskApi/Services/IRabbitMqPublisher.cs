using TaskApi.Models;

namespace TaskApi.Services;

public interface IRabbitMqPublisher
{
    void PublishTaskCompleted(TaskCompletedEvent eventMessage);
}
