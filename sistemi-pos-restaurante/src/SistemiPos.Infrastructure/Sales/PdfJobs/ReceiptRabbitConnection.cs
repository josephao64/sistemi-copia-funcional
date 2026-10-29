using RabbitMQ.Client;

namespace SistemiPos.Infrastructure.Sales.PdfJobs;

internal static class ReceiptRabbitConnection
{
    public static Task<IConnection> ConnectAsync(ReceiptQueueOptions options, string name, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(options.UserName) || string.IsNullOrWhiteSpace(options.Password))
            throw new InvalidOperationException("ReceiptQueue credentials are required when enabled.");
        var factory = new ConnectionFactory
        {
            HostName = options.HostName, Port = options.Port, VirtualHost = options.VirtualHost,
            UserName = options.UserName, Password = options.Password,
            AutomaticRecoveryEnabled = false, RequestedConnectionTimeout = TimeSpan.FromSeconds(5),
            ContinuationTimeout = TimeSpan.FromSeconds(10), RequestedHeartbeat = TimeSpan.FromSeconds(30),
            ClientProvidedName = name
        };
        factory.Ssl.Enabled = options.UseTls;
        factory.Ssl.ServerName = options.HostName;
        return factory.CreateConnectionAsync(ct);
    }
    public static async Task DeclareAsync(IChannel channel, ReceiptQueueOptions options, CancellationToken ct)
    {
        await channel.QueueDeclareAsync(queue: options.QueueName + ".failed", durable: true, exclusive: false,
            autoDelete: false, arguments: null, cancellationToken: ct);
        await channel.QueueDeclareAsync(queue: options.QueueName, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-queue-type"] = "quorum",
                ["x-dead-letter-exchange"] = "",
                ["x-dead-letter-routing-key"] = options.QueueName + ".failed"
            }, cancellationToken: ct);
    }
}
