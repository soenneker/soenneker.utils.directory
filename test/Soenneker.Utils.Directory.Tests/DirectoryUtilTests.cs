using AwesomeAssertions;
using Soenneker.Utils.Directory.Abstract;
using Soenneker.Tests.HostedUnit;
using Soenneker.Utils.Directory.Dtos;
using System;
using System.Collections.Generic;
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
    public async ValueTask Delete_ShouldDeleteDirectoriesContainingReadOnlyFiles()
    {
        string root = CreateDirectoryContainingReadOnlyGitObject();

        try
        {
            await _util.Delete(root);

            System.IO.Directory.Exists(root).Should().BeFalse();
        }
        finally
        {
            DeleteTestDirectory(root);
        }
    }

    [Test]
    public async ValueTask DeleteIfExists_ShouldDeleteDirectoriesContainingReadOnlyFiles()
    {
        string root = CreateDirectoryContainingReadOnlyGitObject();

        try
        {
            await _util.DeleteIfExists(root);

            System.IO.Directory.Exists(root).Should().BeFalse();
        }
        finally
        {
            DeleteTestDirectory(root);
        }
    }

    [Test]
    public async ValueTask GetSizeInBytes_ShouldIncludeNestedFiles()
    {
        var root = CreateTempDirectory();

        try
        {
            var child = System.IO.Path.Combine(root, "child");
            System.IO.Directory.CreateDirectory(child);
            await System.IO.File.WriteAllBytesAsync(System.IO.Path.Combine(root, "root.bin"), new byte[11]);
            await System.IO.File.WriteAllBytesAsync(System.IO.Path.Combine(child, "child.bin"), new byte[17]);

            var recursive = await _util.GetSizeInBytes(root);
            var topLevel = await _util.GetSizeInBytes(root, new GetSizeOptions {Recursive = false});

            recursive.Should().Be(28);
            topLevel.Should().Be(11);
        }
        finally
        {
            System.IO.Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async ValueTask EmptyDirectoryOperations_ShouldDeleteEmptyChainsOnly()
    {
        var root = CreateTempDirectory();

        try
        {
            var emptyLeaf = System.IO.Path.Combine(root, "empty", "leaf");
            var nonempty = System.IO.Path.Combine(root, "nonempty");
            System.IO.Directory.CreateDirectory(emptyLeaf);
            System.IO.Directory.CreateDirectory(nonempty);
            await System.IO.File.WriteAllTextAsync(System.IO.Path.Combine(nonempty, "content.txt"), "content");

            var emptyDirectories = await _util.GetEmptyDirectories(root);
            emptyDirectories.Should().ContainSingle().Which.Should().Be(emptyLeaf);

            await _util.DeleteEmptyDirectories(root);

            System.IO.Directory.Exists(System.IO.Path.Combine(root, "empty")).Should().BeFalse();
            System.IO.Directory.Exists(nonempty).Should().BeTrue();
        }
        finally
        {
            System.IO.Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async ValueTask GetDirectoriesContainingFile_ShouldReturnMatchingDescendants()
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

            var result = await _util.GetDirectoriesContainingFile(root, "target.txt");

            result.Should().ContainSingle().Which.Should().Be(matching);
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
