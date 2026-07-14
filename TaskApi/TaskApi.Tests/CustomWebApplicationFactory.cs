using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using TaskApi.Services;
using Moq;

namespace TaskApi.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public Mock<IRabbitMqPublisher> RabbitMqPublisherMock { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Устанавливаем конфиг ДО создания хоста
        builder.UseSetting("UseInMemoryDatabase", "true");

        builder.ConfigureServices(services =>
        {
            // Удаляем реальный издатель
            var publisherDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IRabbitMqPublisher));
            if (publisherDescriptor != null)
                services.Remove(publisherDescriptor);

            // Мок издателя
            services.AddSingleton(RabbitMqPublisherMock.Object);
        });
    }
}
