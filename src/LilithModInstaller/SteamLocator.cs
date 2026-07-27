namespace LilithModInstaller;

// Locates the game directory from a Steam install.
internal static class SteamLocator
{
    internal static string? FindGameDirectory()
    {
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (IsGameDirectory(AppContext.BaseDirectory)) return Path.GetFullPath(AppContext.BaseDirectory);
        foreach (var registry in new[]
                 {
                     (RegistryHive.CurrentUser, @"Software\Valve\Steam", "SteamPath"),
                     (RegistryHive.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath"),
                     (RegistryHive.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath")
                 })
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(registry.Item1, RegistryView.Default);
                using var key = baseKey.OpenSubKey(registry.Item2);
                if (key?.GetValue(registry.Item3) is string path && Directory.Exists(path)) candidates.Add(path);
            }
            catch { }
        }
        candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));

        foreach (var steam in candidates.ToArray())
        {
            var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { steam };
            var vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
            if (File.Exists(vdf))
            {
                foreach (Match match in Regex.Matches(File.ReadAllText(vdf), "\\\"path\\\"\\s+\\\"(?<path>[^\\\"]+)\\\"", RegexOptions.IgnoreCase))
                    libraries.Add(match.Groups["path"].Value.Replace("\\\\", "\\"));
            }
            foreach (var library in libraries)
            {
                var steamApps = Path.Combine(library, "steamapps");
                var manifest = Path.Combine(steamApps, "appmanifest_4643090.acf");
                if (File.Exists(manifest))
                {
                    var match = Regex.Match(File.ReadAllText(manifest), "\\\"installdir\\\"\\s+\\\"(?<dir>[^\\\"]+)\\\"", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        var game = Path.Combine(steamApps, "common", match.Groups["dir"].Value);
                        if (IsGameDirectory(game)) return Path.GetFullPath(game);
                    }
                }
                var fallback = Path.Combine(steamApps, "common", "The NOexistenceN of Lilith");
                if (IsGameDirectory(fallback)) return Path.GetFullPath(fallback);
            }
        }
        return null;
    }

    internal static bool IsGameDirectory(string? path)
        => !string.IsNullOrWhiteSpace(path)
           && File.Exists(Path.Combine(path, "Lilith.exe"))
           && File.Exists(Path.Combine(path, "GameAssembly.dll"))
           && Directory.Exists(Path.Combine(path, "Lilith_Data"));
}
