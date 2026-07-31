namespace LilithTextInjector;

// Redirects the native gift-exchange dialog to API key entry.
[HarmonyPatch(typeof(GiftExchangeView), "OnRedeemButtonClicked")]
internal static class GiftExchangeApiKeyPatch
{
    private static bool Prefix(GiftExchangeView __instance)
    {
        return !DialogueManagerUpdatePatch.TrySaveApiKey(__instance);
    }
}

[HarmonyPatch(typeof(GiftExchangeView), "Hide")]
internal static class GiftExchangeApiKeyHidePatch
{
    private static void Postfix(GiftExchangeView __instance)
    {
        DialogueManagerUpdatePatch.NotifyGiftExchangeViewHidden(__instance);
    }
}
