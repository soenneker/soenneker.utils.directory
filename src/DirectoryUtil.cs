using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Soenneker.Utils.Directory.Abstract;
using System.Reflection;

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

    public List<string> GetAllDirectoriesRecursively(string directory)
    {
        return System.IO.Directory.EnumerateDirectories(directory, "*", SearchOption.AllDirectories).ToList();
    }

    public void Delete(string directory)
    {
        _logger.LogDebug("Deleting directory: {dir}...", directory);

        System.IO.Directory.Delete(directory, true);
    }

    public void DeleteIfExists(string directory)
    {
        _logger.LogDebug("Deleting directory if it exists: {dir} ...", directory);

        if (System.IO.Directory.Exists(directory))
            System.IO.Directory.Delete(directory, true);
    }

    public void CreateIfDoesNotExist(string directory)
    {
        _logger.LogDebug("Creating directory {dir} if it doesn't exist...", directory);

        if (!System.IO.Directory.Exists(directory))
            System.IO.Directory.CreateDirectory(directory);
    }

    public string GetWorkingDirectory()
    {
       return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
    }
}