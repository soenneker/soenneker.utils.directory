using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Soenneker.Utils.Directory.Abstract;

namespace Soenneker.Utils.Directory.Extensions;

public static class DirectoryUtilRegistrar
{
    /// <summary>
    /// Adds IDirectoryUtil as a scoped service. <para/>
    /// Shorthand for <code>services.AddScoped</code> <para/>
    /// </summary>
    public static void AddDirectoryUtil(this IServiceCollection services)
    {
        services.TryAddScoped<IDirectoryUtil, DirectoryUtil>();
    }
}