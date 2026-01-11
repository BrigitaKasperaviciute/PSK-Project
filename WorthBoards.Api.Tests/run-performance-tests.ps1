# Run performance tests and display results
Write-Host "Running Performance Tests (20 iterations each)..." -ForegroundColor Cyan
Write-Host "This may take a few minutes..." -ForegroundColor Yellow
Write-Host ""

# Run the performance tests
dotnet test --filter "FullyQualifiedName~Performance" --logger "console;verbosity=detailed" | Out-File -FilePath "performance-results.txt"

# Extract and display the results
Write-Host "=======================================" -ForegroundColor Green
Write-Host "  PERFORMANCE TEST RESULTS SUMMARY" -ForegroundColor Green
Write-Host "=======================================" -ForegroundColor Green
Write-Host ""

Get-Content "performance-results.txt" | Select-String -Pattern "Performance Test Results|Average:|Median:|Min:|Max:|Std Dev:" | ForEach-Object {
    $line = $_.Line.Trim()
    if ($line -match "Performance Test Results") {
        Write-Host ""
        Write-Host $line -ForegroundColor Cyan
        Write-Host "================================================" -ForegroundColor Cyan
    }
    elseif ($line -match "Average:") {
        Write-Host $line -ForegroundColor Yellow
    }
    elseif ($line -match "Median:") {
        Write-Host $line -ForegroundColor White
    }
    elseif ($line -match "Min:") {
        Write-Host $line -ForegroundColor Green
    }
    elseif ($line -match "Max:") {
        Write-Host $line -ForegroundColor Red
    }
    elseif ($line -match "Std Dev:") {
        Write-Host $line -ForegroundColor Magenta
    }
}

Write-Host ""
Write-Host "=======================================" -ForegroundColor Green

# Show test summary
$summary = Get-Content "performance-results.txt" | Select-String -Pattern "Passed!|Failed!" | Select-Object -Last 1
Write-Host ""
Write-Host "Test Summary:" -ForegroundColor Cyan
Write-Host $summary.Line.Trim() -ForegroundColor White
Write-Host ""

# Clean up
Remove-Item "performance-results.txt" -ErrorAction SilentlyContinue
