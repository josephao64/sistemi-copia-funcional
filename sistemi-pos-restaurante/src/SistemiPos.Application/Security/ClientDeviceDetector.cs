namespace SistemiPos.Application.Security;

public sealed record ClientDeviceDetails(
    string IpDisplay,
    string MachineName,
    string OperatingSystem,
    string Browser,
    string DeviceType);

public static class ClientDeviceDetector
{
    public static ClientDeviceDetails Detect(string? ipAddress, string? userAgent = null, string? machineName = null)
    {
        var isLocal = string.IsNullOrWhiteSpace(ipAddress) ||
                      ipAddress == "::1" ||
                      ipAddress == "127.0.0.1" ||
                      ipAddress.StartsWith("fe80:", StringComparison.OrdinalIgnoreCase);

        var cleanIp = isLocal
            ? "127.0.0.1 (Localhost)"
            : (ipAddress ?? "IP no detectada");

        var cleanMachine = !string.IsNullOrWhiteSpace(machineName)
            ? machineName
            : (isLocal ? Environment.MachineName : "Terminal Remota");

        var os = "Desconocido";
        var browser = "Navegador Web";
        var deviceType = "Computadora (PC)";

        if (!string.IsNullOrWhiteSpace(userAgent))
        {
            // Sistema Operativo
            if (userAgent.Contains("Windows NT 10.0", StringComparison.OrdinalIgnoreCase))
                os = "Windows 10/11 (64-bit)";
            else if (userAgent.Contains("Windows NT 6.3", StringComparison.OrdinalIgnoreCase))
                os = "Windows 8.1";
            else if (userAgent.Contains("Windows NT 6.1", StringComparison.OrdinalIgnoreCase))
                os = "Windows 7";
            else if (userAgent.Contains("Windows", StringComparison.OrdinalIgnoreCase))
                os = "Windows";
            else if (userAgent.Contains("Macintosh", StringComparison.OrdinalIgnoreCase) || userAgent.Contains("Mac OS X", StringComparison.OrdinalIgnoreCase))
                os = "macOS";
            else if (userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase))
                os = "Android OS";
            else if (userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase))
                os = "iOS (iPhone)";
            else if (userAgent.Contains("iPad", StringComparison.OrdinalIgnoreCase))
                os = "iPadOS (iPad)";
            else if (userAgent.Contains("Linux", StringComparison.OrdinalIgnoreCase))
                os = "Linux";

            // Navegador
            if (userAgent.Contains("Edg/", StringComparison.OrdinalIgnoreCase))
                browser = "Microsoft Edge";
            else if (userAgent.Contains("Chrome/", StringComparison.OrdinalIgnoreCase))
                browser = "Google Chrome";
            else if (userAgent.Contains("Firefox/", StringComparison.OrdinalIgnoreCase))
                browser = "Mozilla Firefox";
            else if (userAgent.Contains("Safari/", StringComparison.OrdinalIgnoreCase))
                browser = "Apple Safari";
            else if (userAgent.Contains("Opera/", StringComparison.OrdinalIgnoreCase) || userAgent.Contains("OPR/", StringComparison.OrdinalIgnoreCase))
                browser = "Opera";

            // Tipo de Dispositivo
            if (userAgent.Contains("Mobile", StringComparison.OrdinalIgnoreCase) || userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase) || userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase))
                deviceType = "Dispositivo Móvil";
            else if (userAgent.Contains("iPad", StringComparison.OrdinalIgnoreCase) || userAgent.Contains("Tablet", StringComparison.OrdinalIgnoreCase))
                deviceType = "Tablet";
            else
                deviceType = "Computadora (PC / Laptop)";
        }
        else if (isLocal)
        {
            os = "Windows (Terminal Local)";
            browser = "Cliente Web POS";
            deviceType = "Computadora Local (POS)";
        }

        return new ClientDeviceDetails(cleanIp, cleanMachine, os, browser, deviceType);
    }
}
