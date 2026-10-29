using SistemiPos.PerformanceSeeder;

try
{
    var options = SeederOptions.Parse(args);
    using var cancellationSource = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        cancellationSource.Cancel();
    };

    return await PerformanceDataSeeder.RunAsync(options, cancellationSource.Token);
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Generación cancelada. La transacción fue revertida.");
    return 2;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"ERROR: {exception.Message}");
    return 1;
}
