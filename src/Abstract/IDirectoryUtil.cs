using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace Soenneker.Utils.Directory.Abstract;

/// <summary>
/// A utility library encapsulating various directory methods
/// </summary>
public interface IDirectoryUtil
{
    [Pure]
    List<string> GetAllDirectories(string directory);

    [Pure]
    List<string> GetAllDirectoriesRecursively(string directory);

    [Pure]
    IEnumerable<string> GetAllAsEnumerable(string directory);

    [Pure]
    IEnumerable<string> GetAllRecursivelyAsEnumerable(string directory);

    /// <summary>
    /// Will throw various exceptions if it doesn't exist
    /// </summary>
    /// <param name="directory"></param>
    void Delete(string directory);

    void DeleteIfExists(string directory);

    /// <returns>True if directory was created, false if it already existed</returns>
    bool CreateIfDoesNotExist(string directory, bool log = true);

    /// <summary>
    /// Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!
    /// </summary>
    [Pure]
    string GetWorkingDirectory(bool log = false);

    string CreateTempDirectory();
}