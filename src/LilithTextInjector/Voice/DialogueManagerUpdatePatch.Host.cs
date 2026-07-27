namespace LilithTextInjector;

// Thin façade over VoiceHostProcess for Update-tick glue.
internal static partial class DialogueManagerUpdatePatch
{
    private static void EnsureLocalVoiceHost() => VoiceHostProcess.EnsureRunning(IsJapaneseVoiceMode());
}
