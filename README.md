[![](https://img.shields.io/nuget/v/Soenneker.Utils.Directory.svg?style=for-the-badge)](https://www.nuget.org/packages/Soenneker.Utils.Directory/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.utils.directory/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.utils.directory/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/Soenneker.Utils.Directory.svg?style=for-the-badge)](https://www.nuget.org/packages/Soenneker.Utils.Directory/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.utils.directory/codeql.yml?label=CodeQL&style=for-the-badge)](https://github.com/soenneker/soenneker.utils.directory/actions/workflows/codeql.yml)

# Soenneker.Utils.Directory

DI-friendly directory creation, enumeration, copying, moving, deletion, size calculation, and temporary-directory helpers.

## Installation

```bash
dotnet add package Soenneker.Utils.Directory
```

## Registration

```csharp
builder.Services.AddDirectoryUtilAsSingleton();
```

`AddDirectoryUtilAsScoped()` is also available. Both registrations include the matching `IPathUtil` lifetime.

## Common operations

```csharp
bool created = await directories.Create(outputPath, cancellationToken: cancellationToken);
bool exists = await directories.Exists(outputPath, cancellationToken);

List<string> children = await directories.GetAllDirectories(outputPath, cancellationToken);
List<string> descendants = await directories.GetAllDirectoriesRecursively(outputPath, cancellationToken);

await directories.CopyDirectory(sourcePath, destinationPath, overwrite: false, cancellationToken);
await directories.Move(sourcePath, archivePath, cancellationToken: cancellationToken);
```

`Create()` and `TryCreate()` return `false` when the path already exists. `CreateStrict()` throws in that case. `CopyDirectory(..., overwrite: false)` keeps existing destination files and continues copying other entries; it is not an all-or-nothing operation.

Copy and recursive size/empty-directory scans do not follow symbolic links, junctions, or other reparse points. This keeps traversal inside the requested directory tree. Reparse-point entries themselves are skipped by copy and size scans and prevent a directory from being considered empty.

## Destructive operations

```csharp
await directories.DeleteIfExists(path, cancellationToken);
await directories.DeleteEmptyDirectories(root, cancellationToken);
```

`Delete()` recursively removes the supplied directory and throws when it does not exist. `DeleteIfExists()` is idempotent. Resolve and validate paths at the application boundary before passing user-controlled values to either method.

Cancellation is cooperative and does not roll back filesystem changes that already completed. `MoveContentsUpOneLevelStrict()` can likewise leave earlier entries moved if a later conflict, I/O error, or cancellation occurs.

## Temporary directories and size

```csharp
string tempDirectory = await directories.CreateTempDirectory(cancellationToken);

long bytes = await directories.GetSizeInBytes(
    path,
    new GetSizeOptions
    {
        Recursive = true,
        ContinueOnError = false,
        Progress = progress
    },
    cancellationToken);
```

`CreateTempDirectory()` returns a unique path and creates it. The static `DirectoryUtil.GetNewTempDirectoryPath()` only generates a candidate path; it does not create anything.

Size calculation returns `0` for a missing directory. With `ContinueOnError = true`, inaccessible or failed subtrees are logged and omitted from the result; set it to `false` when a partial total is unacceptable.
