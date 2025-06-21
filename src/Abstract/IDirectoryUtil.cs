using System.Collections.Generic;
using System.Diagnostics.Contracts;
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
    /// Gets the total size in bytes of all files in the directory (recursively).
    /// </summary>
    long GetSizeInBytes(string directory);

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
}