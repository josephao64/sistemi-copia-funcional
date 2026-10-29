namespace SistemiPos.PerformanceSeeder;

internal sealed record SeederOptions(
    int Products,
    int Customers,
    int Suppliers,
    int Sales,
    int Purchases,
    int InventoryMovements,
    bool Reset)
{
    public const int Seed = 20260913;
    public const string ConnectionEnvironmentVariable = "SISTEMI_POS_RESTAURANTE_PERFORMANCE_DB_CONNECTION";
    public const string RequiredDatabase = "SistemiPosRestaurantePerformanceDb";

    public static SeederOptions Parse(string[] args)
    {
        var values = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["--products"] = 10_000,
            ["--customers"] = 2_000,
            ["--suppliers"] = 400,
            ["--sales"] = 5_000,
            ["--purchases"] = 2_000,
            ["--inventory-movements"] = 50_000
        };
        var reset = false;

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            if (string.Equals(argument, "--reset", StringComparison.OrdinalIgnoreCase))
            {
                reset = true;
                continue;
            }

            if (!values.ContainsKey(argument))
                throw new ArgumentException($"Parámetro no reconocido: {argument}.");
            if (++index >= args.Length || !int.TryParse(args[index], out var value) || value <= 0)
                throw new ArgumentException($"{argument} requiere un entero mayor a cero.");
            values[argument] = value;
        }

        return new SeederOptions(
            values["--products"],
            values["--customers"],
            values["--suppliers"],
            values["--sales"],
            values["--purchases"],
            values["--inventory-movements"],
            reset);
    }
}
