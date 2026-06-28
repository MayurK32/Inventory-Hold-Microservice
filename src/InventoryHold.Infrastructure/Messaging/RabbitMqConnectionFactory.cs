using InventoryHold.Contracts.Settings;
using RabbitMQ.Client;

namespace InventoryHold.Infrastructure.Messaging;

public static class RabbitMqConnectionFactory
{
    public static async Task<IConnection> CreateAsync(RabbitMqSettings settings)
    {
        var factory = new ConnectionFactory
        {
            HostName = settings.Host,
            Port = settings.Port,
            UserName = settings.Username,
            Password = settings.Password,
            VirtualHost = settings.VirtualHost,
            AutomaticRecoveryEnabled = true
        };
        return await factory.CreateConnectionAsync();
    }
}
