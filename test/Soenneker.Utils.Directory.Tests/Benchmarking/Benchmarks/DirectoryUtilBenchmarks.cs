using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Threading.Tasks;

namespace Soenneker.Utils.Directory.Tests.Benchmarking.Benchmarks;

[MemoryDiagnoser]
public class DirectoryUtilBenchmarks
{
    private readonly DirectoryUtil _util = new(null!, NullLogger<DirectoryUtil>.Instance);
    private string _root = null!;

    [GlobalSetup]
    public void Setup()
    {
        _root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"directory-util-benchmarks-{Guid.NewGuid():N}");
        var content = new byte[4 * 1024];

        for (var parentIndex = 0; parentIndex < 16; parentIndex++)
        {
            var parent = System.IO.Path.Combine(_root, $"parent-{parentIndex}");

            for (var childIndex = 0; childIndex < 8; childIndex++)
            {
                var child = System.IO.Path.Combine(parent, $"child-{childIndex}");
                System.IO.Directory.CreateDirectory(child);

                if ((childIndex & 1) == 0)
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(child, "content.bin"), content);

                if (childIndex == 0)
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(child, "target.file"), content);
            }
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (System.IO.Directory.Exists(_root))
            System.IO.Directory.Delete(_root, recursive: true);
    }

    [Benchmark]
    public async Task<long> GetSizeInBytes() => await _util.GetSizeInBytes(_root);

    [Benchmark]
    public async Task<int> GetEmptyDirectories() => (await _util.GetEmptyDirectories(_root)).Count;

    [Benchmark]
    public async Task<int> GetDirectoriesContainingFile() => (await _util.GetDirectoriesContainingFile(_root, "target.file")).Count;

    [Benchmark]
    public async Task<int> GetDirectoriesOrderedByLevels() => (await DirectoryUtil.GetDirectoriesOrderedByLevels(_root)).Count;
}
