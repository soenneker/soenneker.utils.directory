using Microsoft.Extensions.Logging;
using System.Threading;

namespace Soenneker.Utils.Directory.Utils;

internal readonly record struct GetSizeArgs(string Directory, GetSizeOptions? Options, CancellationToken CancellationToken, ILogger Logger);