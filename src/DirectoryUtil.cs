using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Soenneker.Utils.Directory.Abstract;
using System.Reflection;
using System;
using System.Diagnostics.Contracts;
using Soenneker.Utils.Path.Abstract;
using System.Threading.Tasks;
using System.Threading;
using Soenneker.Extensions.Task;
using Soenneker.Extensions.ValueTask;

namespace Soenneker.Utils.Directory;

///<inheritdoc cref="IDirectoryUtil"/>
public sealed class DirectoryUtil : IDirectoryUtil
{
    private readonly IPathUtil _pathUtil;
    private readonly ILogger<DirectoryUtil> _logger;

    public DirectoryUtil(IPathUtil pathUtil, ILogger<DirectoryUtil> logger)
    {
        _pathUtil = pathUtil;
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
        var result = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;

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

        var orderedDirectories = directories.OrderBy(dir => dir.Split(System.IO.Path.DirectorySeparatorChar).Length);

        return orderedDirectories.ToList();
    }

    /// <summary>
    /// Generates a new temporary directory path, but does not actually create the directory.
    /// </summary>
    /// <returns>The path of the new temporary directory.</returns>
    [Pure]
    public static string GetNewTempDirectoryPath()
    {
        return System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString());
    }

    public ValueTask<string> CreateTempDirectory(CancellationToken cancellationToken = default)
    {
        return _pathUtil.GetUniqueTempDirectory(null, true, cancellationToken);
    }

    public bool Exists(string directory)
    {
        return System.IO.Directory.Exists(directory);
    }

    public List<string> GetEmptyDirectories(string root)
    {
        return System.IO.Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
                     .Where(d => !System.IO.Directory.EnumerateFileSystemEntries(d).Any())
                     .ToList();
    }

    public void DeleteEmptyDirectories(string root)
    {
        foreach (var dir in GetEmptyDirectories(root))
        {
            _logger.LogDebug("Deleting empty directory: {dir}", dir);
            System.IO.Directory.Delete(dir);
        }
    }

    public List<string> GetDirectoriesContainingFile(string root, string fileName)
    {
        return System.IO.Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
                     .Where(d => File.Exists(System.IO.Path.Combine(d, fileName)))
                     .ToList();
    }

    public List<string> GetFilesByExtension(string directory, string extension, bool recursive = false)
    {
        return System.IO.Directory.EnumerateFiles(directory, $"*.{extension.TrimStart('.')}",
                         recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                     .ToList();
    }

    public async ValueTask CopyDirectory(string sourceDir, string destDir, bool overwrite = true, CancellationToken cancellationToken = default)
    {
        if (!System.IO.Directory.Exists(sourceDir))
            throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");

        System.IO.Directory.CreateDirectory(destDir);

        foreach (var file in System.IO.Directory.GetFiles(sourceDir))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var destFile = System.IO.Path.Combine(destDir, System.IO.Path.GetFileName(file));

            await using var sourceStream = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            await using var destinationStream = File.Create(destFile);
            await sourceStream.CopyToAsync(destinationStream, 81920, cancellationToken);
        }

        foreach (var subdir in System.IO.Directory.GetDirectories(sourceDir))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var destSubdir = System.IO.Path.Combine(destDir, System.IO.Path.GetFileName(subdir));
            await CopyDirectory(subdir, destSubdir, overwrite, cancellationToken).NoSync();
        }
    }

    public static string Normalize(string directory)
    {
        return System.IO.Path.GetFullPath(new Uri(directory).LocalPath).TrimEnd(System.IO.Path.DirectorySeparatorChar);
    }

    public void LogContentsRecursively(string path, int indentLevel = 0)
    {
        if (!System.IO.Directory.Exists(path))
        {
            _logger.LogWarning("Directory does not exist: {Path}", path);
            return;
        }

        try
        {
            var indent = new string(' ', indentLevel * 2);

            // Log current directory
            _logger.LogInformation("{Indent}📁 {Directory}", indent, System.IO.Path.GetFileName(path));

            // Log files in the directory
            var files = System.IO.Directory.GetFiles(path);
            foreach (var file in files)
            {
                _logger.LogInformation("{Indent}  📄 {File}", indent, System.IO.Path.GetFileName(file));
            }

            // Recurse into subdirectories
            var subdirectories = System.IO.Directory.GetDirectories(path);
            foreach (var subdir in subdirectories)
            {
                LogContentsRecursively(subdir, indentLevel + 1);
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied to {Path}", path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading directory {Path}", path);
        }
    }

    public async ValueTask<long> GetSizeInBytes(string directory, GetSizeOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (!System.IO.Directory.Exists(directory))
        {
            return 0;
        }

        // Offload the entire synchronous-by-nature file I/O to a background thread
        // to keep the calling thread (e.g., UI thread) from blocking.
        return await Task.Run(() =>
        {
            var opts = options ?? new GetSizeOptions();
            long totalSize = 0;

            var rootDirectoryInfo = new DirectoryInfo(directory);
            var directoriesToScan = new Stack<DirectoryInfo>();
            directoriesToScan.Push(rootDirectoryInfo);

            while (directoriesToScan.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var currentDir = directoriesToScan.Pop();

                try
                {
                    foreach (var file in currentDir.EnumerateFiles())
                    {
                        totalSize += file.Length;
                    }

                    opts.Progress?.Report(totalSize);

                    if (opts.Recursive)
                    {
                        foreach (var subDir in currentDir.EnumerateDirectories())
                        {
                            directoriesToScan.Push(subDir);
                        }
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    _logger.LogWarning(ex, "Access denied to directory {DirectoryPath}, skipping.", currentDir.FullName);
                    if (!opts.ContinueOnError)
                    {
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while scanning directory {DirectoryPath}, skipping.", currentDir.FullName);
                    if (!opts.ContinueOnError)
                    {
                        throw;
                    }
                }
            }

            return totalSize;
        }, cancellationToken).NoSync();
    }

    public void MoveContentsUpOneLevelStrict(string tempDir)
    {
        if (!System.IO.Directory.Exists(tempDir))
            throw new DirectoryNotFoundException($"The directory '{tempDir}' does not exist.");

        var rootFiles = System.IO.Directory.GetFiles(tempDir);
        if (rootFiles.Length > 0)
            throw new InvalidOperationException("Top-level directory contains files. Expected only one subdirectory.");

        var rootDirs = System.IO.Directory.GetDirectories(tempDir);
        if (rootDirs.Length != 1)
            throw new InvalidOperationException($"Expected exactly one subdirectory in temp dir, found {rootDirs.Length}.");

        var innerDir = rootDirs[0];
        _logger.LogInformation("Moving contents from inner directory '{inner}' up to '{temp}'", innerDir, tempDir);

        foreach (var dir in System.IO.Directory.GetDirectories(innerDir))
        {
            var destDir = System.IO.Path.Combine(tempDir, System.IO.Path.GetFileName(dir));

            if (System.IO.Directory.Exists(destDir))
                throw new IOException($"Destination directory already exists: {destDir}");

            System.IO.Directory.Move(dir, destDir);
            _logger.LogDebug("Moved directory: {src} -> {dest}", dir, destDir);
        }

        foreach (var file in System.IO.Directory.GetFiles(innerDir))
        {
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