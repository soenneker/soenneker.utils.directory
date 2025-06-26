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
    List<string> GetAllDirectories(string directory);

    /// <summary>
    /// Retrieves all immediate subdirectories as an enumerable.
    /// </summary>
    IEnumerable<string> GetAllAsEnumerable(string directory);

    /// <summary>
    /// Retrieves all subdirectories recursively from the specified directory.
    /// </summary>
    List<string> GetAllDirectoriesRecursively(string directory);

    /// <summary>
    /// Retrieves all subdirectories recursively as an enumerable.
    /// </summary>
    IEnumerable<string> GetAllRecursivelyAsEnumerable(string directory);

    /// <summary>
    /// Deletes the specified directory and all its contents.
    /// </summary>
    void Delete(string directory);

    /// <summary>
    /// Deletes the directory if it exists.
    /// </summary>
    void DeleteIfExists(string directory);

    /// <summary>
    /// Creates the directory if it does not exist.
    /// </summary>
    /// <returns>True if the directory was created, false if it already existed.</returns>
    bool CreateIfDoesNotExist(string directory, bool log = true);

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
    bool Exists(string directory);

    /// <summary>
    /// Retrieves all empty subdirectories within the specified root directory.
    /// </summary>
    List<string> GetEmptyDirectories(string root);

    /// <summary>
    /// Deletes all empty directories within the specified root directory.
    /// </summary>
    void DeleteEmptyDirectories(string root);

    /// <summary>
    /// Finds all subdirectories (recursively) that contain a file with the specified name.
    /// </summary>
    List<string> GetDirectoriesContainingFile(string root, string fileName);

    /// <summary>
    /// Gets all files in the directory that match the given extension.
    /// </summary>
    List<string> GetFilesByExtension(string directory, string extension, bool recursive = false);

    /// <summary>
    /// Asynchronously copies the contents of one directory to another.
    /// </summary>
    ValueTask CopyDirectory(string sourceDir, string destDir, bool overwrite = true, CancellationToken cancellationToken = default);

    void LogContentsRecursively(string path, int indentLevel = 0);

    void MoveContentsUpOneLevelStrict(string tempDir);

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