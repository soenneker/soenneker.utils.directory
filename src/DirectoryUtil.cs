using Microsoft.Extensions.Logging;
using Soenneker.Utils.Directory.Abstract;
using Soenneker.Utils.Path.Abstract;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Extensions.Task;
using Soenneker.Extensions.ValueTask;
using Soenneker.Utils.Directory.Utils;
using Soenneker.Utils.ExecutionContexts;

namespace Soenneker.Utils.Directory;

///<inheritdoc cref="IDirectoryUtil"/>
public sealed class DirectoryUtil : IDirectoryUtil
{
    private readonly IPathUtil _pathUtil;
    private readonly ILogger<DirectoryUtil> _logger;

    // Cache common indents to avoid new string(' ', n) per node in LogContentsRecursively
    private static readonly ConcurrentDictionary<int, string> _indentCache = new();

    // If you want to reduce logger overhead further, swap to [LoggerMessage] partial methods.
    public DirectoryUtil(IPathUtil pathUtil, ILogger<DirectoryUtil> logger)
    {
        _pathUtil = pathUtil;
        _logger = logger;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public List<string> GetAllDirectories(string directory)
    {
        // Avoid LINQ iterator overhead + ToList() overhead
        var list = new List<string>();
        foreach (var d in System.IO.Directory.EnumerateDirectories(directory))
            list.Add(d);

        return list;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IEnumerable<string> GetAllAsEnumerable(string directory) =>
        System.IO.Directory.EnumerateDirectories(directory);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public List<string> GetAllDirectoriesRecursively(string directory)
    {
        var list = new List<string>();
        foreach (var d in System.IO.Directory.EnumerateDirectories(directory, "*", SearchOption.AllDirectories))
            list.Add(d);

        return list;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IEnumerable<string> GetAllRecursivelyAsEnumerable(string directory) =>
        System.IO.Directory.EnumerateDirectories(directory, "*", SearchOption.AllDirectories);

    public void Delete(string directory)
    {
        _logger.LogDebug("Deleting directory ({dir}) ...", directory);
        System.IO.Directory.Delete(directory, recursive: true);
    }

    public void DeleteIfExists(string directory)
    {
        _logger.LogDebug("Deleting directory ({dir}) if it exists...", directory);

        // Exists check still required to avoid exception cost
        if (System.IO.Directory.Exists(directory))
            System.IO.Directory.Delete(directory, recursive: true);
    }

    public bool CreateIfDoesNotExist(string directory, bool log = true)
    {
        if (log)
            _logger.LogDebug("Creating directory ({dir}) if it doesn't exist...", directory);

        // Note: CreateDirectory is idempotent; but if you truly need "created vs existed", keep Exists().
        if (System.IO.Directory.Exists(directory))
            return false;

        System.IO.Directory.CreateDirectory(directory);
        return true;
    }

    public string GetWorkingDirectory(bool log = false)
    {
        // Assembly.Location can be empty in some contexts; keeping your behavior.
        var result = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly()
                                                             .Location)!;

        if (log)
            _logger.LogDebug("Retrieved working directory ({dir})", result);

        return result;
    }

    /// <summary>
    /// Retrieves a list of directories ordered by their levels.
    /// </summary>
    /// <remarks>
    /// Avoids Split() allocations by counting separators.
    /// </remarks>
    [Pure]
    public static List<string> GetDirectoriesOrderedByLevels(string basePath)
    {
        // Directory.GetDirectories returns array; if basePath is huge, you could stream + sort via list.
        var dirs = System.IO.Directory.GetDirectories(basePath, "*", SearchOption.AllDirectories);

        // Precompute depth once per string to avoid repeated scanning during sort comparisons.
        var items = new (string Path, int Depth)[dirs.Length];
        var sep = System.IO.Path.DirectorySeparatorChar;

        for (var i = 0; i < dirs.Length; i++)
        {
            var p = dirs[i];
            items[i] = (p, CountChar(p, sep));
        }

        Array.Sort(items, static (a, b) => a.Depth.CompareTo(b.Depth));

        var result = new List<string>(items.Length);
        for (var i = 0; i < items.Length; i++)
            result.Add(items[i].Path);

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CountChar(string s, char c)
    {
        var count = 0;
        for (var i = 0; i < s.Length; i++)
            if (s[i] == c)
                count++;
        return count;
    }

    /// <summary>
    /// Generates a new temporary directory path, but does not actually create the directory.
    /// </summary>
    [Pure]
    public static string GetNewTempDirectoryPath() =>
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid()
                                                                 .ToString("N"));

    public ValueTask<string> CreateTempDirectory(CancellationToken cancellationToken = default) =>
        _pathUtil.GetUniqueTempDirectory(null, true, cancellationToken);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Exists(string directory) => System.IO.Directory.Exists(directory);

    public List<string> GetEmptyDirectories(string root)
    {
        var result = new List<string>();

        foreach (var d in System.IO.Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
        {
            // Fast "Any()" without LINQ: just probe enumerator once.
            using var e = System.IO.Directory.EnumerateFileSystemEntries(d)
                                .GetEnumerator();
            if (!e.MoveNext())
                result.Add(d);
        }

        return result;
    }

    public void DeleteEmptyDirectories(string root)
    {
        // If you want to delete deepest-first to avoid missing newly-empty parents,
        // you can sort by depth descending. Keeping your current behavior.
        var empties = GetEmptyDirectories(root);

        for (var i = 0; i < empties.Count; i++)
        {
            var dir = empties[i];
            _logger.LogDebug("Deleting empty directory: {dir}", dir);
            System.IO.Directory.Delete(dir);
        }
    }

    public List<string> GetDirectoriesContainingFile(string root, string fileName)
    {
        var result = new List<string>();
        // Avoid extra work if fileName is empty
        if (string.IsNullOrEmpty(fileName))
            return result;

        foreach (var d in System.IO.Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
        {
            // Combine alloc is unavoidable; File.Exists is the real cost anyway.
            if (File.Exists(System.IO.Path.Combine(d, fileName)))
                result.Add(d);
        }

        return result;
    }

    public List<string> GetFilesByExtension(string directory, string extension, bool recursive = false)
    {
        // Avoid string interpolation + repeated TrimStart work
        var ext = extension;
        if (ext.Length > 0 && ext[0] == '.')
            ext = ext.Substring(1);

        var pattern = ext.Length == 0 ? "*" : "*." + ext;

        var result = new List<string>();

        foreach (var f in System.IO.Directory.EnumerateFiles(directory, pattern, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
        {
            result.Add(f);
        }

        return result;
    }

    public async ValueTask CopyDirectory(string sourceDir, string destDir, bool overwrite = true, CancellationToken cancellationToken = default)
    {
        if (!System.IO.Directory.Exists(sourceDir))
            throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");

        System.IO.Directory.CreateDirectory(destDir);

        // Stream enumeration (no arrays)
        foreach (var file in System.IO.Directory.EnumerateFiles(sourceDir))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var destFile = System.IO.Path.Combine(destDir, System.IO.Path.GetFileName(file));

            // If overwrite==false and file exists, skip/throw? Keeping overwrite semantics:
            if (!overwrite && File.Exists(destFile))
                continue;

            // File.Copy is typically faster than managed stream copy, but not cancellable mid-copy.
            // Since you accept CancellationToken, keep async streams (but tuned).
            var srcOpts = new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.Read,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
                BufferSize = 128 * 1024
            };

            var dstOpts = new FileStreamOptions
            {
                Mode = overwrite ? FileMode.Create : FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
                BufferSize = 128 * 1024
            };

            await using var sourceStream = new FileStream(file, srcOpts);
            await using var destinationStream = new FileStream(destFile, dstOpts);

            await sourceStream.CopyToAsync(destinationStream, 128 * 1024, cancellationToken)
                              .NoSync();
        }

        foreach (var subdir in System.IO.Directory.EnumerateDirectories(sourceDir))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var destSubdir = System.IO.Path.Combine(destDir, System.IO.Path.GetFileName(subdir));
            await CopyDirectory(subdir, destSubdir, overwrite, cancellationToken)
                .NoSync();
        }
    }

    public static string Normalize(string directory)
    {
        // Avoid Uri allocation; GetFullPath already normalizes.
        // Trim all trailing separators (both kinds) except when it's a root (e.g. "C:\")
        var full = System.IO.Path.GetFullPath(directory);

        var len = full.Length;
        while (len > 0)
        {
            var c = full[len - 1];
            if (c != System.IO.Path.DirectorySeparatorChar && c != System.IO.Path.AltDirectorySeparatorChar)
                break;

            // Don't trim root separator (e.g. "C:\")
            if (len == 3 && full[1] == ':' && (full[2] == '\\' || full[2] == '/'))
                break;

            len--;
        }

        return len == full.Length ? full : full.Substring(0, len);
    }

    public ValueTask LogContentsRecursively(string path, int indentLevel = 0, CancellationToken cancellationToken = default)
    {
        var args = new LogArgs(path, indentLevel, cancellationToken, this);

        return ExecutionContextUtil.RunInlineOrOffload(static s => ((LogArgs)s!).Self.LogContentsRecursivelySync((LogArgs)s!), args, cancellationToken);
    }

    private void LogContentsRecursivelySync(LogArgs args)
    {
        if (!System.IO.Directory.Exists(args.Path))
        {
            _logger.LogWarning("Directory does not exist: {Path}", args.Path);
            return;
        }

        try
        {
            var indent = GetIndent(args.IndentLevel);

            _logger.LogInformation("{Indent}📁 {Directory}", indent, System.IO.Path.GetFileName(args.Path));

            foreach (var file in System.IO.Directory.EnumerateFiles(args.Path))
            {
                args.Token.ThrowIfCancellationRequested();
                _logger.LogInformation("{Indent}  📄 {File}", indent, System.IO.Path.GetFileName(file));
            }

            foreach (var subdir in System.IO.Directory.EnumerateDirectories(args.Path))
            {
                args.Token.ThrowIfCancellationRequested();
                LogContentsRecursivelySync(new LogArgs(subdir, args.IndentLevel + 1, args.Token, this));
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied to {Path}", args.Path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading directory {Path}", args.Path);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string GetIndent(int indentLevel)
    {
        if ((uint)indentLevel == 0)
            return string.Empty;

        // 2 spaces per level
        var spaces = indentLevel * 2;
        return _indentCache.GetOrAdd(spaces, static s => new string(' ', s));
    }

    public ValueTask<long> GetSizeInBytes(string directory, GetSizeOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (!System.IO.Directory.Exists(directory))
            return ValueTask.FromResult(0L);

        var args = new GetSizeArgs(directory, options, cancellationToken, _logger);

        // If on a UI SyncContext, offload; otherwise scan inline (fastest, lowest overhead).
        return ExecutionContextUtil.RunInlineOrOffload(static s => ScanSize((GetSizeArgs)s!), args, cancellationToken);
    }

    private static long ScanSize(GetSizeArgs args)
    {
        var opts = args.Options ?? new GetSizeOptions();

        long totalSize = 0;

        // Avoid DirectoryInfo allocations: use string paths + Enumerate*
        var stack = new Stack<string>(capacity: 32);
        stack.Push(args.Directory);

        while (stack.Count > 0)
        {
            args.CancellationToken.ThrowIfCancellationRequested();

            var currentDir = stack.Pop();

            try
            {
                foreach (var filePath in System.IO.Directory.EnumerateFiles(currentDir))
                {
                    args.CancellationToken.ThrowIfCancellationRequested();

                    // FileInfo alloc; use FileStream? That's worse. FileInfo is ok here; it’s the IO that dominates.
                    totalSize += new FileInfo(filePath).Length;
                }

                opts.Progress?.Report(totalSize);

                if (opts.Recursive)
                {
                    foreach (var subDir in System.IO.Directory.EnumerateDirectories(currentDir))
                        stack.Push(subDir);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                args.Logger.LogWarning(ex, "Access denied to directory {DirectoryPath}, skipping.", currentDir);
                if (!opts.ContinueOnError)
                    throw;
            }
            catch (Exception ex)
            {
                args.Logger.LogError(ex, "An error occurred while scanning directory {DirectoryPath}, skipping.", currentDir);
                if (!opts.ContinueOnError)
                    throw;
            }
        }

        return totalSize;
    }

    public ValueTask MoveContentsUpOneLevelStrict(string tempDir, CancellationToken cancellationToken = default)
    {
        var args = new MoveArgs(tempDir, cancellationToken, this);

        return ExecutionContextUtil.RunInlineOrOffload(static s => ((MoveArgs)s!).Self.MoveContentsUpOneLevelStrictSync((MoveArgs)s!), args, cancellationToken);
    }

    private void MoveContentsUpOneLevelStrictSync(MoveArgs args)
    {
        args.Token.ThrowIfCancellationRequested();

        var tempDir = args.TempDir;

        if (!System.IO.Directory.Exists(tempDir))
            throw new DirectoryNotFoundException($"The directory '{tempDir}' does not exist.");

        using (var files = System.IO.Directory.EnumerateFiles(tempDir)
                                 .GetEnumerator())
        {
            if (files.MoveNext())
                throw new InvalidOperationException("Top-level directory contains files. Expected only one subdirectory.");
        }

        string innerDir;
        using (var dirs = System.IO.Directory.EnumerateDirectories(tempDir)
                                .GetEnumerator())
        {
            if (!dirs.MoveNext())
                throw new InvalidOperationException("Expected exactly one subdirectory in temp dir, found 0.");

            innerDir = dirs.Current;

            if (dirs.MoveNext())
                throw new InvalidOperationException("Expected exactly one subdirectory in temp dir, found more than 1.");
        }

        _logger.LogInformation("Moving contents from inner directory '{inner}' up to '{temp}'", innerDir, tempDir);

        foreach (var dir in System.IO.Directory.EnumerateDirectories(innerDir))
        {
            args.Token.ThrowIfCancellationRequested();

            var destDir = System.IO.Path.Combine(tempDir, System.IO.Path.GetFileName(dir));

            if (System.IO.Directory.Exists(destDir))
                throw new IOException($"Destination directory already exists: {destDir}");

            System.IO.Directory.Move(dir, destDir);
            _logger.LogDebug("Moved directory: {src} -> {dest}", dir, destDir);
        }

        foreach (var file in System.IO.Directory.EnumerateFiles(innerDir))
        {
            args.Token.ThrowIfCancellationRequested();

            var destFile = System.IO.Path.Combine(tempDir, System.IO.Path.GetFileName(file));

            if (File.Exists(destFile))
                throw new IOException($"Destination file already exists: {destFile}");

            File.Move(file, destFile);
            _logger.LogDebug("Moved file: {src} -> {dest}", file, destFile);
        }

        System.IO.Directory.Delete(innerDir, recursive: true);
        _logger.LogInformation("Inner directory '{inner}' deleted after move", innerDir);
    }
}