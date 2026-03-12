using System.Threading;

namespace Soenneker.Utils.Directory.Utils;

internal readonly record struct MoveArgs(string TempDir, CancellationToken Token, DirectoryUtil Self);