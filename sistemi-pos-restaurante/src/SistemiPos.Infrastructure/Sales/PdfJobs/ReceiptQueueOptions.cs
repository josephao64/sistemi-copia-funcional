namespace SistemiPos.Infrastructure.Sales.PdfJobs;

public sealed class ReceiptQueueOptions
{
    public bool Enabled { get; set; }
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string VirtualHost { get; set; } = "/";
    public string UserName { get; set; } = "";
    public string Password { get; set; } = "";
    public string QueueName { get; set; } = "sistemi.receipts.pdf";
    public bool UseTls { get; set; }
    public int MaximumAttempts { get; set; } = 5;
    public int PollMilliseconds { get; set; } = 1000;
}
