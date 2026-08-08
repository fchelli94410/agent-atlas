namespace AtlasDrop.Installer;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length >= 1 &&
            string.Equals(
                args[0],
                "--self-test",
                StringComparison.OrdinalIgnoreCase))
        {
            var payload = args.Length >= 2
                ? args[1]
                : Path.Combine(
                    AppContext.BaseDirectory,
                    "app");

            return InstallerEngine.SelfTestPayload(payload)
                ? 0
                : 2;
        }

        var engine = new InstallerEngine();

        InstallResult result;

        if (args.Length >= 1 &&
            string.Equals(
                args[0],
                "--uninstall",
                StringComparison.OrdinalIgnoreCase))
        {
            result = engine.Uninstall();
        }
        else
        {
            var payload = args.Length >= 2 &&
                          string.Equals(
                              args[0],
                              "--payload",
                              StringComparison.OrdinalIgnoreCase)
                ? args[1]
                : Path.Combine(
                    AppContext.BaseDirectory,
                    "app");

            result = engine.Install(payload);
        }

        Console.WriteLine(result.Message);

        return result.Success ? 0 : 1;
    }
}
