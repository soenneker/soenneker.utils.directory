using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Soenneker.Utils.Directory.Tests.Benchmarking.Benchmarks;

[MemoryDiagnoser]
public class DirectorySizeComparisonBenchmarks
{
    private readonly DirectoryUtil _util = new(null!, NullLogger<DirectoryUtil>.Instance);
    private string _root = null!;

    [GlobalSetup]
    public void Setup()
    {
        _root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"directory-size-comparison-{Guid.NewGuid():N}");
        var content = new byte[4 * 1024];

        for (var directoryIndex = 0; directoryIndex < 128; directoryIndex++)
        {
            var directory = System.IO.Path.Combine(_root, $"parent-{directoryIndex / 8}", $"child-{directoryIndex}");
            System.IO.Directory.CreateDirectory(directory);

            for (var fileIndex = 0; fileIndex < 8; fileIndex++)
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory, $"file-{fileIndex}.bin"), content);
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (System.IO.Directory.Exists(_root))
            System.IO.Directory.Delete(_root, recursive: true);
    }

    [Benchmark(Baseline = true)]
    public long EnumeratePathsAndFileInfo()
    {
        long total = 0;
        var pending = new Stack<string>();
        pending.Push(_root);

        while (pending.TryPop(out var directory))
        {
            foreach (var file in System.IO.Directory.EnumerateFiles(directory))
                total += new FileInfo(file).Length;

            foreach (var child in System.IO.Directory.EnumerateDirectories(directory))
                pending.Push(child);
        }

        return total;
    }

    [Benchmark]
    public async Task<long> EnumerateEntries() => await _util.GetSizeInBytes(_root);
}
