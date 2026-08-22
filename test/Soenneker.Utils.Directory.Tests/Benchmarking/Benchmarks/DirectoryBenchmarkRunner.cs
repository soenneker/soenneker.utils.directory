using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using Soenneker.Benchmarking.Extensions.Summary;
using Soenneker.Tests.Benchmark;
using System.Threading.Tasks;

namespace Soenneker.Utils.Directory.Tests.Benchmarking.Benchmarks;

public class DirectoryBenchmarkRunner : BenchmarkTest
{
    [Skip("Manual")]
    public async ValueTask DirectoryUtil()
    {
        var summary = BenchmarkRunner.Run<DirectoryUtilBenchmarks>(DefaultConf);
        await summary.OutputSummaryToLog();
    }

    [Skip("Manual")]
    public async ValueTask SizeComparison()
    {
        var summary = BenchmarkRunner.Run<DirectorySizeComparisonBenchmarks>(DefaultConf);
        await summary.OutputSummaryToLog();
    }
}
