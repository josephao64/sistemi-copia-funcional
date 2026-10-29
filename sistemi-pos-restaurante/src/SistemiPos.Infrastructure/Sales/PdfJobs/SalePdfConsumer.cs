using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace SistemiPos.Infrastructure.Sales.PdfJobs;

public sealed class SalePdfConsumer(IServiceScopeFactory scopes, IOptions<ReceiptQueueOptions> settings,
    ILogger<SalePdfConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = settings.Value;
        if (!options.Enabled) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = await ReceiptRabbitConnection.ConnectAsync(options, "sistemi-pdf-consumer", stoppingToken);
                await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
                await ReceiptRabbitConnection.DeclareAsync(channel, options, stoppingToken);
                await channel.BasicQosAsync(0, 1, false, stoppingToken);
                var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                channel.ChannelShutdownAsync += (_, _) => { closed.TrySetResult(); return Task.CompletedTask; };
                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (_, delivery) =>
                {
                    if (!Guid.TryParse(Encoding.UTF8.GetString(delivery.Body.Span), out var jobId))
                    {
                        await channel.BasicNackAsync(delivery.DeliveryTag, false, false, stoppingToken);
                        return;
                    }
                    try
                    {
                        await using var scope = scopes.CreateAsyncScope();
                        await scope.ServiceProvider.GetRequiredService<SalePdfProcessor>().ProcessAsync(jobId, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                    catch (Exception ex)
                    {
                        logger.LogWarning("PDF job {JobId} failed ({FailureType}).", jobId, ex.GetType().Name);
                        try
                        {
                            await using var failureScope = scopes.CreateAsyncScope();
                            var exhausted = await failureScope.ServiceProvider.GetRequiredService<SalePdfProcessor>()
                                .RecordFailureAsync(jobId, options.MaximumAttempts, stoppingToken);
                            if (exhausted)
                            {
                                await channel.BasicNackAsync(delivery.DeliveryTag, false, false, stoppingToken);
                                return;
                            }
                        }
                        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                        catch (Exception failure)
                        {
                            logger.LogWarning("PDF retry persistence unavailable ({FailureType}).", failure.GetType().Name);
                            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                            if (channel.IsOpen) await channel.BasicNackAsync(delivery.DeliveryTag, false, true, stoppingToken);
                            return;
                        }
                    }
                    // Only acknowledge after the PDF or its retry has been committed.
                    if (channel.IsOpen) await channel.BasicAckAsync(delivery.DeliveryTag, false, stoppingToken);
                };
                await channel.BasicConsumeAsync(options.QueueName, false, consumer, stoppingToken);
                await closed.Task.WaitAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                logger.LogWarning("PDF consumer unavailable ({FailureType}); awaiting recovery.", ex.GetType().Name);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
