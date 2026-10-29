using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using RabbitMQ.Client;
using SistemiPos.Application.Abstractions;
using SistemiPos.Application.Organizations;
using SistemiPos.Application.Sales;
using SistemiPos.Application.Security;
using SistemiPos.Domain.Cash;
using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Organizations;
using SistemiPos.Domain.Sales;
using SistemiPos.Infrastructure.Persistence;
using SistemiPos.Infrastructure.Sales;
using SistemiPos.Infrastructure.Sales.PdfJobs;
using SistemiPos.Infrastructure.Security;

namespace SistemiPos.IntegrationTests.Infrastructure;

public sealed class RabbitPdfFactAttribute : FactAttribute
{
    public RabbitPdfFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SISTEMI_TEST_CONNECTION"))
            || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SISTEMI_TEST_RABBIT_PASSWORD")))
            Skip = "Requiere PostgreSQL y RabbitMQ de pruebas configurados explícitamente.";
    }
}

public sealed class QueuedSalePdfTests
{
    [Fact]
    public void Completed_Job_Is_Idempotent_And_Outbox_Retry_Remains_Durable()
    {
        var now = DateTimeOffset.UtcNow;
        var job = new SalePdfJob(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "key", "V-1", now);
        var outbox = new SalePdfOutbox(job.Id, now);
        outbox.Published(now);
        outbox.Retry(now.AddSeconds(10));
        Assert.Null(outbox.PublishedAtUtc);
        Assert.Equal(now.AddSeconds(10), outbox.AvailableAtUtc);
        job.Complete([1, 2, 3], now);
        job.Complete([9], now.AddSeconds(1));
        job.FailedAttempt(1);
        Assert.Equal("Completed", job.Status);
        Assert.Equal(1, job.Attempts);
        Assert.Equal(new byte[] { 1, 2, 3 }, job.Content);
    }

    [PostgresFact]
    public async Task Pdf_Request_And_Outbox_Are_Atomic_Scoped_And_Repeated_Requests_Create_One_Job()
    {
        await WithDatabase(async (options, seed) =>
        {
            await using var db = new AppDbContext(options);
            var service = Queue(db, seed.User, seed.BranchId);
            var key = Guid.NewGuid().ToString();
            var first = await service.RequestAsync(seed.SaleId, key);
            Assert.Equal(first.Id, (await service.RequestAsync(seed.SaleId, key)).Id);
            Assert.Single(await db.Set<SalePdfJob>().ToListAsync());
            Assert.Single(await db.Set<SalePdfOutbox>().ToListAsync());
            Assert.Null(await service.DownloadAsync(first.Id));
            Assert.Null(await Queue(db, new User(Guid.NewGuid()), seed.BranchId).GetAsync(first.Id));
            Assert.Null(await Queue(db, seed.User, Guid.NewGuid()).GetAsync(first.Id));
            await using (var rollback = await db.Database.BeginTransactionAsync())
            {
                var job = new SalePdfJob(seed.SaleId, seed.BranchId, seed.User.UserId!.Value, "rollback", "V-TEST", DateTimeOffset.UtcNow);
                db.Add(job); db.Add(new SalePdfOutbox(job.Id, DateTimeOffset.UtcNow));
                await db.SaveChangesAsync();
                await rollback.RollbackAsync();
            }
            db.ChangeTracker.Clear();
            Assert.Single(await db.Set<SalePdfJob>().ToListAsync());
            Assert.Single(await db.Set<SalePdfOutbox>().ToListAsync());
            var processor = new SalePdfProcessor(db, new SaleReceiptService(db), new Clock());
            Assert.False(await processor.RecordFailureAsync(first.Id, 2, default));
            Assert.True(await processor.RecordFailureAsync(first.Id, 2, default));
            var failed = await service.GetAsync(first.Id);
            Assert.Equal("Failed", failed!.Status);
            Assert.Equal(2, failed.Attempts);
        });
    }

