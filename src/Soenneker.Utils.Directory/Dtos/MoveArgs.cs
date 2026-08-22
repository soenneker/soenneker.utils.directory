using System.Threading;

namespace Soenneker.Utils.Directory.Dtos;

internal readonly record struct MoveArgs(string TempDir, CancellationToken Token, DirectoryUtil Self);
