using System.Threading;

namespace Soenneker.Utils.Directory.Dtos;

internal readonly record struct LogArgs(string Path, int IndentLevel, CancellationToken Token, DirectoryUtil Self);
