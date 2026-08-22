using Microsoft.Extensions.Logging;
using System.Threading;

namespace Soenneker.Utils.Directory.Dtos;

internal readonly record struct GetSizeArgs(string Directory, GetSizeOptions? Options, CancellationToken CancellationToken, ILogger Logger);
