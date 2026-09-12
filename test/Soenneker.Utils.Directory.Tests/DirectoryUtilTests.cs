using AwesomeAssertions;
using Soenneker.Utils.Directory.Abstract;
using Soenneker.Tests.HostedUnit;
using Soenneker.Utils.Directory.Dtos;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;


namespace Soenneker.Utils.Directory.Tests;

[ClassDataSource<Host>(Shared = SharedType.PerTestSession)]
public class DirectoryUtilTests : HostedUnitTest
{
    private readonly IDirectoryUtil _util;

    public DirectoryUtilTests(Host host) : base(host)
    {
        _util = Resolve<IDirectoryUtil>(true);
    }

    [Test]
    public void Default()
    {

    }

    [Test]
    public async ValueTask Delete_ShouldDeleteDirectoriesContainingReadOnlyFiles(CancellationToken cancellationToken)
    {
        string root = CreateDirectoryContainingReadOnlyGitObject();

        try
        {
            await _util.Delete(root, cancellationToken: cancellationToken);

            System.IO.Directory.Exists(root).Should().BeFalse();
        }
        finally
        {
            DeleteTestDirectory(root);
        }
    }

    [Test]
    public async ValueTask DeleteIfExists_ShouldDeleteDirectoriesContainingReadOnlyFiles(CancellationToken cancellationToken)
    {
        string root = CreateDirectoryContainingReadOnlyGitObject();

        try
        {
            await _util.DeleteIfExists(root, cancellationToken: cancellationToken);

            System.IO.Directory.Exists(root).Should().BeFalse();
        }
        finally
        {
            DeleteTestDirectory(root);
        }
    }

