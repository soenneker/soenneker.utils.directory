using System;

namespace Soenneker.Utils.Directory.Utils;

public record GetSizeOptions
{
    /// <summary>
    /// If true, includes all subdirectories in the calculation. Defaults to true.
    /// </summary>
    public bool Recursive { get; init; } = true;

    /// <summary>
    /// If true, skips directories that cannot be accessed (e.g., due to permissions) and logs a warning.
    /// If false, an exception will be thrown and the operation will terminate. Defaults to true.
    /// </summary>
    public bool ContinueOnError { get; init; } = true;

    /// <summary>
    /// An optional progress reporter that receives the running total of bytes calculated so far.
    /// This can be used to update a UI with the progress of the operation.
    /// </summary>
    public IProgress<long>? Progress { get; init; }
}