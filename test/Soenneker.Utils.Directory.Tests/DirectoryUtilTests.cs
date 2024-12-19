using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Soenneker.Utils.Directory.Abstract;
using Soenneker.Tests.FixturedUnit;
using Xunit;


namespace Soenneker.Utils.Directory.Tests;

[Collection("Collection")]
public class DirectoryUtilTests : FixturedUnitTest
{
    private readonly IDirectoryUtil _util;

    public DirectoryUtilTests(Fixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
        _util = Resolve<IDirectoryUtil>(true);
    }

    [Fact]
    public void Default()
    {

    }
}