    [Test]
    public async ValueTask GetSizeInBytes_ShouldIncludeNestedFiles(CancellationToken cancellationToken)
    {
        var root = CreateTempDirectory();

        try
        {
            var child = System.IO.Path.Combine(root, "child");
            System.IO.Directory.CreateDirectory(child);
            await System.IO.File.WriteAllBytesAsync(System.IO.Path.Combine(root, "root.bin"), new byte[11]);
            await System.IO.File.WriteAllBytesAsync(System.IO.Path.Combine(child, "child.bin"), new byte[17]);

            var recursive = await _util.GetSizeInBytes(root, cancellationToken: cancellationToken);
            var topLevel = await _util.GetSizeInBytes(root, new GetSizeOptions {Recursive = false}, cancellationToken: cancellationToken);

            recursive.Should().Be(28);
            topLevel.Should().Be(11);
        }
        finally
        {
            System.IO.Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async ValueTask EmptyDirectoryOperations_ShouldDeleteEmptyChainsOnly(CancellationToken cancellationToken)
    {
        var root = CreateTempDirectory();

        try
        {
            var emptyLeaf = System.IO.Path.Combine(root, "empty", "leaf");
            var nonempty = System.IO.Path.Combine(root, "nonempty");
            System.IO.Directory.CreateDirectory(emptyLeaf);
            System.IO.Directory.CreateDirectory(nonempty);
            await System.IO.File.WriteAllTextAsync(System.IO.Path.Combine(nonempty, "content.txt"), "content");

            var emptyDirectories = await _util.GetEmptyDirectories(root, cancellationToken: cancellationToken);
            emptyDirectories.Should().ContainSingle().Which.Should().Be(emptyLeaf);

            await _util.DeleteEmptyDirectories(root, cancellationToken: cancellationToken);

            System.IO.Directory.Exists(System.IO.Path.Combine(root, "empty")).Should().BeFalse();
            System.IO.Directory.Exists(nonempty).Should().BeTrue();
        }
        finally
        {
            System.IO.Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async ValueTask GetDirectoriesContainingFile_ShouldReturnMatchingDescendants(CancellationToken cancellationToken)
    {
        var root = CreateTempDirectory();

        try
        {
            var matching = System.IO.Path.Combine(root, "matching");
            var other = System.IO.Path.Combine(root, "other");
            System.IO.Directory.CreateDirectory(matching);
            System.IO.Directory.CreateDirectory(other);
            await System.IO.File.WriteAllTextAsync(System.IO.Path.Combine(root, "target.txt"), "excluded root match");
            await System.IO.File.WriteAllTextAsync(System.IO.Path.Combine(matching, "target.txt"), "match");

            var result = await _util.GetDirectoriesContainingFile(root, "target.txt", cancellationToken: cancellationToken);

            result.Should().ContainSingle().Which.Should().Be(matching);
        }
        finally
        {
            System.IO.Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async ValueTask CopyDirectory_ShouldCopyNestedFilesAndEmptyDirectories(CancellationToken cancellationToken)
    {
        string source = CreateTempDirectory();
        string destination = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"directory-util-tests-{Guid.NewGuid():N}");

        try
        {
            string nested = System.IO.Path.Combine(source, "nested");
            string empty = System.IO.Path.Combine(source, "empty");
            System.IO.Directory.CreateDirectory(nested);
            System.IO.Directory.CreateDirectory(empty);
            await System.IO.File.WriteAllTextAsync(System.IO.Path.Combine(nested, "content.txt"), "content", cancellationToken);

            await _util.CopyDirectory(source, destination, cancellationToken: cancellationToken);

            System.IO.Directory.Exists(System.IO.Path.Combine(destination, "empty")).Should().BeTrue();
            string content = await System.IO.File.ReadAllTextAsync(System.IO.Path.Combine(destination, "nested", "content.txt"), cancellationToken);
            content.Should().Be("content");
        }
        finally
        {
            System.IO.Directory.Delete(source, recursive: true);

            if (System.IO.Directory.Exists(destination))
                System.IO.Directory.Delete(destination, recursive: true);
        }
    }

    [Test]
    public async ValueTask GetFilesByExtension_ShouldMatchSequentialEnumeration(CancellationToken cancellationToken)
    {
        string root = CreateTempDirectory();
        try
        {
            foreach (string relative in new[] { "", "empty", "first", "first/deep", "first/deep/nested", ".hidden/child", "second/deep" })
            {
                string directory = System.IO.Path.Combine(root, relative);
                System.IO.Directory.CreateDirectory(directory);
                if (relative == "empty")
                    continue;
                await System.IO.File.WriteAllTextAsync(System.IO.Path.Combine(directory, "solution.slnx"), "", cancellationToken);
                await System.IO.File.WriteAllTextAsync(System.IO.Path.Combine(directory, "other.txt"), "", cancellationToken);
            }

            foreach (string searchRoot in new[] { root, System.IO.Path.GetRelativePath(Environment.CurrentDirectory, root) })
            foreach (string extension in new[] { "slnx", ".slnx", "" })
            foreach (bool recursive in new[] { false, true })
            {
                string pattern = extension.Length == 0 ? "*" : "*.slnx";
                string[] expected = System.IO.Directory.GetFiles(searchRoot, pattern,
                    recursive ? System.IO.SearchOption.AllDirectories : System.IO.SearchOption.TopDirectoryOnly);
                foreach (int degree in new[] { 1, 2, 8 })
                {
                    var actual = await _util.GetFilesByExtension(searchRoot, extension, recursive, degree, cancellationToken);
                    actual.Should().BeEquivalentTo(expected);
                }
                var defaults = await _util.GetFilesByExtension(searchRoot, extension, recursive, cancellationToken);
                defaults.Should().BeEquivalentTo(expected);
            }

            using var canceled = new CancellationTokenSource();
            canceled.Cancel();
            Func<Task> searchCanceled = async () => { await _util.GetFilesByExtension(root, "slnx", true, 8, canceled.Token); };
            await searchCanceled.Should().ThrowAsync<OperationCanceledException>();
            Func<Task> invalidDegree = async () => { await _util.GetFilesByExtension(root, "slnx", true, 0, cancellationToken); };
            await invalidDegree.Should().ThrowAsync<ArgumentOutOfRangeException>();
            Func<Task> missing = async () => { await _util.GetFilesByExtension(System.IO.Path.Combine(root, "missing"), "slnx", true, 8, cancellationToken); };
            await missing.Should().ThrowAsync<System.IO.DirectoryNotFoundException>();
        }
        finally
        {
            System.IO.Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async ValueTask CopyDirectory_ShouldCopyDifferentSizesAndRespectOverwrite(CancellationToken cancellationToken)
    {
        string root = CreateTempDirectory();
        string source = System.IO.Path.Combine(root, "source");
        System.IO.Directory.CreateDirectory(source);
        try
        {
            var expected = new Dictionary<string, byte[]>();
            for (int index = 0; index < 12; index++)
            {
                string relative = System.IO.Path.Combine($"child-{index % 3}", $"file-{index}.bin");
                string path = System.IO.Path.Combine(source, relative);
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
                var content = new byte[index * 32769];
                new System.Random(index).NextBytes(content);
                expected.Add(relative, content);
                await System.IO.File.WriteAllBytesAsync(path, content, cancellationToken);
            }
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(source, "empty", "nested"));

            foreach (int degree in new[] { 1, 4 })
            {
                string destination = System.IO.Path.Combine(root, $"destination-{degree}");
                System.IO.Directory.CreateDirectory(System.IO.Path.Combine(destination, "child-0"));
                string existing = System.IO.Path.Combine(destination, "child-0", "file-0.bin");
                await System.IO.File.WriteAllTextAsync(existing, "keep", cancellationToken);
                await _util.CopyDirectory(source, destination, false, degree, cancellationToken);
                (await System.IO.File.ReadAllTextAsync(existing, cancellationToken)).Should().Be("keep");
                foreach (var file in expected)
                {
                    if (file.Key == System.IO.Path.Combine("child-0", "file-0.bin"))
                        continue;
                    byte[] actual = await System.IO.File.ReadAllBytesAsync(System.IO.Path.Combine(destination, file.Key), cancellationToken);
                    actual.AsSpan().SequenceEqual(file.Value).Should().BeTrue();
                }
                await _util.CopyDirectory(source, destination, true, degree, cancellationToken);
                foreach (var file in expected)
                {
                    byte[] actual = await System.IO.File.ReadAllBytesAsync(System.IO.Path.Combine(destination, file.Key), cancellationToken);
                    actual.AsSpan().SequenceEqual(file.Value).Should().BeTrue();
                }
                System.IO.Directory.Exists(System.IO.Path.Combine(destination, "empty", "nested")).Should().BeTrue();
            }

            using var canceled = new CancellationTokenSource();
            canceled.Cancel();
            string untouched = System.IO.Path.Combine(root, "untouched");
            Func<Task> cancel = async () => { await _util.CopyDirectory(source, untouched, cancellationToken: canceled.Token); };
            await cancel.Should().ThrowAsync<OperationCanceledException>();
            System.IO.Directory.Exists(untouched).Should().BeFalse();
            Func<Task> invalid = async () => { await _util.CopyDirectory(source, untouched, true, 0, cancellationToken); };
            await invalid.Should().ThrowAsync<ArgumentOutOfRangeException>();

            string conflict = System.IO.Path.Combine(root, "conflict");
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(conflict, "child-0", "file-0.bin"));
            Func<Task> fail = async () => { await _util.CopyDirectory(source, conflict, cancellationToken: cancellationToken); };
            await fail.Should().ThrowAsync<UnauthorizedAccessException>();
        }
        finally
        {
            System.IO.Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"directory-util-tests-{Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(path);
        return path;
    }

    private static string CreateDirectoryContainingReadOnlyGitObject()
    {
        string root = CreateTempDirectory();
        string objectDirectory = System.IO.Path.Combine(root, ".git", "objects", "63");
        System.IO.Directory.CreateDirectory(objectDirectory);

        string objectPath = System.IO.Path.Combine(objectDirectory, "6866ce7d1a7d233781c80f2c9d3187c46274");
        System.IO.File.WriteAllText(objectPath, "git object");
        System.IO.File.SetAttributes(objectPath, System.IO.File.GetAttributes(objectPath) | System.IO.FileAttributes.ReadOnly);
        return root;
    }

    private static void DeleteTestDirectory(string root)
    {
        if (!System.IO.Directory.Exists(root))
            return;

        foreach (string path in System.IO.Directory.EnumerateFileSystemEntries(root, "*", System.IO.SearchOption.AllDirectories))
            System.IO.File.SetAttributes(path, System.IO.File.GetAttributes(path) & ~System.IO.FileAttributes.ReadOnly);

        System.IO.Directory.Delete(root, recursive: true);
    }
}