    [RabbitPdfFact]
    public async Task RabbitMQ_Processes_The_Outbox_And_Redelivery_Does_Not_Generate_A_Second_Pdf()
    {
        await WithDatabase(async (options, seed) =>
        {
            var rabbit = new ReceiptQueueOptions
            {
                Enabled = true, HostName = "127.0.0.1", Port = 5678, UserName = "sistemi_receipts",
                Password = Environment.GetEnvironmentVariable("SISTEMI_TEST_RABBIT_PASSWORD")!,
                QueueName = "sistemi.test.pdf." + Guid.NewGuid().ToString("N"), PollMilliseconds = 250
            };
            var services = new ServiceCollection();
            services.AddLogging(); services.AddScoped(_ => new AppDbContext(options));
            services.AddSingleton<IDateTimeProvider>(new Clock());
            services.AddScoped<ISaleReceiptService, SaleReceiptService>();
            services.AddScoped<SalePdfProcessor>();
            services.AddSingleton<IOptions<ReceiptQueueOptions>>(Options.Create(rabbit));
            await using var provider = services.BuildServiceProvider();
            await using var requestDb = new AppDbContext(options);
            var queue = Queue(requestDb, seed.User, seed.BranchId);
            var job = await queue.RequestAsync(seed.SaleId, Guid.NewGuid().ToString());
            // The durable request exists while no publisher/consumer is running.
            Assert.Equal("Pending", (await queue.GetAsync(job.Id))!.Status);
            Assert.Null((await requestDb.Set<SalePdfOutbox>().SingleAsync()).PublishedAtUtc);
            var factory = new ConnectionFactory { HostName = rabbit.HostName, Port = rabbit.Port, UserName = rabbit.UserName, Password = rabbit.Password };
            await using var connection = await factory.CreateConnectionAsync();
            await using var channel = await connection.CreateChannelAsync();
            var publisher = new SalePdfOutboxPublisher(provider.GetRequiredService<IServiceScopeFactory>(), Options.Create(rabbit),
                provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SalePdfOutboxPublisher>>());
            var consumer = new SalePdfConsumer(provider.GetRequiredService<IServiceScopeFactory>(), Options.Create(rabbit),
                provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SalePdfConsumer>>());
            try
            {
                await publisher.StartAsync(default); await consumer.StartAsync(default);
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                while ((await queue.GetAsync(job.Id, timeout.Token))!.Status != "Completed")
                    await Task.Delay(100, timeout.Token);
                var download = await queue.DownloadAsync(job.Id);
                Assert.NotNull(download);
                Assert.StartsWith("%PDF", Encoding.ASCII.GetString(download.Content));
                await channel.BasicPublishAsync(exchange: "", routingKey: rabbit.QueueName, body: Encoding.UTF8.GetBytes("invalid-job"));
                BasicGetResult? deadLetter = null;
                while (deadLetter is null)
                {
                    timeout.Token.ThrowIfCancellationRequested();
                    deadLetter = await channel.BasicGetAsync(rabbit.QueueName + ".failed", true, timeout.Token);
                    if (deadLetter is null) await Task.Delay(100, timeout.Token);
                }
                Assert.Equal("invalid-job", Encoding.UTF8.GetString(deadLetter.Body.Span));
                await publisher.StopAsync(default); await consumer.StopAsync(default);
                await channel.BasicPublishAsync(exchange: "", routingKey: rabbit.QueueName,
                    body: Encoding.UTF8.GetBytes(job.Id.ToString("N")));
                var duplicate = await channel.BasicGetAsync(rabbit.QueueName, false);
                Assert.NotNull(duplicate);
                await using (var processingScope = provider.CreateAsyncScope())
                    await processingScope.ServiceProvider.GetRequiredService<SalePdfProcessor>().ProcessAsync(Guid.Parse(Encoding.UTF8.GetString(duplicate.Body.Span)), default);
                await channel.BasicAckAsync(duplicate.DeliveryTag, false);
                requestDb.ChangeTracker.Clear();
                Assert.Equal(1, (await requestDb.Set<SalePdfJob>().SingleAsync()).Attempts);
                Assert.Equal(download.Content, (await queue.DownloadAsync(job.Id))!.Content);
            }
            finally
            {
                await publisher.StopAsync(default); await consumer.StopAsync(default);
                await channel.QueueDeleteAsync(rabbit.QueueName, false, false);
                await channel.QueueDeleteAsync(rabbit.QueueName + ".failed", false, false);
                publisher.Dispose(); consumer.Dispose();
            }
        });
    }

    private sealed record Seed(Guid SaleId, Guid BranchId, User User);
    private static SalePdfQueue Queue(AppDbContext db, User user, Guid branch) => new(db, user, new Context(branch), new Authorization(), new Clock());
    private static async Task WithDatabase(Func<DbContextOptions<AppDbContext>, Seed, Task> run)
    {
        var source = Environment.GetEnvironmentVariable("SISTEMI_TEST_CONNECTION");
        var name = "sistemi_pdf_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(source) { Database = "postgres", Pooling = false }.ConnectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", admin)) await create.ExecuteNonQueryAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(new NpgsqlConnectionStringBuilder(source) { Database = name, Pooling = false }.ConnectionString).Options;
        try
        {
            await using var db = new AppDbContext(options);
            await db.Database.MigrateAsync();
            var group = new BusinessGroup("PDF", "Prueba"); var company = new Company(group.Id, "PDF", "Prueba");
            var branch = new Branch(company.Id, "PDF", "Prueba");
            var actor = new AppUser { Id = Guid.NewGuid(), UserName = "pdf-test", NormalizedUserName = "PDF-TEST", IsActive = true };
            var cash = new CashSession("PDF", actor.UserName, DateTimeOffset.UtcNow, 0, actor.Id, branchId: branch.Id);
            var category = new Category("Prueba", null);
            var product = new Product("PDF", null, "Producto", null, category.Id, "Marca", "Unidad", 0, 0, 10, 0, false);
            var sale = new Sale("V-PDF", cash.Id, null, DateTimeOffset.UtcNow, actor.UserName,
                [new SaleItem(product.Id, product.Name, 1, 10, 0, 0, true)], SalePayment.CreateCash(10, 10, actor.UserName),
                "GTQ", "Quetzal", "Q", 2, ".", ",", "Before");
            db.AddRange(group, company, branch, actor, cash, category, product, sale);
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            await run(options, new(sale.Id, branch.Id, new User(actor.Id)));
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
    private sealed class User(Guid id) : ICurrentUserAccessor { public Guid? UserId => id; public string UserName => "pdf-test"; }
    private sealed class Clock : IDateTimeProvider { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }
    private sealed class Authorization : IOperationAuthorizationService
    {
        public Task<bool> HasPermissionAsync(string userName, string code, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<string> ValidateCredentialsAsync(string userName, string password, string? permission = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class Context(Guid branch) : IOperationContextAccessor
    {
        public Guid? CurrentBranchId => branch; public Guid? CurrentCompanyId => null; public Guid? CurrentBusinessGroupId => null; public Guid? CurrentWarehouseId => null;
        public string? CurrentBranchName => "Prueba"; public string? CurrentCompanyName => null;
        public Task<IReadOnlyList<BranchDto>> GetAuthorizedBranchesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<BranchDto>>([]);
        public Task SetCurrentBranchAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
