using Xunit;
using AtlasDrop.Infrastructure.Logging;

namespace AtlasDrop.Tests.Logging;

public sealed class AtlasLogPathTests
{
    [Fact]
    public void Default_log_path_is_local_app_data()
    {
        var path = AtlasLogPath.GetDefaultDirectory();

        Assert.Contains(
            "AtlasDrop",
            path,
            StringComparison.OrdinalIgnoreCase);

        Assert.EndsWith(
            Path.Combine("AtlasDrop", "Logs"),
            path,
            StringComparison.OrdinalIgnoreCase);
    }
}
