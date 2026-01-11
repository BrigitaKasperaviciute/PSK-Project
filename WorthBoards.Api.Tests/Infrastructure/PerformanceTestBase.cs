using System.Diagnostics;

namespace WorthBoards.Api.Tests.Infrastructure;

public abstract class PerformanceTestBase : IntegrationTestBase
{
    private const int DefaultIterations = 20;

    protected async Task<PerformanceResult> RunPerformanceTestAsync(
        Func<Task> testAction,
        int iterations = DefaultIterations)
    {
        var executionTimes = new List<long>();

        for (int i = 0; i < iterations; i++)
        {
            // Clear database before each iteration
            await ClearDatabaseAsync();

            var stopwatch = Stopwatch.StartNew();
            await testAction();
            stopwatch.Stop();

            executionTimes.Add(stopwatch.ElapsedMilliseconds);
        }

        return new PerformanceResult
        {
            Iterations = iterations,
            ExecutionTimes = executionTimes,
            AverageTimeMs = executionTimes.Average(),
            MinTimeMs = executionTimes.Min(),
            MaxTimeMs = executionTimes.Max(),
            MedianTimeMs = CalculateMedian(executionTimes),
            StandardDeviation = CalculateStandardDeviation(executionTimes)
        };
    }

    private static double CalculateMedian(List<long> values)
    {
        var sorted = values.OrderBy(x => x).ToList();
        int mid = sorted.Count / 2;

        if (sorted.Count % 2 == 0)
        {
            return (sorted[mid - 1] + sorted[mid]) / 2.0;
        }

        return sorted[mid];
    }

    private static double CalculateStandardDeviation(List<long> values)
    {
        var avg = values.Average();
        var sumOfSquares = values.Sum(val => Math.Pow(val - avg, 2));
        return Math.Sqrt(sumOfSquares / values.Count);
    }
}

public class PerformanceResult
{
    public int Iterations { get; set; }
    public List<long> ExecutionTimes { get; set; } = new();
    public double AverageTimeMs { get; set; }
    public long MinTimeMs { get; set; }
    public long MaxTimeMs { get; set; }
    public double MedianTimeMs { get; set; }
    public double StandardDeviation { get; set; }

    public override string ToString()
    {
        return $"""
            Performance Test Results ({Iterations} iterations):
            ================================================
            Average:    {AverageTimeMs:F2} ms
            Median:     {MedianTimeMs:F2} ms
            Min:        {MinTimeMs} ms
            Max:        {MaxTimeMs} ms
            Std Dev:    {StandardDeviation:F2} ms
            ================================================
            """;
    }
}
