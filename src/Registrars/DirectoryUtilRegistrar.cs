using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Soenneker.Utils.Directory.Abstract;
using Soenneker.Utils.Path.Registrars;

namespace Soenneker.Utils.Directory.Registrars;

public static class DirectoryUtilRegistrar
{
    /// <summary>
    /// Adds IDirectoryUtil as a scoped service. <para/>
    /// Shorthand for <code>services.TryAddScoped</code> <para/>
    /// </summary>
    public static IServiceCollection AddDirectoryUtilAsScoped(this IServiceCollection services)
    {
        services.AddPathUtilAsScoped().TryAddScoped<IDirectoryUtil, DirectoryUtil>();
        return services;
    }

    /// <summary>
    /// Adds IDirectoryUtil as a singleton service. <para/>
    /// Shorthand for <code>services.TryAddSingleton</code> <para/>
    /// </summary>
    public static IServiceCollection AddDirectoryUtilAsSingleton(this IServiceCollection services)
    {
        services.AddPathUtilAsSingleton().TryAddSingleton<IDirectoryUtil, DirectoryUtil>();
        return services;
    }
}