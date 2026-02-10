using System.Diagnostics;

namespace _1Dev.Pagin8.Test.IntegrationTests.Performance;

/// <summary>
/// Collects and reports performance metrics for integration tests
/// </summary>
public class PerformanceMetricsCollector
{
    private readonly List<QueryMetric> _metrics = new();
    private readonly Stopwatch _totalStopwatch = new();
    private readonly object _lock = new();

    public string DatabaseType { get; set; } = "Unknown";
    public int DatasetSize { get; set; }

    public void Start()
    {
        _totalStopwatch.Start();
    }

    public void RecordQuery(string testName, string query, long elapsedMs, int resultCount)
    {
        lock (_lock)
        {
            _metrics.Add(new QueryMetric
            {
                TestName = testName,
                Query = query,
                ElapsedMs = elapsedMs,
                ResultCount = resultCount,
                Timestamp = DateTime.UtcNow
            });
        }
    }

    public PerformanceReport GenerateReport()
    {
        _totalStopwatch.Stop();
        
        lock (_lock)
        {
            return new PerformanceReport
            {
                DatabaseType = DatabaseType,
                DatasetSize = DatasetSize,
                TotalTests = _metrics.Count,
                TotalElapsedMs = _totalStopwatch.ElapsedMilliseconds,
                Metrics = _metrics.ToList(),
                AverageMs = _metrics.Any() ? _metrics.Average(m => m.ElapsedMs) : 0,
                MinMs = _metrics.Any() ? _metrics.Min(m => m.ElapsedMs) : 0,
                MaxMs = _metrics.Any() ? _metrics.Max(m => m.ElapsedMs) : 0,
                MedianMs = CalculateMedian(_metrics.Select(m => m.ElapsedMs).ToList())
            };
        }
    }

    private static double CalculateMedian(List<long> values)
    {
        if (!values.Any()) return 0;
        
        var sorted = values.OrderBy(v => v).ToList();
        var mid = sorted.Count / 2;
        
        return sorted.Count % 2 == 0
            ? (sorted[mid - 1] + sorted[mid]) / 2.0
            : sorted[mid];
    }

    public void PrintReport(PerformanceReport report, int excellentThreshold = 100, int goodThreshold = 500, int acceptableThreshold = 1000)
    {
        Console.WriteLine();
        Console.WriteLine("??????????????????????????????????????????????????????????????????????");
        Console.WriteLine($"?  ?? Performance Report - {report.DatabaseType,-40} ?");
        Console.WriteLine("??????????????????????????????????????????????????????????????????????");
        Console.WriteLine($"?  Dataset Size:     {report.DatasetSize,10:N0} records                         ?");
        Console.WriteLine($"?  Total Tests:      {report.TotalTests,10} tests                           ?");
        Console.WriteLine($"?  Total Time:       {report.TotalElapsedMs,10:N0} ms                              ?");
        Console.WriteLine("??????????????????????????????????????????????????????????????????????");
        Console.WriteLine($"?  Average:          {report.AverageMs,10:F2} ms  {GetRating(report.AverageMs, excellentThreshold, goodThreshold, acceptableThreshold),-30} ?");
        Console.WriteLine($"?  Median:           {report.MedianMs,10:F2} ms                              ?");
        Console.WriteLine($"?  Min:              {report.MinMs,10:N0} ms                              ?");
        Console.WriteLine($"?  Max:              {report.MaxMs,10:N0} ms                              ?");
        Console.WriteLine("??????????????????????????????????????????????????????????????????????");

        // Performance distribution
        var excellent = report.Metrics.Count(m => m.ElapsedMs < excellentThreshold);
        var good = report.Metrics.Count(m => m.ElapsedMs >= excellentThreshold && m.ElapsedMs < goodThreshold);
        var acceptable = report.Metrics.Count(m => m.ElapsedMs >= goodThreshold && m.ElapsedMs < acceptableThreshold);
        var slow = report.Metrics.Count(m => m.ElapsedMs >= acceptableThreshold);

        Console.WriteLine();
        Console.WriteLine("Performance Distribution:");
        Console.WriteLine($"  ? Excellent (< {excellentThreshold}ms):    {excellent,3} tests ({excellent * 100.0 / report.TotalTests:F1}%) {GetBar(excellent, report.TotalTests)}");
        Console.WriteLine($"  ? Good ({excellentThreshold}-{goodThreshold}ms):        {good,3} tests ({good * 100.0 / report.TotalTests:F1}%) {GetBar(good, report.TotalTests)}");
        Console.WriteLine($"  ??  Acceptable ({goodThreshold}-{acceptableThreshold}ms): {acceptable,3} tests ({acceptable * 100.0 / report.TotalTests:F1}%) {GetBar(acceptable, report.TotalTests)}");
        Console.WriteLine($"  ?? Slow (> {acceptableThreshold}ms):       {slow,3} tests ({slow * 100.0 / report.TotalTests:F1}%) {GetBar(slow, report.TotalTests)}");

        // Top 5 slowest queries
        Console.WriteLine();
        Console.WriteLine("Top 5 Slowest Queries:");
        var slowest = report.Metrics.OrderByDescending(m => m.ElapsedMs).Take(5).ToList();
        for (int i = 0; i < slowest.Count; i++)
        {
            var metric = slowest[i];
            var rating = GetRatingIcon(metric.ElapsedMs, excellentThreshold, goodThreshold, acceptableThreshold);
            Console.WriteLine($"  {i + 1}. {rating} {metric.TestName,-40} {metric.ElapsedMs,6:N0}ms ({metric.ResultCount,5:N0} results)");
        }

        // Top 5 fastest queries
        Console.WriteLine();
        Console.WriteLine("Top 5 Fastest Queries:");
        var fastest = report.Metrics.OrderBy(m => m.ElapsedMs).Take(5).ToList();
        for (int i = 0; i < fastest.Count; i++)
        {
            var metric = fastest[i];
            var rating = GetRatingIcon(metric.ElapsedMs, excellentThreshold, goodThreshold, acceptableThreshold);
            Console.WriteLine($"  {i + 1}. {rating} {metric.TestName,-40} {metric.ElapsedMs,6:N0}ms ({metric.ResultCount,5:N0} results)");
        }

        Console.WriteLine();
    }

    private static string GetRating(double ms, int excellent, int good, int acceptable)
    {
        if (ms < excellent) return "? Excellent";
        if (ms < good) return "? Good";
        if (ms < acceptable) return "??  Acceptable";
        return "?? Slow";
    }

    private static string GetRatingIcon(double ms, int excellent, int good, int acceptable)
    {
        if (ms < excellent) return "?";
        if (ms < good) return "?";
        if (ms < acceptable) return "?? ";
        return "??";
    }

    private static string GetBar(int count, int total)
    {
        if (total == 0) return "";
        
        var percentage = count * 100.0 / total;
        var barLength = (int)(percentage / 5); // 20 chars max (100% / 5)
        return new string('?', barLength);
    }
}

public class QueryMetric
{
    public string TestName { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public long ElapsedMs { get; set; }
    public int ResultCount { get; set; }
    public DateTime Timestamp { get; set; }
}

public class PerformanceReport
{
    public string DatabaseType { get; set; } = string.Empty;
    public int DatasetSize { get; set; }
    public int TotalTests { get; set; }
    public long TotalElapsedMs { get; set; }
    public double AverageMs { get; set; }
    public double MedianMs { get; set; }
    public long MinMs { get; set; }
    public long MaxMs { get; set; }
    public List<QueryMetric> Metrics { get; set; } = new();
}
