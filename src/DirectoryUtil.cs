using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Soenneker.Utils.Directory.Abstract;
using System.Reflection;
using System;
using System.Diagnostics.Contracts;

namespace Soenneker.Utils.Directory;

///<inheritdoc cref="IDirectoryUtil"/>
public class DirectoryUtil : IDirectoryUtil
{
    private readonly ILogger<DirectoryUtil> _logger;

    public DirectoryUtil(ILogger<DirectoryUtil> logger)
    {
        _logger = logger;
    }

    public List<string> GetAllDirectories(string directory)
    {
        return System.IO.Directory.EnumerateDirectories(directory).ToList();
    }

    public IEnumerable<string> GetAllAsEnumerable(string directory)
    {
        return System.IO.Directory.EnumerateDirectories(directory);
    }

    public List<string> GetAllDirectoriesRecursively(string directory)
    {
        return System.IO.Directory.EnumerateDirectories(directory, "*", SearchOption.AllDirectories).ToList();
    }

    public IEnumerable<string> GetAllRecursivelyAsEnumerable(string directory)
    {
        return System.IO.Directory.EnumerateDirectories(directory, "*", SearchOption.AllDirectories);
    }

    public void Delete(string directory)
    {
        _logger.LogDebug("Deleting directory ({dir}) ...", directory);

        System.IO.Directory.Delete(directory, true);
    }

    public void DeleteIfExists(string directory)
    {
        _logger.LogDebug("Deleting directory ({dir}) if it exists...", directory);

        if (System.IO.Directory.Exists(directory))
            System.IO.Directory.Delete(directory, true);
    }

    public bool CreateIfDoesNotExist(string directory, bool log = true)
    {
        if (log)
            _logger.LogDebug("Creating directory ({dir}) if it doesn't exist...", directory);

        if (System.IO.Directory.Exists(directory))
            return false;

        System.IO.Directory.CreateDirectory(directory);
        return true;
    }

    public string GetWorkingDirectory(bool log = false)
    {
        var result = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;

        if (log)
            _logger.LogDebug("Retrieved working directory ({dir})", result);

        return result;
    }

    /// <summary>
    /// Retrieves a list of directories ordered by their levels.
    /// </summary>
    /// <param name="basePath">The base path to search for directories.</param>
    /// <returns>A list of directories ordered by their levels.</returns>
    [Pure]
    public static List<string> GetDirectoriesOrderedByLevels(string basePath)
    {
        var directories = System.IO.Directory.GetDirectories(basePath, "*", SearchOption.AllDirectories);

        var orderedDirectories = directories
            .OrderBy(dir => dir.Split(Path.DirectorySeparatorChar).Length);

        var result = orderedDirectories.ToList();
        return result;
    }

    /// <summary>
    /// Generates a new temporary directory path, but does not actually create the directory.
    /// </summary>
    /// <returns>The path of the new temporary directory.</returns>
    [Pure]
    public static string GetNewTempDirectoryPath()
    {
        var result = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        return result;
    }

    public string CreateTempDirectory()
    {
        var path = GetNewTempDirectoryPath();
        _ = CreateIfDoesNotExist(path);
        return path;
    }
}