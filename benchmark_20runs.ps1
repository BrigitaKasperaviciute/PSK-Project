# Run Integration Tests 20 Times:
#   ./benchmark_20runs.ps1

$times = @()
Write-Host "Running integration tests 20 times..."
Write-Host "=========================================="
for ($i = 1; $i -le 20; $i++) {
    $start = Get-Date
    dotnet test WorthBoards.Api.IntegrationTests/WorthBoards.Api.IntegrationTests.csproj -q 2>&1 | Out-Null
    $end = Get-Date
    $duration = ($end - $start).TotalSeconds
    $times += $duration
    $durationMs = [math]::Round($duration * 1000, 2)
    $progress = "$i".PadLeft(2)
    Write-Host "Run $progress : ${duration}s (${durationMs}ms)"
}

$avg = ($times | Measure-Object -Average).Average
$avgMs = [math]::Round($avg * 1000, 2)

Write-Host "`n========== RESULTS =========="
Write-Host "Total runs: 20"
Write-Host "Average execution time: ${avg}s (${avgMs}ms)"
Write-Host "========================================"
