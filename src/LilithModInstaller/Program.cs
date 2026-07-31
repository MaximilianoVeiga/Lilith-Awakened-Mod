namespace LilithModInstaller;

// Installer entry point.
internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Contains("--voice-host", StringComparer.OrdinalIgnoreCase))
        {
            var parent = 0;
            var index = Array.FindIndex(args, value => string.Equals(value, "--parent", StringComparison.OrdinalIgnoreCase));
            if (index >= 0 && index + 1 < args.Length && int.TryParse(args[index + 1], out var parsed))
                parent = parsed;
            VoiceHost.RunAsync(parent).GetAwaiter().GetResult();
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new InstallerForm());
    }
}
