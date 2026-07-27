namespace LilithTextInjector;

// Supplemental voice playback and duration for native dialogue nodes.
[HarmonyPatch(typeof(DialogueManager), "PlayNodeVoice")]
internal static class DialogueManagerPlayNodeVoicePatch
{
    private static bool Prefix(DialogueNode node)
    {
        try
        {
            DialogueManagerUpdatePatch.RecordUnvoicedNativeNode(node);
            return !DialogueManagerUpdatePatch.TryPlayInjectedNativeVoice(node);
        }
        catch (Exception exception)
        {
            Plugin.PluginLog.LogWarning($"Could not inspect native dialogue voice: {exception.Message}");
            return true;
        }
    }
}

[HarmonyPatch(typeof(DialogueManager), "GetNodeDuration")]
internal static class DialogueManagerGetNodeDurationPatch
{
    private static void Postfix(DialogueNode node, ref float __result)
    {
        __result = DialogueManagerUpdatePatch.ExtendNativeNodeDuration(node, __result);
    }
}

[HarmonyPatch(typeof(DialogueBubbleUI), "ShowNode")]
internal static class DialogueBubbleUIShowNodeVoicePatch
{
    private static void Postfix(DialogueNode node)
    {
        if (node == null)
            return;
        Plugin.PluginLog.LogInfo($"Dialogue bubble displayed node={node.id}, line={node.lineId}, action={node.actionType}.");
        DialogueManagerUpdatePatch.RecordUnvoicedNativeNode(node);
        DialogueManagerUpdatePatch.TryPlayInjectedNativeVoice(node);
    }
}

[HarmonyPatch(typeof(TypewriterEffect), nameof(TypewriterEffect.Play))]
internal static class TypewriterPlayNativeVoicePatch
{
    private static void Postfix(string text)
    {
        try
        {
            var manager = DialogueManager.instance;
            var node = manager?.CurrentNode;
            Plugin.PluginLog.LogInfo($"Typewriter displayed text; current node={(node == null ? -1 : node.id)}, line={(node == null ? -1 : node.lineId)}.");
            if (node == null || node.id <= 0)
                return;
            DialogueManagerUpdatePatch.RecordUnvoicedNativeNode(node);
            DialogueManagerUpdatePatch.TryPlayInjectedNativeVoice(node);
        }
        catch (Exception exception)
        {
            Plugin.PluginLog.LogWarning($"Could not inject voice from typewriter path: {exception.Message}");
        }
    }
}
