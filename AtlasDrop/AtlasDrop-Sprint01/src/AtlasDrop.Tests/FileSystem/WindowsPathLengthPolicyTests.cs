using Xunit;
using AtlasDrop.Infrastructure.FileSystem;

namespace AtlasDrop.Tests.FileSystem;

public sealed class WindowsPathLengthPolicyTests
{
    private static WindowsPathLengthPolicy Policy() => new();

    [Fact]
    public void Normal_path_is_safe()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "AtlasDrop",
            "facture.pdf");

        var result = Policy().Check(path);

        Assert.True(result.IsSafe);
        Assert.True(result.Length < result.Limit);
    }

    [Fact]
    public void Empty_path_is_rejected()
    {
        var result = Policy().Check("   ");

        Assert.False(result.IsSafe);
    }

    [Fact]
    public void Path_at_or_above_limit_is_rejected()
    {
        var root = Path.GetTempPath();
        var segment = new string('A', 200);
        var path = Path.Combine(
            root,
            segment,
            segment,
            "file.pdf");

        var result = Policy().Check(
            path,
            safeLimit: 120);

        Assert.False(result.IsSafe);
        Assert.True(result.Length >= 120);
    }

    [Fact]
    public void Destination_combines_directory_and_file_name()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "AtlasDrop",
            "Clients");

        var result = Policy().CheckDestination(
            directory,
            "2026 - Facture - EDF.pdf");

        Assert.True(result.IsSafe);
        Assert.EndsWith(
            "2026 - Facture - EDF.pdf",
            result.NormalizedPath,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Destination_uses_only_file_name_component()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "AtlasDrop");

        var result = Policy().CheckDestination(
            directory,
            @"C:\Other\facture.pdf");

        Assert.True(result.IsSafe);
        Assert.EndsWith(
            "facture.pdf",
            result.NormalizedPath,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Invalid_safe_limit_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Policy().Check(
                @"C:\Temp\a.txt",
                safeLimit: 10));
    }

    [Fact]
    public void Long_destination_is_rejected_before_file_operation()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            new string('B', 100));

        var fileName = new string('C', 100) + ".pdf";

        var result = Policy().CheckDestination(
            directory,
            fileName,
            safeLimit: 150);

        Assert.False(result.IsSafe);
    }
}
