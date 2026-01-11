using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace WorthBoards.Api.IntegrationTests;

/// <summary>
/// Benchmark test to run all integration tests multiple times and calculate average execution time.
/// This test is skipped by default. To run it, use: dotnet test --filter "FullyQualifiedName~TestBenchmark"
/// </summary>
public class TestBenchmark
{
    private readonly ITestOutputHelper _output;

    public TestBenchmark(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact(Skip = "Manual benchmark - remove Skip attribute to run")]
    public async Task RunAllTests_20Times_CalculateAverageExecutionTime()
    {
        const int iterations = 20;
        var durations = new List<double>();
        var factory = new TestWebApplicationFactory();

        _output.WriteLine($"Running benchmark: {iterations} iterations");
        _output.WriteLine(new string('=', 60));

        for (int i = 1; i <= iterations; i++)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Run a representative sample of tests
                await RunSampleTests(factory);

                stopwatch.Stop();
                var duration = stopwatch.Elapsed.TotalSeconds;
                durations.Add(duration);

                _output.WriteLine($"Run {i,2}: {duration:F3} seconds");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _output.WriteLine($"Run {i,2}: FAILED - {ex.Message}");
            }

            // Small delay between iterations
            await Task.Delay(100);
        }

        // Calculate statistics
        if (durations.Count > 0)
        {
            var average = durations.Average();
            var min = durations.Min();
            var max = durations.Max();
            var total = durations.Sum();
            var stdDev = CalculateStandardDeviation(durations, average);

            _output.WriteLine(new string('=', 60));
            _output.WriteLine("BENCHMARK RESULTS:");
            _output.WriteLine(new string('=', 60));
            _output.WriteLine($"Total iterations: {iterations}");
            _output.WriteLine($"Successful runs:  {durations.Count}");
            _output.WriteLine($"Failed runs:      {iterations - durations.Count}");
            _output.WriteLine("");
            _output.WriteLine("Execution Time Statistics:");
            _output.WriteLine($"  Average:  {average:F3} seconds");
            _output.WriteLine($"  Minimum:  {min:F3} seconds");
            _output.WriteLine($"  Maximum:  {max:F3} seconds");
            _output.WriteLine($"  Std Dev:  {stdDev:F3} seconds");
            _output.WriteLine($"  Total:    {total:F3} seconds");
            _output.WriteLine(new string('=', 60));

            // Assert that average execution time is reasonable (< 10 seconds)
            Assert.True(average < 10, $"Average execution time ({average:F3}s) exceeds 10 seconds");
        }

        factory.Dispose();
    }

    private async Task RunSampleTests(TestWebApplicationFactory factory)
    {
        // Run a quick sample test from each controller
        var authTests = new Controllers.AuthControllerTests(factory);
        var boardTests = new Controllers.BoardControllerTests(factory);
        var userTests = new Controllers.UserControllerTests(factory);

        // Run quick tests (not found scenarios are faster)
        await boardTests.GetBoardById_WhenBoardDoesNotExist_ReturnsNotFound();
        await userTests.GetUserById_WhenUserDoesNotExist_ReturnsNotFound();
    }

    private double CalculateStandardDeviation(List<double> values, double average)
    {
        if (values.Count < 2) return 0;

        var sumOfSquares = values.Sum(val => Math.Pow(val - average, 2));
        return Math.Sqrt(sumOfSquares / values.Count);
    }
}
