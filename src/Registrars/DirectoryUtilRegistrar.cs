using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Soenneker.Utils.Directory.Abstract;

namespace Soenneker.Utils.Directory.Registrars;

public static class DirectoryUtilRegistrar
{
    /// <summary>
    /// Adds IDirectoryUtil as a scoped service. <para/>
    /// Shorthand for <code>services.TryAddScoped</code> <para/>
    /// </summary>
    public static void AddDirectoryUtilAsScoped(this IServiceCollection services)
    {
        services.TryAddScoped<IDirectoryUtil, DirectoryUtil>();
    }

    /// <summary>
    /// Adds IDirectoryUtil as a singleton service. <para/>
    /// Shorthand for <code>services.TryAddSingleton</code> <para/>
    /// </summary>
    public static void AddDirectoryUtilAsSingleton(this IServiceCollection services)
    {
        services.TryAddSingleton<IDirectoryUtil, DirectoryUtil>();
    }
}