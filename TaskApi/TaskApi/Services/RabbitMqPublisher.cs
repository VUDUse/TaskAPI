using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using TaskApi.Models;

namespace TaskApi.Services;

/// <summary>
/// Издатель сообщений в RabbitMQ с корректным управлением жизненным циклом.
/// Реализует IHostedService для graceful shutdown.
/// </summary>
public class RabbitMqPublisher : IRabbitMqPublisher, IHostedService, IDisposable
{
    private IConnection? _connection;
    private IModel? _channel;
    private readonly ILogger<RabbitMqPublisher> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _exchangeName = "task.events";
    private readonly string _routingKey = "task.completed";
    private bool _disposed;

    public RabbitMqPublisher(IConfiguration configuration, ILogger<RabbitMqPublisher> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var hostName = _configuration["RabbitMQ:HostName"] ?? "localhost";
        var port = _configuration.GetValue<int>("RabbitMQ:Port", 5672);
        var userName = _configuration["RabbitMQ:UserName"] ?? "guest";
        var password = _configuration["RabbitMQ:Password"] ?? "guest";

        var factory = new ConnectionFactory
        {
            HostName = hostName,
            Port = port,
            UserName = userName,
            Password = password,
            AutomaticRecoveryEnabled = true
        };

        try
        {
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            _channel.ExchangeDeclare(_exchangeName, ExchangeType.Topic, durable: true);
            _logger.LogInformation("RabbitMQ publisher initialized (exchange={Exchange}).", _exchangeName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize RabbitMQ publisher at startup.");
        }

        return Task.CompletedTask;
    }

    public void PublishTaskCompleted(TaskCompletedEvent eventMessage)
    {
        if (_disposed || _channel is null || _channel.IsClosed)
        {
            _logger.LogWarning("RabbitMQ channel unavailable. Dropping message for TaskId={TaskId}.", eventMessage.TaskId);
            return;
        }

        try
        {
            var body = JsonSerializer.Serialize(eventMessage);
            var bytes = Encoding.UTF8.GetBytes(body);

            var properties = _channel.CreateBasicProperties();
            properties.ContentType = "application/json";
            properties.DeliveryMode = 2; // persistent

            _channel.BasicPublish(
                exchange: _exchangeName,
                routingKey: _routingKey,
                basicProperties: properties,
                body: bytes);

            _logger.LogInformation("Published task.completed event for TaskId={TaskId}", eventMessage.TaskId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish task.completed event for TaskId={TaskId}. Message dropped.", eventMessage.TaskId);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("RabbitMQ publisher stopping...");
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            _channel?.Close();
            _channel?.Dispose();
            _connection?.Close();
            _connection?.Dispose();
            _logger.LogInformation("RabbitMQ publisher disposed gracefully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during RabbitMQ publisher disposal.");
        }
    }
}
