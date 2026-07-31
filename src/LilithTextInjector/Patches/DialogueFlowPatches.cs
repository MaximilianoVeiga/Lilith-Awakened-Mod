namespace LilithTextInjector;

// Native dialogue advance, typewriter state and completion hooks.
[HarmonyPatch(typeof(DialogueManager), "BeginDialogue")]
internal static class DialogueManagerBeginDialoguePatch
{
    private static void Prefix(DialogueNode node) => DialogueManagerUpdatePatch.RecordUnvoicedNativeNode(node);
}

[HarmonyPatch(typeof(DialogueManager), "ApplyAdvancedNode")]
internal static class DialogueManagerApplyAdvancedNodePatch
{
    private static void Prefix(DialogueNode node) => DialogueManagerUpdatePatch.RecordUnvoicedNativeNode(node);
}

[HarmonyPatch(typeof(DialogueManager), nameof(DialogueManager.AdvanceDialogue))]
internal static class DialogueManagerAdvancePatch
{
    private static bool Prefix(DialogueManager __instance)
    {
        return !DialogueManagerUpdatePatch.TryAdvanceAiPage(__instance);
    }
}

[HarmonyPatch(typeof(TypewriterEffect), "SetIsTyping")]
internal static class TypewriterStatePatch
{
    private static void Postfix(bool value)
    {
        DialogueManagerUpdatePatch.NotifyAiTypingState(value);
    }
}

[HarmonyPatch(typeof(DialogueManager), "CompleteCurrentNode")]
internal static class DialogueCompletionPatch
{
    private static bool Prefix(DialogueManager __instance)
    {
        return !DialogueManagerUpdatePatch.ShouldDelayAiCompletion(__instance);
    }
}
