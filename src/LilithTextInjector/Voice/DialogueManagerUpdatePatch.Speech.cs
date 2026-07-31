namespace LilithTextInjector;

// Thin façade over SpeechSynth for call sites throughout the patch.
internal static partial class DialogueManagerUpdatePatch
{
    private static Task RequestSpeechAsync(string text, SpeechSynth.NativeReaction? reaction = null, VoiceStyle poseStyle = VoiceStyle.Calm, bool? japaneseVoiceMode = null)
        => SpeechSynth.RequestAsync(text, reaction, poseStyle, japaneseVoiceMode);

    private static SpeechSynth.NativeReaction? GetNativeReaction(string userText, string reply) => SpeechSynth.GetNativeReaction(userText, reply);

    private static string RemoveLeadingReactionText(string text) => SpeechSynth.RemoveLeadingReactionText(text);
}
