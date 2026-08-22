namespace Soenneker.Utils.Directory.Dtos;

internal struct DirectoryState
{
    public DirectoryState(string path, int parentIndex)
    {
        Path = path;
        ParentIndex = parentIndex;
    }

    public readonly string Path;
    public readonly int ParentIndex;
    public bool HasFiles;
    public bool HasRemainingChild;
}
