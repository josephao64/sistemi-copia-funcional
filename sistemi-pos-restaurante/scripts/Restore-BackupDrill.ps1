param(
    [Parameter(Mandatory=$true)][string]$Archive,
    [Parameter(Mandatory=$true)][ValidatePattern('^sistemi_restore_[a-z0-9_]+$')][string]$TargetDatabase,
    [Parameter(Mandatory=$true)][string]$PgRestorePath,
    [Parameter(Mandatory=$true)][string]$PsqlPath,
    [Parameter(Mandatory=$true)][string]$ReportPath,
    [int]$RtoMinutes = 0
)
$ErrorActionPreference = 'Stop'
# PGHOST, PGPORT, PGUSER, PGPASSWORD/PGPASSFILE and PGSSLMODE come from the operator's environment.
# The target must already exist, be isolated, and contain no application tables. Nothing is deleted.
$resolvedArchive = (Resolve-Path -LiteralPath $Archive).Path
$manifest = Get-Content -LiteralPath ($resolvedArchive + '.manifest.json') -Raw | ConvertFrom-Json
if (-not $manifest.Verified -or -not $manifest.Counts) { throw 'El manifiesto no contiene verificación y conteos de referencia.' }
if ((Get-FileHash -LiteralPath $resolvedArchive -Algorithm SHA256).Hash -ne $manifest.Sha256) { throw 'La suma SHA256 no coincide. No se restaurará.' }
if ((Split-Path -Leaf $resolvedArchive) -ne $manifest.File) { throw 'Archivo y manifiesto no corresponden.' }
function Invoke-PgTool([string]$Tool, [string[]]$Arguments) {
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = (Resolve-Path -LiteralPath $Tool).Path
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $startInfo.ArgumentList.Add($argument) }
    $process = [System.Diagnostics.Process]::Start($startInfo)
    $outputTask = $process.StandardOutput.ReadToEndAsync()
    $errorTask = $process.StandardError.ReadToEndAsync()
    $process.WaitForExit()
    $output = $outputTask.GetAwaiter().GetResult()
    $null = $errorTask.GetAwaiter().GetResult()
    if ($process.ExitCode -ne 0) { throw 'La herramienta PostgreSQL falló. Revisa conectividad, permisos y compatibilidad de versiones en el entorno de simulacro.' }
    $process.Dispose()
    return $output.Trim()
}
$emptySql = "SELECT COUNT(*) FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE c.relkind IN ('r','p','v','m','f') AND n.nspname NOT IN ('pg_catalog','information_schema') AND n.nspname NOT LIKE 'pg_toast%';"
$existing = Invoke-PgTool $PsqlPath @('-X','-w','-A','-t','--set=ON_ERROR_STOP=1',"--dbname=$TargetDatabase",'--command',$emptySql)
if ($existing -ne '0') { throw 'La base de simulacro no está vacía. Usa otra base aislada.' }
$timer = [System.Diagnostics.Stopwatch]::StartNew()
$null = Invoke-PgTool $PgRestorePath @('--no-password','--exit-on-error','--single-transaction','--no-owner','--no-acl',"--dbname=$TargetDatabase",$resolvedArchive)
$results = @()
foreach ($table in @('Sales','Purchases','InventoryMovements','CashSessions')) {
    $sql = 'SELECT COUNT(*) FROM "' + $table + '";'
    $actual = [long](Invoke-PgTool $PsqlPath @('-X','-w','-A','-t','--set=ON_ERROR_STOP=1',"--dbname=$TargetDatabase",'--command',$sql))
    $expected = [long]$manifest.Counts.$table
    $results += [pscustomobject]@{Table=$table;Expected=$expected;Actual=$actual;Matches=($actual -eq $expected)}
}
$timer.Stop()
$passed = @($results | Where-Object { -not $_.Matches }).Count -eq 0
$report = [pscustomobject]@{Archive=$manifest.File;TargetDatabase=$TargetDatabase;CompletedAtUtc=[DateTimeOffset]::UtcNow;ElapsedSeconds=$timer.Elapsed.TotalSeconds;RtoMinutes=$RtoMinutes;RtoMet=($RtoMinutes -gt 0 -and $timer.Elapsed.TotalMinutes -le $RtoMinutes);Counts=$results;Passed=$passed}
$report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $ReportPath -Encoding utf8
if (-not $passed) { throw 'La restauración terminó, pero los conteos no coinciden. Consulta el reporte.' }
Write-Output "Restauración y conteos correctos. Reporte: $ReportPath"
