namespace LilithTextInjector;

// Thin façade over VoiceInputService for Update-tick glue.
internal static partial class DialogueManagerUpdatePatch
{
    private static void HandleVoiceInput(DialogueManager manager) => VoiceInputService.HandleInput(manager);
}
