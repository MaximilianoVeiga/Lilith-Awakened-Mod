namespace LilithTextInjector;

// Thin façade over NativeVoicePack for Harmony patches and Update-tick glue.
internal static partial class DialogueManagerUpdatePatch
{
    private static void ObserveCurrentNativeNode(DialogueManager manager) => NativeVoicePack.ObserveCurrentNode(manager);

    internal static void RecordUnvoicedNativeNode(DialogueNode node) => NativeVoicePack.RecordUnvoiced(node);

    internal static bool TryPlayInjectedNativeVoice(DialogueNode node) => NativeVoicePack.TryPlayInjectedVoice(node);

    internal static float ExtendNativeNodeDuration(DialogueNode node, float original) => NativeVoicePack.ExtendDuration(node, original);

    private static void EnsureNativeVoiceManifestLoaded() => NativeVoicePack.EnsureManifestLoaded();
}
