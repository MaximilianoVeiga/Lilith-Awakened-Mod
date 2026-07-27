namespace LilithTextInjector;

// Observes native voice-language button presses.
[HarmonyPatch(typeof(ButtonPressedSwapSprite), nameof(ButtonPressedSwapSprite.OnPointerClick))]
internal static class VoiceSettingsButtonClickPatch
{
    private static void Postfix(ButtonPressedSwapSprite __instance)
    {
        DialogueManagerUpdatePatch.NotifyVoiceSettingsButtonClicked(__instance);
    }
}
