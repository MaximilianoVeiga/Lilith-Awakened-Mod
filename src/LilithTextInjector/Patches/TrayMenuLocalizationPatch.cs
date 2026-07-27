namespace LilithTextInjector;

// Localises the injected tray menu entries.
[HarmonyPatch(typeof(ShowSystemTray), "GetFallbackMenuText")]
internal static class TrayMenuLocalizationPatch
{
    private static bool Prefix(string tableEntryKey, ref string __result)
    {
        if (string.Equals(tableEntryKey, "AddApiKey", StringComparison.Ordinal))
        {
            __result = DialogueManagerUpdatePatch.LocalizedText("加入 API KEY", "加入 API KEY", "APIキーを追加", "Add API Key");
            return false;
        }
        if (tableEntryKey is "Gemini" or "Qwen" or "OpenAI" or "DeepSeek")
        {
            __result = tableEntryKey == "Qwen"
                ? DialogueManagerUpdatePatch.LocalizedText("千問", "千问", "Qwen（千問）", "Qwen")
                : tableEntryKey;
            return false;
        }
        return true;
    }
}
