# PowerShell script to run tests multiple times and calculate average execution time

param(
    [int]$Iterations = 20,
    [switch]$SkipBuild = $false
)

Write-Host "Running tests $Iterations times..." -ForegroundColor Cyan
Write-Host ""

# Build once before running tests
if (-not $SkipBuild) {
    Write-Host "Building project..." -ForegroundColor Cyan
    dotnet build --nologo --verbosity quiet 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed!" -ForegroundColor Red
        exit 1
    }
    Write-Host "Build successful!" -ForegroundColor Green
    Write-Host ""
}

$totalDuration = 0.0
$successfulRuns = 0
$failedRuns = 0
$durations = @()

for ($i = 1; $i -le $Iterations; $i++) {
    Write-Host "Run $i of ${Iterations}..." -ForegroundColor Yellow

    # Run dotnet test and capture output with precise timing
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $testArgs = if ($SkipBuild) { "--nologo --verbosity quiet" } else { "--nologo --verbosity quiet --no-build" }
    $output = Invoke-Expression "dotnet test $testArgs 2>&1" | Out-String
    $stopwatch.Stop()

    # Use actual elapsed time in milliseconds for accuracy
    $durationMs = $stopwatch.ElapsedMilliseconds
    $duration = $durationMs / 1000.0

    # Check if tests passed
    if ($output -match "Passed!|Test Run Successful") {
        $durations += $duration
        $totalDuration += $duration
        $successfulRuns++

        # Format duration with milliseconds
        if ($duration -lt 1) {
            Write-Host "  Duration: $($durationMs) ms" -ForegroundColor Green
        } else {
            Write-Host "  Duration: $([math]::Round($duration, 3)) s ($($durationMs) ms)" -ForegroundColor Green
        }
    } else {
        $failedRuns++
        Write-Host "  Duration: $([math]::Round($duration, 3)) s - FAILED" -ForegroundColor Red
    }

    # Small delay between runs
    Start-Sleep -Milliseconds 100
}

Write-Host ""
Write-Host "================================" -ForegroundColor Cyan
Write-Host "Test Execution Summary" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host "Total runs: $Iterations"
Write-Host "Successful runs: $successfulRuns" -ForegroundColor Green
Write-Host "Failed runs: $failedRuns" -ForegroundColor $(if ($failedRuns -eq 0) { "Green" } else { "Red" })
Write-Host ""

if ($successfulRuns -gt 0) {
    $averageDuration = $totalDuration / $successfulRuns
    $minDuration = ($durations | Measure-Object -Minimum).Minimum
    $maxDuration = ($durations | Measure-Object -Maximum).Maximum

    # Calculate standard deviation
    $variance = 0.0
    foreach ($d in $durations) {
        $variance += [math]::Pow($d - $averageDuration, 2)
    }
    $stdDev = [math]::Sqrt($variance / $successfulRuns)

    Write-Host "Execution Time Statistics:" -ForegroundColor Cyan
    Write-Host "  Average: $([math]::Round($averageDuration, 3)) s ($([math]::Round($averageDuration * 1000, 0)) ms)" -ForegroundColor Yellow
    Write-Host "  Minimum: $([math]::Round($minDuration, 3)) s ($([math]::Round($minDuration * 1000, 0)) ms)" -ForegroundColor Green
    Write-Host "  Maximum: $([math]::Round($maxDuration, 3)) s ($([math]::Round($maxDuration * 1000, 0)) ms)" -ForegroundColor Red
    Write-Host "  Std Dev: $([math]::Round($stdDev, 3)) s ($([math]::Round($stdDev * 1000, 0)) ms)" -ForegroundColor Cyan
    Write-Host "  Total:   $([math]::Round($totalDuration, 3)) s ($([math]::Round($totalDuration * 1000, 0)) ms)"
}

Write-Host ""
