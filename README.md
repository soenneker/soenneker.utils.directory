[![](https://img.shields.io/nuget/v/Soenneker.Utils.Directory.svg?style=for-the-badge)](https://www.nuget.org/packages/Soenneker.Utils.Directory/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.utils.directory/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.utils.directory/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/Soenneker.Utils.Directory.svg?style=for-the-badge)](https://www.nuget.org/packages/Soenneker.Utils.Directory/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.utils.directory/codeql.yml?label=CodeQL&style=for-the-badge)](https://github.com/soenneker/soenneker.utils.directory/actions/workflows/codeql.yml)

# ![](https://user-images.githubusercontent.com/4441470/224455560-91ed3ee7-f510-4041-a8d2-3fc093025112.png) Soenneker.Utils.Directory
A utility library encapsulating various directory methods.

## Installation

```bash
dotnet add package Soenneker.Utils.Directory
```

## Quick start

```csharp
using Soenneker.Utils.Directory.Registrars;

services.AddDirectoryUtilAsSingleton();
```

Then inject `IDirectoryUtil` wherever you need it.

## Common operations

- `GetAllDirectories()` - Retrieves all immediate subdirectories in the specified directory.
- `GetAllAsEnumerable()` - Retrieves all immediate subdirectories as a list.
- `GetAllDirectoriesRecursively()` - Retrieves all subdirectories recursively from the specified directory.
- `GetAllRecursivelyAsEnumerable()` - Retrieves all subdirectories recursively as a list.
- `Delete()` - Deletes the specified directory and all its contents.
- `DeleteIfExists()` - Deletes the directory if it exists.
- `Create()` - Creates the directory if it does not exist. Returns true if the directory was created, false if it already existed.
- `TryCreate()` - Attempts to create the specified directory.
- `CreateStrict()` - Creates the specified directory and throws if it already exists.
- `GetWorkingDirectory()` - Gets the working directory of the currently executing assembly.
- `GetDirectoriesOrderedByLevels()` - Retrieves a list of directories ordered by their levels. Avoids Split() allocations by counting separators.
- `GetNewTempDirectoryPath()` - Generates a new temporary directory path, but does not actually create the directory.

The package also includes 12 additional operations for more specialized cases.
