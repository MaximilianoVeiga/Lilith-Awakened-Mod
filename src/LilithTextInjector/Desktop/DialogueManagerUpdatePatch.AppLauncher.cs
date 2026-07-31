namespace LilithTextInjector;

// Thin façade forwarding application/game launch commands to ApplicationLauncher.
internal static partial class DialogueManagerUpdatePatch
{
    internal static void EnsureApplicationLauncherFile() => ApplicationLauncher.EnsureFile();

    internal static void LogOfficialApplicationCategories() => ApplicationLauncher.LogOfficialCategories();

    private static bool TryLaunchApplicationCommand(string text, out string reply) => ApplicationLauncher.TryLaunch(text, out reply);

    private static ApplicationLauncher.ConfiguredApplication? FindConfiguredApplication(string text) => ApplicationLauncher.FindConfiguredApplication(text);

    private static ApplicationLauncher.ConfiguredApplication? FindOfficialApplication(string text) => ApplicationLauncher.FindOfficialApplication(text);

    private static ApplicationLauncher.WindowsStartApplication? ResolveWindowsStartApplication(string requestedName) => ApplicationLauncher.ResolveWindowsStartApplication(requestedName);

    private static bool TryLaunchWindowsStartApplication(ApplicationLauncher.WindowsStartApplication application) => ApplicationLauncher.TryLaunchWindowsStartApplication(application);

    private static string? ResolveWindowsShortcut(string[] names) => ApplicationLauncher.ResolveWindowsShortcut(names);

    private static string? ResolveFuzzyWindowsShortcut(string requestedName) => ApplicationLauncher.ResolveFuzzyWindowsShortcut(requestedName);

    private static bool TryFocusRunningApplication(string requestedName) => ApplicationLauncher.TryFocusRunningApplication(requestedName);
}
