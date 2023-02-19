using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace Soenneker.Utils.Directory.Abstract;

public interface IDirectoryUtil
{
    [Pure]
    List<string> GetAllDirectories(string directory);

    [Pure]
    List<string> GetAllDirectoriesRecursively(string directory);

    void Delete(string directory);

    void DeleteIfExists(string directory);

    void CreateIfDoesNotExist(string directory);
}