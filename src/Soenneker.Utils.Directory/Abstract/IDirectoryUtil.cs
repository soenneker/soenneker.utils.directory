using Soenneker.Utils.Directory.Dtos;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Utils.Directory.Abstract;

/// <summary>
/// A utility interface for performing various directory-related operations.
/// </summary>
public interface IDirectoryUtil
{
    /// <summary>
    /// Retrieves all immediate subdirectories in the specified directory.
    /// </summary>
    /// <returns>The all immediate subdirectories in the specified directory.</returns>
    [Pure]
    ValueTask<List<string>> GetAllDirectories(string directory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all immediate subdirectories as a list.
    /// </summary>
    /// <returns>The all immediate subdirectories as a list.</returns>
    [Pure]
    ValueTask<List<string>> GetAllAsEnumerable(string directory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all subdirectories recursively from the specified directory.
    /// </summary>
    /// <returns>The all subdirectories recursively from the specified directory.</returns>
    [Pure]
    ValueTask<List<string>> GetAllDirectoriesRecursively(string directory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all subdirectories recursively as a list.
    /// </summary>
    /// <returns>The all subdirectories recursively as a list.</returns>
    [Pure]
    ValueTask<List<string>> GetAllRecursivelyAsEnumerable(string directory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the specified directory and all its contents.
    /// </summary>
    /// <remarks>On Windows, read-only attributes are cleared from the directory tree before deletion.</remarks>
    /// <returns>Deletes the specified directory and all its contents.</returns>
    ValueTask Delete(string directory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the directory if it exists.
    /// </summary>
    /// <remarks>On Windows, read-only attributes are cleared from the directory tree before deletion.</remarks>
    /// <returns>Deletes the directory if it exists.</returns>
    ValueTask DeleteIfExists(string directory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates the directory if it does not exist.
    /// </summary>
    /// <returns>True if the directory was created, false if it already existed.</returns>
    ValueTask<bool> Create(string directory, bool log = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to create the specified directory.
    /// </summary>
    /// <remarks>
    /// Returns <see langword="true"/> only if the directory did not previously exist.
    /// </remarks>
    /// <returns>Attempts to create the specified directory.</returns>
    ValueTask<bool> TryCreate(string directory, bool log = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates the specified directory and throws if it already exists.
    /// </summary>
    /// <exception cref="IOException">
    /// Thrown if the directory already exists.
    /// </exception>
    /// <returns>Creates the specified directory and throws if it already exists.</returns>
    ValueTask CreateStrict(string directory, bool log = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the working directory of the currently executing assembly.
    /// </summary>
    /// <returns>The the working directory of the currently executing assembly.</returns>
    [Pure]
    string GetWorkingDirectory(bool log = false);

    /// <summary>
    /// Creates and returns a unique temporary directory path (and creates the folder).
    /// </summary>
    /// <returns>Creates and returns a unique temporary directory path (and creates the folder).</returns>
    ValueTask<string> CreateTempDirectory(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the specified directory exists.
    /// </summary>
    /// <returns><see langword="true"/> when the specified directory exists; otherwise <see langword="false"/>.</returns>
    [Pure]
    ValueTask<bool> Exists(string directory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all empty subdirectories within the specified root directory.
    /// </summary>
    /// <returns>The all empty subdirectories within the specified root directory.</returns>
    [Pure]
    ValueTask<List<string>> GetEmptyDirectories(string root, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all empty directories within the specified root directory.
    /// </summary>
    /// <returns>Deletes all empty directories within the specified root directory.</returns>
    ValueTask DeleteEmptyDirectories(string root, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds all subdirectories (recursively) that contain a file with the specified name.
    /// </summary>
    /// <returns>Finds all subdirectories (recursively) that contain a file with the specified name.</returns>
    [Pure]
    ValueTask<List<string>> GetDirectoriesContainingFile(string root, string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all files in the directory that match the given extension.
    /// </summary>
    /// <remarks>Recursive searches use up to eight workers. Result ordering is unspecified.</remarks>
    /// <returns>All files in the directory that match the given extension.</returns>
    [Pure]
    ValueTask<List<string>> GetFilesByExtension(string directory, string extension, bool recursive = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets files matching an extension with a configurable limit on recursive search concurrency.
    /// </summary>
    /// <param name="directory">The directory to search.</param>
    /// <param name="extension">The extension, with or without a leading dot; empty matches all files.</param>
    /// <param name="recursive">Whether to search subdirectories.</param>
    /// <param name="maxDegreeOfParallelism">The maximum number of concurrent subtree scans. Must be positive; one scans sequentially.</param>
    /// <param name="cancellationToken">Signals that the search should stop.</param>
    /// <returns>All matching file paths, in unspecified order.</returns>
    /// <remarks>Nonrecursive searches run sequentially. Multiple concurrent failures may be reported as an <see cref="AggregateException"/>.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">The concurrency limit is less than one.</exception>
    [Pure]
    ValueTask<List<string>> GetFilesByExtension(string directory, string extension, bool recursive, int maxDegreeOfParallelism,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously copies the contents of one directory to another.
    /// </summary>
    /// <remarks>Copies up to four files concurrently, skips reparse points, and preserves empty directories.
    /// Existing destination files are skipped when overwrite is false. Cancellation or failure can leave a partial copy;
    /// all started copies finish or stop before the operation completes.</remarks>
    /// <returns>An awaitable that completes after all file copies have stopped.</returns>
    ValueTask CopyDirectory(string sourceDir, string destDir, bool overwrite = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Copies a directory with a configurable limit on concurrent file copies.
    /// </summary>
    /// <param name="sourceDir">The source directory.</param>
    /// <param name="destDir">The destination directory.</param>
    /// <param name="overwrite">Whether to replace existing files; false skips them.</param>
    /// <param name="maxDegreeOfParallelism">The maximum concurrent file copies. Must be positive; one copies sequentially.</param>
    /// <param name="cancellationToken">Signals that copying should stop.</param>
    /// <returns>An awaitable that completes after all started copies have finished or stopped.</returns>
    /// <remarks>Skips reparse points and preserves empty directories. Cancellation or failure may leave a partial copy.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">The concurrency limit is less than one.</exception>
    ValueTask CopyDirectory(string sourceDir, string destDir, bool overwrite, int maxDegreeOfParallelism,
        CancellationToken cancellationToken = default);


    /// <summary>
    /// Moves a directory to a new location.
    /// </summary>
    /// <returns>Moves a directory to a new location.</returns>
    ValueTask Move(string sourceDir, string destinationDir, bool log = true, CancellationToken cancellationToken = default);

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
    ValueTask LogContentsRecursively(string path, int indentLevel = 0, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves every entry from a directory into its parent and removes the now-empty directory, failing on conflicts.
    /// </summary>
    /// <param name="tempDir">The directory whose entries are moved into its parent.</param>
    /// <param name="cancellationToken">Signals that the operation should stop.</param>
    /// <returns>An awaitable that completes after all entries have moved and the source directory has been removed.</returns>
    /// <remarks>Unlike a best-effort move, this method does not skip name collisions or inaccessible entries; the first failed move aborts the operation.</remarks>
    ValueTask MoveContentsUpOneLevelStrict(string tempDir, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously calculates the total size in bytes of all files within a specified directory.
    /// This method is optimized for performance, supports cancellation, progress reporting, and robust error handling.
    /// </summary>
    /// <remarks>
    /// This method uses a non-recursive, stack-based approach to traverse directories, preventing stack overflow exceptions.
    /// Since file system enumeration is inherently synchronous, this method uses <see cref="Task.Run(Action, CancellationToken)"/> to offload the
    /// entire operation to a thread pool thread, ensuring the calling thread (e.g., the UI thread) remains responsive.
    /// Progress updates and cancellation are checked periodically during the scan.
    /// </remarks>
    /// <param name="directory">The absolute or relative path to the directory.</param>
    /// <param name="options">Optional configuration for the calculation, such as recursion, error handling, and progress reporting.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> representing the asynchronous operation, which returns the total size of the directory in bytes. Returns 0 if the directory does not exist.</returns>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the <paramref name="cancellationToken"/>.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown if <see cref="GetSizeOptions.ContinueOnError"/> is false and a subdirectory cannot be accessed.</exception>
    [Pure]
    ValueTask<long> GetSizeInBytes(string directory, GetSizeOptions? options = null, CancellationToken cancellationToken = default);
}
