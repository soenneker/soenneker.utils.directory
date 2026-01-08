using System.Threading;

namespace Soenneker.Utils.Directory.Utils;

internal readonly record struct LogArgs(string Path, int IndentLevel, CancellationToken Token, DirectoryUtil Self);