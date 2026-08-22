using Microsoft.Extensions.Logging;
using Soenneker.Utils.Directory.Abstract;
using Soenneker.Utils.Path.Abstract;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.IO.Enumeration;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Extensions.Spans.Readonly.Chars;
using Soenneker.Extensions.Task;
using Soenneker.Utils.Directory.Dtos;
using Soenneker.Utils.ExecutionContexts;

namespace Soenneker.Utils.Directory;

///<inheritdoc cref="IDirectoryUtil"/>
public sealed class DirectoryUtil : IDirectoryUtil
{
    private const int _copyBufferSize = 128 * 1024;

    private readonly IPathUtil _pathUtil;
    private readonly ILogger<DirectoryUtil> _logger;

    public DirectoryUtil(IPathUtil pathUtil, ILogger<DirectoryUtil> logger)
    {
        _pathUtil = pathUtil;
        _logger = logger;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<List<string>> GetAllDirectories(string directory, CancellationToken cancellationToken = default) =>
        ExecutionContextUtil.RunInlineOrOffload(static s =>
        {
            var (dir, token) = ((string Directory, CancellationToken Token))s;
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
            var (dir, token) = ((string Directory, CancellationToken Token))s;
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
            System.IO.Directory.Delete(s, recursive: true);
        }, directory, cancellationToken);
    }

    public ValueTask DeleteIfExists(string directory, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Deleting directory ({dir}) if it exists...", directory);

        return ExecutionContextUtil.RunInlineOrOffload(static s =>
        {
            var dir = s;
            // Exists check still required to avoid exception cost
            if (System.IO.Directory.Exists(dir))
                System.IO.Directory.Delete(dir, recursive: true);
        }, directory, cancellationToken);
    }

    public ValueTask<bool> Create(string directory, bool log = true, CancellationToken cancellationToken = default)
    {
        if (log)
            _logger.LogDebug("Creating directory ({dir}) if it doesn't exist...", directory);

        return ExecutionContextUtil.RunInlineOrOffload(static s =>
        {
            var dir = s;

            // Note: CreateDirectory is idempotent; but if you truly need "created vs existed", keep Exists().
            if (System.IO.Directory.Exists(dir))
                return false;

            System.IO.Directory.CreateDirectory(dir);
            return true;
        }, directory, cancellationToken);
    }

    public ValueTask<bool> TryCreate(string directory, bool log = true, CancellationToken cancellationToken = default)
    {
        if (log)
            _logger.LogDebug("Attempting to create directory ({dir}) ...", directory);

        return ExecutionContextUtil.RunInlineOrOffload(static s =>
        {
            var (dir, token) = ((string Dir, CancellationToken Token))s;
            token.ThrowIfCancellationRequested();

            if (System.IO.Directory.Exists(dir))
                return false;

            System.IO.Directory.CreateDirectory(dir);
            return true;
        }, (directory, cancellationToken), cancellationToken);
    }

