using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Soenneker.TestHosts.Unit;
using Soenneker.Utils.Directory.Registrars;
using Soenneker.Utils.Test;

namespace Soenneker.Utils.Directory.Tests;

public class Host : UnitTestHost
{
    public override Task InitializeAsync()
    {
        SetupIoC(Services);

        return base.InitializeAsync();
    }

    private static void SetupIoC(IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.AddSerilog(dispose: false);
        });

        var config = TestUtil.BuildConfig();
        services.AddSingleton(config);

        services.AddDirectoryUtilAsScoped();
    }
}
