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
using Soenneker.Extensions.Spans.Readonly.Chars;
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

    /// <summary>
    /// Asynchronously retrieves the full paths of all immediate subdirectories within the specified directory.
    /// </summary>
    /// <remarks>The method throws an <see cref="OperationCanceledException"/> if the operation is canceled
    /// via the cancellation token. Only immediate subdirectories are returned; nested subdirectories are not
    /// included.</remarks>
    /// <param name="directory">The path of the directory from which to retrieve subdirectory paths. This parameter cannot be null or empty.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation. The default value is <see
    /// cref="CancellationToken.None"/>.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of strings, each representing
    /// the full path of a subdirectory found in the specified directory. The list will be empty if no subdirectories
    /// are found.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<List<string>> GetAllDirectories(string directory, CancellationToken cancellationToken = default) =>
        ExecutionContextUtil.RunInlineOrOffload(static s =>
        {
            var (dir, token) = ((string Directory, CancellationToken Token))s!;
            var list = new List<string>();

            foreach (var d in System.IO.Directory.EnumerateDirectories(dir))
            {
                token.ThrowIfCancellationRequested();
                list.Add(d);
            }

            return list;
        }, (directory, cancellationToken), cancellationToken);

    /// <summary>
    /// Asynchronously retrieves a list of all directories within the specified directory.
    /// </summary>
    /// <remarks>This method is optimized for performance using aggressive inlining. Ensure that the specified
    /// directory exists to avoid exceptions or unexpected results.</remarks>
    /// <param name="directory">The path of the directory from which to retrieve subdirectory names. This parameter cannot be null or empty.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation. The default value is a
    /// non-cancelable token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of strings representing the
    /// names of all directories found in the specified directory.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<List<string>> GetAllAsEnumerable(string directory, CancellationToken cancellationToken = default) =>
        GetAllDirectories(directory, cancellationToken);

    /// <summary>
    /// Asynchronously retrieves the full paths of all directories within the specified directory and its
    /// subdirectories.
    /// </summary>
    /// <remarks>The operation is performed recursively, including all nested subdirectories. If the operation
    /// is canceled via the provided cancellation token, an <see cref="OperationCanceledException"/> is
    /// thrown.</remarks>
    /// <param name="directory">The path of the root directory to search. This parameter cannot be null or empty.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation. The default value is <see
    /// cref="CancellationToken.None"/>.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of strings representing the
    /// full paths of all directories found. The list will be empty if no directories are found.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<List<string>> GetAllDirectoriesRecursively(string directory, CancellationToken cancellationToken = default) =>
        ExecutionContextUtil.RunInlineOrOffload(static s =>
        {
            var (dir, token) = ((string Directory, CancellationToken Token))s!;
            var list = new List<string>();
            foreach (var d in System.IO.Directory.EnumerateDirectories(dir, "*", SearchOption.AllDirectories))
            {
                token.ThrowIfCancellationRequested();
                list.Add(d);
            }

            return list;
        }, (directory, cancellationToken), cancellationToken);

    /// <summary>
    /// Asynchronously retrieves a list of all directories within the specified directory and its subdirectories.
    /// </summary>
    /// <remarks>This method performs a recursive search for directories. Use the cancellation token to cancel
    /// the operation if it may run for an extended period, especially on large directory trees.</remarks>
    /// <param name="directory">The full path of the directory to search. This parameter cannot be null or empty.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of strings representing the
    /// full paths of all directories found. The list is empty if no directories are found.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<List<string>> GetAllRecursivelyAsEnumerable(string directory, CancellationToken cancellationToken = default) =>
        GetAllDirectoriesRecursively(directory, cancellationToken);

    /// <summary>
    /// Deletes the specified directory and all of its contents asynchronously.
    /// </summary>
    /// <remarks>This method deletes the directory recursively, including all files and subdirectories. Ensure
    /// that the directory is not in use by another process before calling this method.</remarks>
    /// <param name="directory">The path of the directory to delete. The directory must exist and cannot be null.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the delete operation. The default value is <see
    /// cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous operation of deleting the directory.</returns>
    public ValueTask Delete(string directory, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Deleting directory ({dir}) ...", directory);
        return ExecutionContextUtil.RunInlineOrOffload(static s =>
        {
            System.IO.Directory.Delete((string)s!, recursive: true);
        }, directory, cancellationToken);
    }

    /// <summary>
    /// Deletes the specified directory and all its contents if the directory exists.
    /// </summary>
    /// <remarks>The deletion attempt is logged at the debug level. If the directory does not exist, the
    /// method completes without performing any action.</remarks>
    /// <param name="directory">The path of the directory to delete. Must be a valid directory path. If the directory does not exist, no action
    /// is taken.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the delete operation.</param>
    /// <returns>A task that represents the asynchronous operation of deleting the directory.</returns>
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

    /// <summary>
    /// Creates the specified directory if it does not already exist.
    /// </summary>
    /// <remarks>This method is idempotent; calling it multiple times with the same directory path does not
    /// result in an error. If logging is enabled, a debug message is logged indicating the creation attempt.</remarks>
    /// <param name="directory">The path of the directory to create. Cannot be null or empty.</param>
    /// <param name="log">true to log the creation attempt; otherwise, false. The default is true.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result is true if the directory was created;
    /// otherwise, false if it already existed.</returns>
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

    /// <summary>
    /// Gets the directory path of the currently executing assembly.
    /// </summary>
    /// <remarks>The assembly location may be empty in certain contexts, such as when running in some test
    /// environments or with single-file deployments. Logging is optional and can be enabled by setting the log
    /// parameter to <see langword="true"/>.</remarks>
    /// <param name="log">true to log the retrieved directory path at the debug level; otherwise, false.</param>
    /// <returns>A string containing the path to the directory where the executing assembly is located.</returns>
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
            var (basePath, token) = ((string BasePath, CancellationToken Token))s!;

            // Directory.GetDirectories returns array; if basePath is huge, you could stream + sort via list.
            var dirs = System.IO.Directory.GetDirectories(basePath, "*", SearchOption.AllDirectories);

            // Precompute depth once per string to avoid repeated scanning during sort comparisons.
            var items = new (string Path, int Depth)[dirs.Length];
            var sep = System.IO.Path.DirectorySeparatorChar;

            for (var i = 0; i < dirs.Length; i++)
            {
                token.ThrowIfCancellationRequested();
                var p = dirs[i];
                items[i] = (p, p.CountChar(sep));
            }

            Array.Sort(items, static (a, b) => a.Depth.CompareTo(b.Depth));

            var result = new List<string>(items.Length);
            for (var i = 0; i < items.Length; i++)
                result.Add(items[i].Path);

            return result;
        }, (basePath, cancellationToken), cancellationToken);

    /// <summary>
    /// Generates a new temporary directory path, but does not actually create the directory.
    /// </summary>
    [Pure]
    public static string GetNewTempDirectoryPath() =>
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid()
                                                                 .ToString("N"));

    /// <summary>
    /// Creates a uniquely named temporary directory and returns its path.
    /// </summary>
    /// <remarks>The created directory is guaranteed to be unique and suitable for temporary storage. The
    /// method may throw exceptions if the directory cannot be created or if the operation is canceled.</remarks>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the full path of the created
    /// temporary directory.</returns>
    public ValueTask<string> CreateTempDirectory(CancellationToken cancellationToken = default) =>
        _pathUtil.GetUniqueTempDirectory(null, true, cancellationToken);

    /// <summary>
    /// Asynchronously determines whether the specified directory exists on the file system.
    /// </summary>
    /// <remarks>This method performs the existence check asynchronously and can be canceled using the
    /// provided cancellation token.</remarks>
    /// <param name="directory">The path of the directory to check for existence. This parameter cannot be null or empty.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation. The default value is <see
    /// cref="CancellationToken.None"/>.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains <see langword="true"/> if the
    /// directory exists; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<bool> Exists(string directory, CancellationToken cancellationToken = default) =>
        ExecutionContextUtil.RunInlineOrOffload(static s => System.IO.Directory.Exists((string)s!), directory, cancellationToken);

    /// <summary>
    /// Asynchronously retrieves a list of all empty directories within the specified root directory and its
    /// subdirectories.
    /// </summary>
    /// <remarks>The search is performed recursively. A directory is considered empty if it contains no files
    /// or subdirectories. If the operation is canceled, an <see cref="OperationCanceledException"/> is
    /// thrown.</remarks>
    /// <param name="root">The full path to the root directory to search. This parameter cannot be null or an empty string.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation. The default value is <see
    /// cref="CancellationToken.None"/>.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of strings representing the
    /// full paths of empty directories found within the root directory.</returns>
    public ValueTask<List<string>> GetEmptyDirectories(string root, CancellationToken cancellationToken = default) =>
        ExecutionContextUtil.RunInlineOrOffload(static s =>
        {
            var (root, token) = ((string Root, CancellationToken Token))s!;
            var result = new List<string>();

            foreach (var d in System.IO.Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
            {
                token.ThrowIfCancellationRequested();
                // Fast "Any()" without LINQ: just probe enumerator once.
                using var e = System.IO.Directory.EnumerateFileSystemEntries(d)
                                    .GetEnumerator();
                if (!e.MoveNext())
                    result.Add(d);
            }

            return result;
        }, (root, cancellationToken), cancellationToken);

    /// <summary>
    /// Deletes all empty directories within the specified root directory and its subdirectories.
    /// </summary>
    /// <remarks>The method traverses the directory tree starting at the specified root and deletes
    /// directories that do not contain any files or subdirectories. Deletions are performed in a non-recursive manner
    /// and are logged for debugging purposes. If a directory is deleted, its parent may become empty and will be
    /// considered for deletion in subsequent iterations. The operation can be cancelled by providing a cancellation
    /// token.</remarks>
    /// <param name="root">The full path to the root directory from which to begin deleting empty directories. This path must exist and be
    /// accessible.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation before it completes.</param>
    /// <returns>A ValueTask that represents the asynchronous operation of deleting empty directories.</returns>
    public ValueTask DeleteEmptyDirectories(string root, CancellationToken cancellationToken = default) =>
        ExecutionContextUtil.RunInlineOrOffload(static s =>
        {
            var (root, token, logger) =
                ((string Root, CancellationToken Token, ILogger<DirectoryUtil> Logger))s!;

            // If you want to delete deepest-first to avoid missing newly-empty parents,
            // you can sort by depth descending. Keeping your current behavior.
            foreach (var d in System.IO.Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
            {
                token.ThrowIfCancellationRequested();

                using var e = System.IO.Directory.EnumerateFileSystemEntries(d)
                                    .GetEnumerator();
                if (!e.MoveNext())
                {
                    logger.LogDebug("Deleting empty directory: {dir}", d);
                    System.IO.Directory.Delete(d);
                }
            }
        }, (root, cancellationToken, _logger), cancellationToken);

    /// <summary>
    /// Asynchronously retrieves a list of directory paths under the specified root directory that contain a file with
    /// the given name.
    /// </summary>
    /// <remarks>The search includes all subdirectories of the specified root directory. The operation can be
    /// canceled by passing a cancellation token, in which case an OperationCanceledException is thrown.</remarks>
    /// <param name="root">The root directory to search. This path must exist and be accessible.</param>
    /// <param name="fileName">The name of the file to search for within subdirectories. If this parameter is empty, the method returns an
    /// empty list.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of directory paths that
    /// contain the specified file. The list is empty if no directories contain the file.</returns>
    public ValueTask<List<string>> GetDirectoriesContainingFile(string root, string fileName, CancellationToken cancellationToken = default)
    {
        // Avoid extra work if fileName is empty
        if (string.IsNullOrEmpty(fileName))
            return ValueTask.FromResult(new List<string>());

        return ExecutionContextUtil.RunInlineOrOffload(static s =>
        {
            var (root, fileName, token) = ((string Root, string FileName, CancellationToken Token))s!;
            var result = new List<string>();

            foreach (var d in System.IO.Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
            {
                token.ThrowIfCancellationRequested();
                // Combine alloc is unavoidable; File.Exists is the real cost anyway.
                if (File.Exists(System.IO.Path.Combine(d, fileName)))
                    result.Add(d);
            }

            return result;
        }, (root, fileName, cancellationToken), cancellationToken);
    }

    public ValueTask<List<string>> GetFilesByExtension(string directory, string extension, bool recursive = false, CancellationToken cancellationToken = default) =>
        ExecutionContextUtil.RunInlineOrOffload(static s =>
        {
            var (directory, extension, recursive, token) = ((string Directory, string Extension, bool Recursive, CancellationToken Token))s!;

            // Avoid string interpolation + repeated TrimStart work
            var ext = extension;
            if (ext.Length > 0 && ext[0] == '.')
                ext = ext.Substring(1);

            var pattern = ext.Length == 0 ? "*" : "*." + ext;

            var result = new List<string>();

            foreach (var f in System.IO.Directory.EnumerateFiles(directory, pattern, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
            {
                token.ThrowIfCancellationRequested();
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
            var (source, destination) = ((string Source, string Destination))s!;
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

    /// <summary>
    /// Asynchronously logs the contents of the specified directory and all its subdirectories.
    /// </summary>
    /// <remarks>This method enables structured logging of directory hierarchies and supports cancellation
    /// through the provided token.</remarks>
    /// <param name="path">The full path of the directory to log. This value must not be null or empty.</param>
    /// <param name="indentLevel">The indentation level to use for formatting the log output. A higher value increases the indentation of logged
    /// entries.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the logging operation.</param>
    /// <returns>A ValueTask that represents the asynchronous operation of logging the directory contents.</returns>
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

    /// <summary>
    /// Calculates the total size, in bytes, of the specified directory and its contents asynchronously.
    /// </summary>
    /// <remarks>If the specified directory does not exist, the method returns 0 without performing any
    /// further calculations. The method may offload the size calculation to a background thread if called from a UI
    /// context to avoid blocking the UI.</remarks>
    /// <param name="directory">The path to the directory for which the size is to be calculated. Must be a valid directory path.</param>
    /// <param name="options">Optional settings that influence the size calculation, such as whether to include subdirectories or hidden
    /// files.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the total size, in bytes, of the
    /// directory and its contents. Returns 0 if the directory does not exist.</returns>
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

    /// <summary>
    /// Moves all contents of the specified directory up one level in the directory hierarchy, replacing the parent
    /// directory's contents with those from the given directory.
    /// </summary>
    /// <remarks>This method executes asynchronously and may offload the operation to a background context for
    /// improved performance. Monitor the provided cancellation token to handle cancellation requests appropriately. The
    /// method is strict and may fail if the operation cannot be completed as intended.</remarks>
    /// <param name="tempDir">The path to the directory whose contents will be moved up one level. This path must refer to an existing
    /// directory.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the move operation.</param>
    /// <returns>A ValueTask that represents the asynchronous operation of moving the directory contents.</returns>
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