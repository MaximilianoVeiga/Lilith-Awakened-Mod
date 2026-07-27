namespace LilithTextInjector;

// Shared on-disk data roots for the injector. Centralised so feature types
// do not depend on DialogueManagerUpdatePatch static field initialisers.
internal static class ModDataPaths
{
    internal static readonly string Directory = Path.Combine(Paths.BepInExRootPath, "data", "LilithTextInjector");
    internal static readonly string MemoryPath = Path.Combine(Directory, "memory.json");
    internal static readonly string AiNoteStatePath = Path.Combine(Directory, "ai-note-state.json");
    internal static readonly string ApplicationLauncherPath = Path.Combine(Directory, "applications.json");
    internal static readonly string UnvoicedManifestPath = Path.Combine(Directory, "unvoiced-native-lines.tsv");
}
