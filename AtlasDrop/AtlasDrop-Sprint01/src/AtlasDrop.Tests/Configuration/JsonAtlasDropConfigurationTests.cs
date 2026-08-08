using Xunit;
using AtlasDrop.Infrastructure.Configuration;

namespace AtlasDrop.Tests.Configuration;

public sealed class JsonAtlasDropConfigurationTests
{
    [Fact]
    public void Loads_valid_external_configuration()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "AtlasDrop.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var configPath = Path.Combine(tempRoot, "appsettings.json");
            File.WriteAllText(configPath, """
            {
              "OneDriveRoot": "C:\\Users\\fchelli\\OneDrive - ALTEDIS",
              "MaxSuggestedDepth": 4,
              "ExcludedFolderNames": ["Bureau", "99 - Archives"]
            }
            """);

            var loader = new JsonAtlasDropConfiguration();
            var options = loader.Load(configPath);

            Assert.Equal(4, options.MaxSuggestedDepth);
            Assert.Equal(@"C:\Users\fchelli\OneDrive - ALTEDIS", options.OneDriveRoot);
            Assert.Contains("99 - Archives", options.ExcludedFolderNames);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Missing_configuration_file_is_rejected()
    {
        var loader = new JsonAtlasDropConfiguration();
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.json");

        Assert.Throws<FileNotFoundException>(() => loader.Load(missing));
    }

    [Fact]
    public void Invalid_depth_in_json_is_rejected()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "AtlasDrop.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var configPath = Path.Combine(tempRoot, "appsettings.json");
            File.WriteAllText(configPath, """
            {
              "OneDriveRoot": "C:\\Users\\fchelli\\OneDrive - ALTEDIS",
              "MaxSuggestedDepth": 99,
              "ExcludedFolderNames": []
            }
            """);

            var loader = new JsonAtlasDropConfiguration();

            Assert.Throws<InvalidOperationException>(() => loader.Load(configPath));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }
}
