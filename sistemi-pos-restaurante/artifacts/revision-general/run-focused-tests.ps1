$ErrorActionPreference = 'Stop'
$env:VSTEST_CONNECTION_TIMEOUT = '15'
$auditRoot = (Get-Location).Path
$auditDir = Join-Path $auditRoot 'artifacts/revision-general'
$auditRuns = @(
    @{ Name = 'unit'; Project = 'tests/SistemiPos.UnitTests/SistemiPos.UnitTests.csproj'; Filter = 'FullyQualifiedName~Security|FullyQualifiedName~Cash|FullyQualifiedName~Orders|FullyQualifiedName~Sales|FullyQualifiedName~Reports|FullyQualifiedName~EnterpriseSettingsResolverTests' },
    @{ Name = 'components'; Project = 'tests/SistemiPos.IntegrationTests/SistemiPos.IntegrationTests.csproj'; Filter = 'FullyQualifiedName~PhaseEightControlTests|FullyQualifiedName~PhaseEightModelTests|FullyQualifiedName~RestaurantOrderModelTests|FullyQualifiedName~RecipeModelTests' }
)
$auditResults = @()
foreach ($auditRun in $auditRuns) {
    $auditStdout = Join-Path $auditDir ($auditRun.Name + '.stdout.log')
    $auditStderr = Join-Path $auditDir ($auditRun.Name + '.stderr.log')
    $auditArgs = @('test', $auditRun.Project, '--no-build', '--no-restore', '-c', 'Release', '--filter', ('"' + $auditRun.Filter + '"'), '--logger', ('"trx;LogFileName=review-' + $auditRun.Name + '.trx"'), '--results-directory', 'artifacts/revision-general', '--diag', ('artifacts/revision-general/' + $auditRun.Name + '.diag.log'))
    $auditProcess = Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList $auditArgs -PassThru -WindowStyle Hidden -RedirectStandardOutput $auditStdout -RedirectStandardError $auditStderr
    $auditFinished = $auditProcess.WaitForExit(45000)
    if (-not $auditFinished) {
        $auditProcess.Kill($true)
        $auditProcess.WaitForExit()
    }
    $auditProcess.Refresh()
    $auditResults += @{ Name = $auditRun.Name; Finished = $auditFinished; ExitCode = $auditProcess.ExitCode; TimeoutSeconds = 45 }
    Get-Content -LiteralPath $auditStdout
    Get-Content -LiteralPath $auditStderr
    $auditResults[-1] | ConvertTo-Json -Compress
}
$auditResults | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $auditDir 'test-execution-status.json') -Encoding utf8