    public ValueTask CreateStrict(string directory, bool log = true, CancellationToken cancellationToken = default)
    {
        if (log)
            _logger.LogDebug("Creating directory strictly ({dir}) ...", directory);

        return ExecutionContextUtil.RunInlineOrOffload(static s =>
        {
            var (dir, token) = ((string Dir, CancellationToken Token))s;
            token.ThrowIfCancellationRequested();

            if (System.IO.Directory.Exists(dir))
                throw new IOException($"Directory already exists: {dir}");

            System.IO.Directory.CreateDirectory(dir);
        }, (directory, cancellationToken), cancellationToken);
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
            var (basePath, token) = ((string BasePath, CancellationToken Token))s;

            var dirs = System.IO.Directory.GetDirectories(basePath, "*", SearchOption.AllDirectories);
            var depths = new int[dirs.Length];
            var sep = System.IO.Path.DirectorySeparatorChar;

            for (var i = 0; i < dirs.Length; i++)
            {
                token.ThrowIfCancellationRequested();
                depths[i] = dirs[i].CountChar(sep);
            }

            // Sort the path array in place using compact integer keys. This avoids
            // the much larger (string, int) tuple array used previously.
            Array.Sort(depths, dirs);
            return new List<string>(dirs);
        }, (basePath, cancellationToken), cancellationToken);

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
        ExecutionContextUtil.RunInlineOrOffload(static s => System.IO.Directory.Exists(s), directory, cancellationToken);

    public ValueTask<List<string>> GetEmptyDirectories(string root, CancellationToken cancellationToken = default) =>
        ExecutionContextUtil.RunInlineOrOffload(static s =>
        {
            var (root, token) = ((string Root, CancellationToken Token))s;
            var result = new List<string>();
            var pending = new Stack<(string ScanPath, string ResultPath, bool IncludeInResult)>();
            pending.Push((System.IO.Path.GetFullPath(root), root, false));

            while (pending.TryPop(out var current))
            {
                token.ThrowIfCancellationRequested();

                var entries = new FileSystemEnumerable<string?>(current.ScanPath,
                    static (ref FileSystemEntry entry) => entry.IsDirectory ? entry.ToFullPath() : null);
                var isEmpty = true;

                foreach (var subdirectory in entries)
                {
                    isEmpty = false;
                    if (subdirectory is not null)
                        pending.Push((subdirectory, System.IO.Path.Combine(current.ResultPath, System.IO.Path.GetFileName(subdirectory)), true));
                }

                if (current.IncludeInResult && isEmpty)
                    result.Add(current.ResultPath);
            }

            return result;
        }, (root, cancellationToken), cancellationToken);

    public ValueTask DeleteEmptyDirectories(string root, CancellationToken cancellationToken = default) =>
        ExecutionContextUtil.RunInlineOrOffload(static s =>
        {
            var (root, token, logger) =
                ((string Root, CancellationToken Token, ILogger<DirectoryUtil> Logger))s;
            var states = new List<DirectoryState>(32) {new(root, -1)};

            // Build the tree with one enumeration per directory. A reverse pass can
            // then remove empty chains without rescanning every directory.
            for (var index = 0; index < states.Count; index++)
            {
                token.ThrowIfCancellationRequested();
                var state = states[index];
                var entries = new FileSystemEnumerable<string?>(state.Path,
                    static (ref FileSystemEntry entry) => entry.IsDirectory ? entry.ToFullPath() : null);

                foreach (var subdirectory in entries)
                {
                    token.ThrowIfCancellationRequested();

                    if (subdirectory is null)
                        state.HasFiles = true;
                    else
                        states.Add(new DirectoryState(subdirectory, index));
                }

                states[index] = state;
            }

            for (var index = states.Count - 1; index > 0; index--)
            {
                token.ThrowIfCancellationRequested();
                var state = states[index];

                if (!state.HasFiles && !state.HasRemainingChild)
                {
                    logger.LogDebug("Deleting empty directory: {dir}", state.Path);
                    System.IO.Directory.Delete(state.Path);
                    continue;
                }

                var parent = states[state.ParentIndex];
                parent.HasRemainingChild = true;
                states[state.ParentIndex] = parent;
            }
        }, (root, cancellationToken, _logger), cancellationToken);

    public ValueTask<List<string>> GetDirectoriesContainingFile(string root, string fileName, CancellationToken cancellationToken = default)
    {
        // Avoid extra work if fileName is empty
        if (string.IsNullOrEmpty(fileName))
            return ValueTask.FromResult(new List<string>());

        return ExecutionContextUtil.RunInlineOrOffload(static s =>
        {
            var (root, fileName, token) = ((string Root, string FileName, CancellationToken Token))s;
            var result = new List<string>();
            var fullRoot = System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(root));
            var rootIsFullyQualified = System.IO.Path.IsPathFullyQualified(root);
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            var enumerationOptions = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = false,
                AttributesToSkip = 0
            };

            var matches = new FileSystemEnumerable<string>(fullRoot, (ref FileSystemEntry entry) =>
            {
                var directory = entry.Directory.ToString();
                return rootIsFullyQualified ? directory : System.IO.Path.Combine(root, System.IO.Path.GetRelativePath(fullRoot, directory));
            }, enumerationOptions)
            {
                ShouldIncludePredicate = (ref FileSystemEntry entry) =>
                    !entry.IsDirectory &&
                    entry.FileName.Equals(fileName.AsSpan(), comparison) &&
                    !entry.Directory.Equals(fullRoot.AsSpan(), comparison)
            };

            foreach (var directory in matches)
            {
                token.ThrowIfCancellationRequested();
                result.Add(directory);
            }

            return result;
        }, (root, fileName, cancellationToken), cancellationToken);
    }

    public ValueTask<List<string>> GetFilesByExtension(string directory, string extension, bool recursive = false, CancellationToken cancellationToken = default) =>
        ExecutionContextUtil.RunInlineOrOffload(static s =>
        {
            var (directory, extension, recursive, token) = ((string Directory, string Extension, bool Recursive, CancellationToken Token))s;

            // Avoid string interpolation + repeated TrimStart work
            var pattern = extension.Length switch
            {
                0 => "*",
                _ when extension[0] == '.' => string.Concat("*", extension),
                _ => string.Concat("*.", extension)
            };

            var result = new List<string>();

            foreach (var f in System.IO.Directory.EnumerateFiles(directory, pattern, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
            {
                token.ThrowIfCancellationRequested();
                result.Add(f);
            }

            return result;
        }, (directory, extension, recursive, cancellationToken), cancellationToken);

    public async ValueTask CopyDirectory(string sourceDir, string destDir, bool overwrite = true, CancellationToken cancellationToken = default)
    {
        if (!System.IO.Directory.Exists(sourceDir))
            throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");

        var srcOpts = new FileStreamOptions
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.Read,
            Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
            BufferSize = 1
        };

        var dstOpts = new FileStreamOptions
        {
            Mode = overwrite ? FileMode.Create : FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
            Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
            BufferSize = 1
        };

        var pending = new Stack<(string Source, string Destination)>();
        pending.Push((sourceDir, destDir));

        while (pending.TryPop(out var current))
        {
            cancellationToken.ThrowIfCancellationRequested();
            System.IO.Directory.CreateDirectory(current.Destination);

            foreach (var file in System.IO.Directory.EnumerateFiles(current.Source))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var destFile = System.IO.Path.Combine(current.Destination, System.IO.Path.GetFileName(file));

                if (!overwrite && File.Exists(destFile))
                    continue;

                await using var sourceStream = new FileStream(file, srcOpts);
                dstOpts.PreallocationSize = sourceStream.Length;
                await using var destinationStream = new FileStream(destFile, dstOpts);

                await sourceStream.CopyToAsync(destinationStream, _copyBufferSize, cancellationToken).NoSync();
            }

            foreach (var subdir in System.IO.Directory.EnumerateDirectories(current.Source))
                pending.Push((subdir, System.IO.Path.Combine(current.Destination, System.IO.Path.GetFileName(subdir))));
        }
    }

    public ValueTask Move(string sourceDir, string destinationDir, bool log = true, CancellationToken cancellationToken = default)
    {
        if (log)
            _logger.LogDebug("{name} start from {source} to {dest} ...", nameof(Move), sourceDir, destinationDir);

        return ExecutionContextUtil.RunInlineOrOffload(static s =>
        {
            var (source, destination) = ((string Source, string Destination))s;
            System.IO.Directory.Move(source, destination);
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

        return ExecutionContextUtil.RunInlineOrOffload(static s => s.Self.LogContentsRecursivelySync(s), args, cancellationToken);
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

        // The common depths use interned strings: no dictionary lookup and no
        // per-node allocation. Very deep trees retain the previous behavior.
        return indentLevel switch
        {
            1 => "  ",
            2 => "    ",
            3 => "      ",
            4 => "        ",
            5 => "          ",
            6 => "            ",
            7 => "              ",
            8 => "                ",
            _ => new string(' ', checked(indentLevel * 2))
        };
    }

    [Pure]
    public ValueTask<long> GetSizeInBytes(string directory, GetSizeOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (!System.IO.Directory.Exists(directory))
            return ValueTask.FromResult(0L);

        var args = new GetSizeArgs(directory, options, cancellationToken, _logger);

        // If on a UI SyncContext, offload; otherwise scan inline (fastest, lowest overhead).
        return ExecutionContextUtil.RunInlineOrOffload(static s => ScanSize(s), args, cancellationToken);
    }

    private static long ScanSize(GetSizeArgs args)
    {
        var opts = args.Options ?? new GetSizeOptions();

        long totalSize = 0;

        var stack = new Stack<string>(capacity: 32);
        stack.Push(args.Directory);

        while (stack.Count > 0)
        {
            args.CancellationToken.ThrowIfCancellationRequested();

            var currentDir = stack.Pop();

            try
            {
                if (opts.Recursive)
                {
                    // Enumerate each directory once. File paths and FileInfo objects
                    // are never materialized; only subdirectory paths are allocated.
                    var entries = new FileSystemEnumerable<SizeEntry>(currentDir, static (ref FileSystemEntry entry) =>
                        entry.IsDirectory ? new SizeEntry(entry.ToFullPath(), 0) : new SizeEntry(null, entry.Length));

                    foreach (var entry in entries)
                    {
                        args.CancellationToken.ThrowIfCancellationRequested();

                        if (entry.Directory is not null)
                            stack.Push(entry.Directory);
                        else
                            totalSize += entry.Length;
                    }
                }
                else
                {
                    var files = new FileSystemEnumerable<long>(currentDir, static (ref FileSystemEntry entry) => entry.Length)
                    {
                        ShouldIncludePredicate = static (ref FileSystemEntry entry) => !entry.IsDirectory
                    };

                    foreach (var length in files)
                    {
                        args.CancellationToken.ThrowIfCancellationRequested();
                        totalSize += length;
                    }
                }

                opts.Progress?.Report(totalSize);
            }
            catch (OperationCanceledException)
            {
                throw;
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

        return ExecutionContextUtil.RunInlineOrOffload(static s => s.Self.MoveContentsUpOneLevelStrictSync(s), args, cancellationToken);
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
