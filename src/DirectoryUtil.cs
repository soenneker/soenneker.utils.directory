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

    public DirectoryUtil(IPathUtil pathUtil, ILogger<DirectoryUtil> logger)
    {
        _pathUtil = pathUtil;
        _logger = logger;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<List<string>> GetAllDirectories(string directory, CancellationToken cancellationToken = default) =>
        ExecutionContextUtil.RunInlineOrOffload(static s =>
        {
            (string dir, CancellationToken token) = ((string Directory, CancellationToken Token))s!;
            var list = new List<string>();

            foreach (var d in System.IO.Directory.EnumerateDirectories(dir))
            {
                token.ThrowIfCancellationRequested();
                list.Add(d);
            }

            return list;
        }, (directory, cancellationToken), cancellationToken);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<List<string>> GetAllAsEnumerable(string directory, CancellationToken cancellationToken = default) =>
        GetAllDirectories(directory, cancellationToken);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<List<string>> GetAllDirectoriesRecursively(string directory, CancellationToken cancellationToken = default) =>
        ExecutionContextUtil.RunInlineOrOffload(static s =>
        {
            (string dir, CancellationToken token) = ((string Directory, CancellationToken Token))s!;
            var list = new List<string>();
            foreach (var d in System.IO.Directory.EnumerateDirectories(dir, "*", SearchOption.AllDirectories))
            {
                token.ThrowIfCancellationRequested();
                list.Add(d);
            }

            return list;
        }, (directory, cancellationToken), cancellationToken);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<List<string>> GetAllRecursivelyAsEnumerable(string directory, CancellationToken cancellationToken = default) =>
        GetAllDirectoriesRecursively(directory, cancellationToken);

    public ValueTask Delete(string directory, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Deleting directory ({dir}) ...", directory);
        return ExecutionContextUtil.RunInlineOrOffload(static s =>
        {
            System.IO.Directory.Delete((string)s!, recursive: true);
        }, directory, cancellationToken);
    }

    public ValueTask DeleteIfExists(string directory, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Deleting directory ({dir}) if it exists...", directory);

        return ExecutionContextUtil.RunInlineOrOffload(static s =>
        {
            var dir = (string)s!;
            // Exists check still required to avoid exception cost
            if (System.IO.Directory.Exists(dir))
                System.IO.Directory.Delete(dir, recursive: true);
        }, directory, cancellationToken);
    }

    public ValueTask<bool> CreateIfDoesNotExist(string directory, bool log = true, CancellationToken cancellationToken = default)
    {
        if (log)
            _logger.LogDebug("Creating directory ({dir}) if it doesn't exist...", directory);

        return ExecutionContextUtil.RunInlineOrOffload(static s =>
        {
            var dir = (string)s!;

            // Note: CreateDirectory is idempotent; but if you truly need "created vs existed", keep Exists().
            if (System.IO.Directory.Exists(dir))
                return false;

            System.IO.Directory.CreateDirectory(dir);
            return true;
        }, directory, cancellationToken);
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
    public static ValueTask<List<string>> GetDirectoriesOrderedByLevels(string basePath, CancellationToken cancellationToken = default) =>
        ExecutionContextUtil.RunInlineOrOffload(static s =>
        {
            (string BasePath, CancellationToken Token) = ((string BasePath, CancellationToken Token))s!;

            // Directory.GetDirectories returns array; if basePath is huge, you could stream + sort via list.
            var dirs = System.IO.Directory.GetDirectories(BasePath, "*", SearchOption.AllDirectories);

            // Precompute depth once per string to avoid repeated scanning during sort comparisons.
            var items = new (string Path, int Depth)[dirs.Length];
            var sep = System.IO.Path.DirectorySeparatorChar;

            for (var i = 0; i < dirs.Length; i++)
            {
                Token.ThrowIfCancellationRequested();
                var p = dirs[i];
                items[i] = (p, CountChar(p, sep));
            }

            Array.Sort(items, static (a, b) => a.Depth.CompareTo(b.Depth));

            var result = new List<string>(items.Length);
            for (var i = 0; i < items.Length; i++)
                result.Add(items[i].Path);

            return result;
        }, (basePath, cancellationToken), cancellationToken);

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
    public ValueTask<bool> Exists(string directory, CancellationToken cancellationToken = default) =>
        ExecutionContextUtil.RunInlineOrOffload(static s => System.IO.Directory.Exists((string)s!), directory, cancellationToken);

    public ValueTask<List<string>> GetEmptyDirectories(string root, CancellationToken cancellationToken = default) =>
        ExecutionContextUtil.RunInlineOrOffload(static s =>
        {
            (string Root, CancellationToken Token) = ((string Root, CancellationToken Token))s!;
            var result = new List<string>();

            foreach (var d in System.IO.Directory.EnumerateDirectories(Root, "*", SearchOption.AllDirectories))
            {
                Token.ThrowIfCancellationRequested();
                // Fast "Any()" without LINQ: just probe enumerator once.
                using var e = System.IO.Directory.EnumerateFileSystemEntries(d)
                                    .GetEnumerator();
                if (!e.MoveNext())
                    result.Add(d);
            }

            return result;
        }, (root, cancellationToken), cancellationToken);

    public ValueTask DeleteEmptyDirectories(string root, CancellationToken cancellationToken = default) =>
        ExecutionContextUtil.RunInlineOrOffload(static s =>
        {
            (string Root, CancellationToken Token, ILogger<DirectoryUtil> Logger) =
                ((string Root, CancellationToken Token, ILogger<DirectoryUtil> Logger))s!;

            // If you want to delete deepest-first to avoid missing newly-empty parents,
            // you can sort by depth descending. Keeping your current behavior.
            foreach (var d in System.IO.Directory.EnumerateDirectories(Root, "*", SearchOption.AllDirectories))
            {
                Token.ThrowIfCancellationRequested();

                using var e = System.IO.Directory.EnumerateFileSystemEntries(d)
                                    .GetEnumerator();
                if (!e.MoveNext())
                {
                    Logger.LogDebug("Deleting empty directory: {dir}", d);
                    System.IO.Directory.Delete(d);
                }
            }
        }, (root, cancellationToken, _logger), cancellationToken);

    public ValueTask<List<string>> GetDirectoriesContainingFile(string root, string fileName, CancellationToken cancellationToken = default)
    {
        // Avoid extra work if fileName is empty
        if (string.IsNullOrEmpty(fileName))
            return ValueTask.FromResult(new List<string>());

        return ExecutionContextUtil.RunInlineOrOffload(static s =>
        {
            (string Root, string FileName, CancellationToken Token) = ((string Root, string FileName, CancellationToken Token))s!;
            var result = new List<string>();

            foreach (var d in System.IO.Directory.EnumerateDirectories(Root, "*", SearchOption.AllDirectories))
            {
                Token.ThrowIfCancellationRequested();
                // Combine alloc is unavoidable; File.Exists is the real cost anyway.
                if (File.Exists(System.IO.Path.Combine(d, FileName)))
                    result.Add(d);
            }

            return result;
        }, (root, fileName, cancellationToken), cancellationToken);
    }

    public ValueTask<List<string>> GetFilesByExtension(string directory, string extension, bool recursive = false, CancellationToken cancellationToken = default) =>
        ExecutionContextUtil.RunInlineOrOffload(static s =>
        {
            (string Directory, string Extension, bool Recursive, CancellationToken Token) = ((string Directory, string Extension, bool Recursive, CancellationToken Token))s!;

            // Avoid string interpolation + repeated TrimStart work
            var ext = Extension;
            if (ext.Length > 0 && ext[0] == '.')
                ext = ext.Substring(1);

            var pattern = ext.Length == 0 ? "*" : "*." + ext;

            var result = new List<string>();

            foreach (var f in System.IO.Directory.EnumerateFiles(Directory, pattern, Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
            {
                Token.ThrowIfCancellationRequested();
                result.Add(f);
            }

            return result;
        }, (directory, extension, recursive, cancellationToken), cancellationToken);

    /// <summary>
    /// Recursively copies all files and subdirectories from the specified source directory to the specified destination
    /// directory.
    /// </summary>
    /// <remarks>If overwrite is false, existing files in the destination directory with the same name as
    /// files in the source directory are not replaced. The method copies the entire directory tree, including all
    /// subdirectories and their contents. The operation is performed asynchronously and can be cancelled via the
    /// provided cancellation token.</remarks>
    /// <param name="sourceDir">The path of the directory to copy. Must refer to an existing directory.</param>
    /// <param name="destDir">The path of the destination directory where the contents will be copied. The directory will be created if it
    /// does not exist.</param>
    /// <param name="overwrite">true to overwrite existing files in the destination directory; otherwise, false. The default is true.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the copy operation.</param>
    /// <returns>A ValueTask that represents the asynchronous copy operation.</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown if sourceDir does not exist.</exception>
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

    /// <summary>
    /// Moves an existing directory to a new location, optionally logging the operation and supporting cancellation.
    /// </summary>
    /// <param name="sourceDir">The path of the directory to move. This path must refer to an existing directory.</param>
    /// <param name="destinationDir">The path to which the directory should be moved. The destination must not already exist.</param>
    /// <param name="log">true to log the move operation; otherwise, false. The default is true.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests. The default is none.</param>
    /// <returns>A ValueTask that represents the asynchronous move operation.</returns>
    public ValueTask Move(string sourceDir, string destinationDir, bool log = true, CancellationToken cancellationToken = default)
    {
        if (log)
            _logger.LogDebug("{name} start from {source} to {dest} ...", nameof(Move), sourceDir, destinationDir);

        return ExecutionContextUtil.RunInlineOrOffload(static s =>
        {
            (string Source, string Destination) = ((string Source, string Destination))s!;
            System.IO.Directory.Move(Source, Destination);
        }, (sourceDir, destinationDir), cancellationToken);
    }

    /// <summary>
    /// Returns a normalized directory path with all trailing directory separators removed, except for root paths.
    /// </summary>
    /// <remarks>This method converts the specified path to its absolute form and removes any trailing
    /// directory or alternate directory separator characters, unless the path represents a root directory. The
    /// normalization is platform-specific and uses the current operating system's path rules.</remarks>
    /// <param name="directory">The directory path to normalize. Can be relative or absolute. Cannot be null or an empty string.</param>
    /// <returns>A normalized absolute directory path with trailing separators removed, except when the path is a root (for
    /// example, "C:\").</returns>
    [Pure]
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

    [Pure]
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