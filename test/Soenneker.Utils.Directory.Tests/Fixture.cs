using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Soenneker.Fixtures.Unit;
using Soenneker.Utils.Directory.Registrars;
using Soenneker.Utils.Test;

namespace Soenneker.Utils.Directory.Tests;

public class Fixture : UnitFixture
{
    public override System.Threading.Tasks.ValueTask InitializeAsync()
    {
        SetupIoC(Services);

        return base.InitializeAsync();
    }

    private static void SetupIoC(IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.AddSerilog(dispose: true);
        });

        var config = TestUtil.BuildConfig();
        services.AddSingleton(config);

        services.AddDirectoryUtilAsScoped();
    }
}
