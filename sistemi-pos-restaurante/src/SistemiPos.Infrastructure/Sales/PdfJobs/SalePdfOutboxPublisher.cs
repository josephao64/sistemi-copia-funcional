using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using SistemiPos.Application.Abstractions;
using SistemiPos.Infrastructure.Persistence;

namespace SistemiPos.Infrastructure.Sales.PdfJobs;

public sealed class SalePdfOutboxPublisher(IServiceScopeFactory scopes, IOptions<ReceiptQueueOptions> settings,
    ILogger<SalePdfOutboxPublisher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = settings.Value;
        if (!options.Enabled) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = await ReceiptRabbitConnection.ConnectAsync(options, "sistemi-pdf-publisher", stoppingToken);
                await using var channel = await connection.CreateChannelAsync(new CreateChannelOptions(
                    publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), stoppingToken);
                await ReceiptRabbitConnection.DeclareAsync(channel, options, stoppingToken);
                while (channel.IsOpen && !stoppingToken.IsCancellationRequested)
                {
                    await PublishBatchAsync(channel, options, stoppingToken);
                    await Task.Delay(Math.Clamp(options.PollMilliseconds, 250, 10000), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                logger.LogWarning("PDF publisher unavailable ({FailureType}); durable outbox remains pending.", ex.GetType().Name);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
    private async Task PublishBatchAsync(IChannel channel, ReceiptQueueOptions options, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var messages = await db.Set<SalePdfOutbox>().FromSqlInterpolated($"SELECT * FROM \"SalePdfOutbox\" WHERE \"PublishedAtUtc\" IS NULL AND \"AvailableAtUtc\" <= {clock.UtcNow} ORDER BY \"AvailableAtUtc\", \"Id\" LIMIT 10 FOR UPDATE SKIP LOCKED")
            .ToListAsync(ct);
        foreach (var message in messages)
        {
            await channel.BasicPublishAsync(exchange: "", routingKey: options.QueueName, mandatory: true,
                basicProperties: new BasicProperties
                {
                    Persistent = true, ContentType = "text/plain", Type = "SalePdfRequested", MessageId = message.Id.ToString("N")
                }, body: Encoding.UTF8.GetBytes(message.JobId.ToString("N")), cancellationToken: ct);
            // Awaiting BasicPublish with confirmation tracking confirms broker acceptance.
            message.Published(clock.UtcNow);
        }
        if (messages.Count > 0) await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
