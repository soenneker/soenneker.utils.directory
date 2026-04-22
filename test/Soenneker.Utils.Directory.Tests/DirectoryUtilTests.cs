using Soenneker.Utils.Directory.Abstract;
using Soenneker.Tests.HostedUnit;


namespace Soenneker.Utils.Directory.Tests;

[ClassDataSource<Host>(Shared = SharedType.PerTestSession)]
public class DirectoryUtilTests : HostedUnitTest
{
    private readonly IDirectoryUtil _util;

    public DirectoryUtilTests(Host host) : base(host)
    {
        _util = Resolve<IDirectoryUtil>(true);
    }

    [Test]
    public void Default()
    {

    }
}
