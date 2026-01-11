# Run performance tests from root directory
Write-Host "Running Performance Tests (20 iterations each)..." -ForegroundColor Cyan
Write-Host "This may take approximately 10-15 seconds..." -ForegroundColor Yellow
Write-Host ""

# Run the tests
dotnet test WorthBoards.Api.Tests --filter "FullyQualifiedName~Performance" --logger "console;verbosity=detailed"

Write-Host ""
Write-Host "Performance tests complete!" -ForegroundColor Green
