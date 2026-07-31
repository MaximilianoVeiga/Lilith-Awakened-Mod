namespace LilithTextInjector;

// Thin façade over ChatMemory so existing patch call sites stay local.
internal static partial class DialogueManagerUpdatePatch
{
    internal static void LoadMemory() => ChatMemory.Load();

    private static void AddMemoryTurn(string role, string text) => ChatMemory.AddTurn(role, text);
}
