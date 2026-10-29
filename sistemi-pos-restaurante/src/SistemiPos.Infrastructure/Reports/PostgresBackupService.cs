using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using SistemiPos.Application.Reports;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Infrastructure.Reports;

public sealed class PostgresBackupService(IConfiguration config, IHostEnvironment environment) : IBackupService
{
    private readonly SemaphoreSlim gate = new(1,1);
    private string? Root => config["Backups:Directory"];
    private bool Configured => !string.IsNullOrWhiteSpace(Root) && Path.IsPathFullyQualified(Root)
        && !Path.GetFullPath(Root).Replace('\\','/').Split('/').Contains("wwwroot",StringComparer.OrdinalIgnoreCase)
        && !Path.GetFullPath(Root).TrimEnd(Path.DirectorySeparatorChar).Equals(
            Path.GetFullPath(environment.ContentRootPath).TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase)
        && File.Exists(config["Backups:PgDumpPath"]);
    private int Interval => Math.Clamp(config.GetValue("Backups:IntervalHours",24),1,168);
    private int Retention => Math.Clamp(config.GetValue("Backups:RetentionDays",7),2,365);
    public async Task<BackupStatus> StatusAsync(CancellationToken ct = default)
    {
        var files = new List<BackupInfo>();
        if (Configured && Directory.Exists(Root))
            foreach(var file in Directory.EnumerateFiles(Root!, "sistemi-*.manifest.json").OrderByDescending(x=>x).Take(50))
            {
                try
                {
                    var item = JsonSerializer.Deserialize<BackupInfo>(await File.ReadAllTextAsync(file,ct));
                    if(item is not null && Path.GetFileName(item.File)==item.File)
                        files.Add(item with { Verified=item.Verified && File.Exists(Path.Combine(Root!,item.File)) });
                }
                catch(JsonException) { }
            }
        return new(Configured,config.GetValue("Backups:Enabled",false),Interval,Retention,
            config.GetValue<int?>("Backups:RpoMinutes"),config.GetValue<int?>("Backups:RtoMinutes"),files);
    }
    public async Task<BackupInfo> CreateAsync(CancellationToken ct = default)
    {
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromMinutes(60)); ct=timeout.Token;
        if(!Configured) throw new DomainRuleException("Configura una carpeta privada absoluta y la ruta de pg_dump en el servidor.");
        if(!await gate.WaitAsync(0,ct)) throw new DomainRuleException("Ya hay un respaldo en ejecución.");
        string? temporary = null;
        try
        {
            Directory.CreateDirectory(Root!);
            var cs = new NpgsqlConnectionStringBuilder(config.GetConnectionString("DefaultConnection"));
            var name = "sistemi-"+DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")[..6]+".dump";
            var destination = Path.Combine(Root!,name); temporary = destination+".partial";
            await using var connection = new NpgsqlConnection(cs.ConnectionString); await connection.OpenAsync(ct);
            await using var tx = await connection.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead,ct);
            await using var command = new NpgsqlCommand("SELECT pg_export_snapshot()",connection,tx);
            var snapshot = (string)(await command.ExecuteScalarAsync(ct))!;
            var counts = new Dictionary<string,long>();
            foreach(var table in new[]{"Sales","Purchases","InventoryMovements","CashSessions"})
            {
                await using var count = new NpgsqlCommand("SELECT COUNT(*) FROM \""+table+"\"",connection,tx);
                counts[table] = (long)(await count.ExecuteScalarAsync(ct))!;
            }
            var info = new ProcessStartInfo(config["Backups:PgDumpPath"]!) { UseShellExecute=false,CreateNoWindow=true,
                RedirectStandardError=true,RedirectStandardOutput=true };
            foreach(var argument in new[]{"--format=custom","--no-owner","--no-acl","--snapshot="+snapshot,
                "--host="+cs.Host,"--port="+cs.Port,"--username="+cs.Username,"--dbname="+cs.Database,"--file="+temporary})
                info.ArgumentList.Add(argument);
            info.Environment["PGPASSWORD"]=cs.Password ?? "";
            info.Environment["PGCONNECT_TIMEOUT"]="15";
            if(cs.SslMode == SslMode.Require || cs.SslMode == SslMode.VerifyFull || cs.SslMode == SslMode.VerifyCA)
                info.Environment["PGSSLMODE"]=cs.SslMode==SslMode.Require ? "require" : cs.SslMode==SslMode.VerifyFull ? "verify-full" : "verify-ca";
            using var process = Process.Start(info) ?? throw new DomainRuleException("No pudo iniciarse pg_dump.");
            var stderr = process.StandardError.ReadToEndAsync(ct);
            var stdout = process.StandardOutput.ReadToEndAsync(ct);
            try { await process.WaitForExitAsync(ct); }
            catch { if(!process.HasExited) process.Kill(true); throw; }
            await stderr; await stdout;
            if(process.ExitCode!=0 || !File.Exists(temporary) || new FileInfo(temporary).Length==0)
                throw new DomainRuleException("El respaldo no terminó correctamente. Revisa la configuración y permisos del servidor.");
            await tx.CommitAsync(ct);
            await using var stream = File.OpenRead(temporary);
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream,ct)); await stream.DisposeAsync();
            File.Move(temporary,destination); temporary=null;
            var result = new BackupInfo(name,DateTimeOffset.UtcNow,new FileInfo(destination).Length,hash,true,Counts:counts);
            await File.WriteAllTextAsync(destination+".manifest.json",JsonSerializer.Serialize(result),ct);
            // Delete only our own expired, completed archives after a new verified archive exists.
            foreach(var manifest in Directory.EnumerateFiles(Root!,"sistemi-*.dump.manifest.json"))
            {
                var old = JsonSerializer.Deserialize<BackupInfo>(await File.ReadAllTextAsync(manifest,ct));
                if(old is null || !old.Verified || old.CreatedAtUtc>=DateTimeOffset.UtcNow.AddDays(-Retention))continue;
                if(Path.GetFileName(old.File)!=old.File || !old.File.StartsWith("sistemi-",StringComparison.Ordinal))continue;
                var archive = Path.Combine(Root!,old.File);
                if(File.Exists(archive)) File.Delete(archive);
                File.Delete(manifest);
            }
            return result;
        }
        finally { if(temporary is not null && File.Exists(temporary))File.Delete(temporary);gate.Release(); }
    }
}

public sealed class BackupWorker(IBackupService backups, IConfiguration config, ILogger<BackupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while(!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if(config.GetValue("Backups:Enabled",false))
                {
                    var status = await backups.StatusAsync(stoppingToken);
                    if(status.Configured && (status.Files.Count==0 || status.Files[0].CreatedAtUtc<DateTimeOffset.UtcNow.AddHours(-status.IntervalHours)))
                        await backups.CreateAsync(stoppingToken);
                }
            }
            catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested) { break; }
            catch(Exception ex) { logger.LogError("El respaldo automático falló ({Type}). Revisar el servicio de respaldo.",ex.GetType().Name); }
            try { await Task.Delay(TimeSpan.FromMinutes(5),stoppingToken); } catch(OperationCanceledException) { break; }
        }
    }
}
