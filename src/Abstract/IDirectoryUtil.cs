using Soenneker.Utils.Directory.Utils;
using System;
using System.Collections.Generic;
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
    ValueTask<List<string>> GetAllDirectories(string directory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all immediate subdirectories as a list.
    /// </summary>
    ValueTask<List<string>> GetAllAsEnumerable(string directory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all subdirectories recursively from the specified directory.
    /// </summary>
    ValueTask<List<string>> GetAllDirectoriesRecursively(string directory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all subdirectories recursively as a list.
    /// </summary>
    ValueTask<List<string>> GetAllRecursivelyAsEnumerable(string directory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the specified directory and all its contents.
    /// </summary>
    ValueTask Delete(string directory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the directory if it exists.
    /// </summary>
    ValueTask DeleteIfExists(string directory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates the directory if it does not exist.
    /// </summary>
    /// <returns>True if the directory was created, false if it already existed.</returns>
    ValueTask<bool> CreateIfDoesNotExist(string directory, bool log = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the working directory of the currently executing assembly.
    /// </summary>
    string GetWorkingDirectory(bool log = false);

    /// <summary>
    /// Creates and returns a unique temporary directory path (and creates the folder).
    /// </summary>
    ValueTask<string> CreateTempDirectory(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the specified directory exists.
    /// </summary>
    ValueTask<bool> Exists(string directory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all empty subdirectories within the specified root directory.
    /// </summary>
    ValueTask<List<string>> GetEmptyDirectories(string root, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all empty directories within the specified root directory.
    /// </summary>
    ValueTask DeleteEmptyDirectories(string root, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds all subdirectories (recursively) that contain a file with the specified name.
    /// </summary>
    ValueTask<List<string>> GetDirectoriesContainingFile(string root, string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all files in the directory that match the given extension.
    /// </summary>
    ValueTask<List<string>> GetFilesByExtension(string directory, string extension, bool recursive = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously copies the contents of one directory to another.
    /// </summary>
    ValueTask CopyDirectory(string sourceDir, string destDir, bool overwrite = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a directory to a new location.
    /// </summary>
    ValueTask Move(string sourceDir, string destinationDir, bool log = true, CancellationToken cancellationToken = default);

    ValueTask LogContentsRecursively(string path, int indentLevel = 0, CancellationToken cancellationToken = default);

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
    ValueTask<long> GetSizeInBytes(string directory, GetSizeOptions? options = null, CancellationToken cancellationToken = default);
}