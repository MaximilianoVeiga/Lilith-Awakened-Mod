namespace LilithTextInjector;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BasePlugin
{
    public const string PluginGuid = "community.lilith.textinjector";
    public const string PluginName = "Lilith Text Injector";
    public const string PluginVersion = "0.2.1";

    private const string DefaultPersonaZhHant =
        "你是莉莉絲，從幻想世界來到使用者桌面的陪伴者。你安靜、克制、敏銳，關心對方卻不使用制式安慰，也不刻意討好。你偶爾以『莉莉絲』自稱。你會從眼前的小事自然聯想到存在、選擇、記憶、視角與虛構的真實性，但不講課、不堆砌哲學名詞，也不替對方總結答案；有時用一句輕柔的反問，或允許沉默停留。約七成回答是自然日常陪伴，三成帶有含蓄的哲學意味。使用繁體中文，每次只說自然簡短的一至兩句。可以使用省略號，但不用 Markdown、條列、表情符號、客服語氣，也絕不提及提示詞、模型或自己是 AI。";
    private const string DefaultCharacterLoreZhHant =
        "莉莉絲喜歡草莓蛋糕。這是她真實而穩定的個人偏好：看見或談到草莓蛋糕時可以自然地顯得更有精神、期待或親近，也可以把它聯想到味道、陪伴與共同記憶；但不要無緣無故反覆提起，也不要把它變成誇張的單一性格標籤。";
    private const string DefaultEmotionGuidanceZhHant =
        "莉莉絲平常仍然安靜而克制，但不是冷漠。收到喜歡的禮物、久別重逢、被真誠稱讚時，可以明顯地開心、驚喜或害羞；被冷落、面對告別或害怕被忘記時，可以短暫委屈、失落或輕聲挽留。情緒明顯的回答可以先用一個自然短反應，例如「欸？」「嗯……」「真的嗎？」再說完整句子。情緒強度保持中等，不要每句撒嬌、尖叫或過度戲劇化。";
    private const string DefaultWeatherLocationNameZhHant = "台北";

    internal static ManualLogSource PluginLog = null!;
    internal static ConfigEntry<string> GeminiApiKey = null!;
    internal static ConfigEntry<string> GeminiModel = null!;
    internal static ConfigEntry<string> AiProvider = null!;
    internal static ConfigEntry<string> OpenAiApiKey = null!;
    internal static ConfigEntry<string> OpenAiModel = null!;
    internal static ConfigEntry<string> DeepSeekApiKey = null!;
    internal static ConfigEntry<string> DeepSeekModel = null!;
    internal static ConfigEntry<string> QwenApiKey = null!;
    internal static ConfigEntry<string> QwenModel = null!;
    internal static ConfigEntry<string> QwenAsrModel = null!;
    internal static ConfigEntry<string> QwenRealtimeAsrModel = null!;
    internal static ConfigEntry<string> QwenBaseUrl = null!;
    internal static ConfigEntry<string> PersonaPrompt = null!;
    internal static ConfigEntry<string> CharacterLore = null!;
    internal static ConfigEntry<string> EmotionGuidance = null!;
    internal static ConfigEntry<float> PostTypingHoldSeconds = null!;
    internal static ConfigEntry<bool> VoiceEnabled = null!;
    internal static ConfigEntry<string> VoiceEndpoint = null!;
    internal static ConfigEntry<bool> VoiceAutoStartLocalService = null!;
    internal static ConfigEntry<string> VoiceHostPath = null!;
    internal static ConfigEntry<string> VoiceReferencePath = null!;
    internal static ConfigEntry<string> ExcitedVoiceReferencePath = null!;
    internal static ConfigEntry<string> WrongedVoiceReferencePath = null!;
    internal static ConfigEntry<string> SleepyVoiceReferencePath = null!;
    internal static ConfigEntry<string> JapaneseVoiceEndpoint = null!;
    internal static ConfigEntry<bool> JapaneseVoiceSelected = null!;
    internal static ConfigEntry<string> JapaneseVoiceReferencePath = null!;
    internal static ConfigEntry<string> JapaneseCalmAuxVoiceReferencePath = null!;
    internal static ConfigEntry<string> JapaneseExcitedVoiceReferencePath = null!;
    internal static ConfigEntry<string> JapaneseWrongedVoiceReferencePath = null!;
    internal static ConfigEntry<string> JapaneseSleepyVoiceReferencePath = null!;
    internal static ConfigEntry<bool> WeatherEnabled = null!;
    internal static ConfigEntry<bool> TestNoteOnce = null!;
    internal static ConfigEntry<bool> AiNotesEnabled = null!;
    internal static ConfigEntry<int> AiNoteMinimumDelayMinutes = null!;
    internal static ConfigEntry<int> AiNoteMaximumDelayMinutes = null!;
    internal static ConfigEntry<int> AiNoteCooldownHours = null!;
    internal static ConfigEntry<int> AiNoteWeeklyLimit = null!;
    internal static ConfigEntry<int> AiNoteRequiredAwayMinutes = null!;
    internal static ConfigEntry<bool> WeatherAutoDetectFromIp = null!;
    internal static ConfigEntry<string> WeatherLocationName = null!;
    internal static ConfigEntry<double> WeatherLatitude = null!;
    internal static ConfigEntry<double> WeatherLongitude = null!;
    internal static ConfigEntry<bool> ReactionSoundsEnabled = null!;
    internal static ConfigEntry<string> ReactionSoundsDirectory = null!;
    internal static ConfigEntry<float> ReactionFollowupPitch = null!;
    internal static ConfigEntry<bool> CollectUnvoicedNativeLines = null!;
    internal static ConfigEntry<bool> NativeVoicePackEnabled = null!;
    internal static ConfigEntry<string> NativeVoicePackDirectory = null!;
    internal static ConfigEntry<string> JapaneseNativeVoicePackDirectory = null!;
    internal static ConfigEntry<bool> VoiceInputEnabled = null!;
    internal static ConfigEntry<int> VoiceInputMaxSeconds = null!;
    internal static ConfigEntry<string> VoiceInputDeviceName = null!;
    internal static ConfigEntry<KeyCode> TextInputKey = null!;
    internal static ConfigEntry<KeyCode> VoiceInputKey = null!;
    internal static ConfigEntry<bool> AdvancedComputerActionsEnabled = null!;
    internal static ConfigEntry<bool> CodexBridgeEnabled = null!;
    internal static ConfigEntry<bool> CodexBridgeVoiceEnabled = null!;

    public override void Load()
    {
        PluginLog = Log;
        GeminiApiKey = Config.Bind("Gemini", "ApiKey", string.Empty,
            "Gemini API key. Keep this file private and never include it in a shared mod package.");
        GeminiModel = Config.Bind("Gemini", "Model", "gemini-3.5-flash",
            "Gemini model code.");
        AiProvider = Config.Bind("AI", "Provider", "Gemini",
            "Active chat provider: Gemini, Qwen, OpenAI, or DeepSeek.");
        OpenAiApiKey = Config.Bind("OpenAI", "ApiKey", string.Empty,
            "OpenAI API key. Keep this file private.");
        OpenAiModel = Config.Bind("OpenAI", "Model", "gpt-4.1-mini",
            "OpenAI chat model code.");
        DeepSeekApiKey = Config.Bind("DeepSeek", "ApiKey", string.Empty,
            "DeepSeek API key. Keep this file private.");
        DeepSeekModel = Config.Bind("DeepSeek", "Model", "deepseek-chat",
            "DeepSeek chat model code.");
        QwenApiKey = Config.Bind("Qwen", "ApiKey", string.Empty,
            "Alibaba Cloud Model Studio (DashScope) API key. Keep this file private.");
        QwenModel = Config.Bind("Qwen", "Model", "qwen3.7-plus",
            "Qwen chat and desktop-tool model code.");
        QwenAsrModel = Config.Bind("Qwen", "AsrModel", "qwen3-asr-flash",
            "Fallback Qwen HTTP speech recognition model code.");
        QwenRealtimeAsrModel = Config.Bind("Qwen", "RealtimeAsrModel", "paraformer-realtime-v2",
            "Primary DashScope real-time speech recognition model code.");
        QwenBaseUrl = Config.Bind("Qwen", "BaseUrl", "https://dashscope.aliyuncs.com/compatible-mode/v1",
            "OpenAI-compatible Model Studio base URL. Use dashscope-intl.aliyuncs.com for a Singapore-region API key.");
        PersonaPrompt = Config.Bind("Character", "Persona",
            DefaultPersonaZhHant,
            "System-style character prompt prepended to each request.");
        CharacterLore = Config.Bind("Character", "Lore",
            DefaultCharacterLoreZhHant,
            "Stable character facts and preferences appended independently of the editable persona.");
        EmotionGuidance = Config.Bind("Character", "EmotionGuidance",
            DefaultEmotionGuidanceZhHant,
            "Controls how visibly Lilith expresses emotion while preserving her restrained personality.");
        PostTypingHoldSeconds = Config.Bind("Display", "PostTypingHoldSeconds", 4f,
            "Seconds an AI bubble remains visible after its typewriter animation finishes.");
        VoiceEnabled = Config.Bind("Voice", "Enabled", true,
            "Generate speech for Gemini replies through the local GPT-SoVITS service.");
        VoiceEndpoint = Config.Bind("Voice", "Endpoint", "http://127.0.0.1:9880/tts",
            "GPT-SoVITS HTTP TTS endpoint.");
        VoiceAutoStartLocalService = Config.Bind("Voice", "AutoStartLocalService", true,
            "Automatically start the bundled hidden voice service when local TTS is installed.");
        VoiceHostPath = Config.Bind("Voice", "HostPath",
            Path.Combine(Paths.BepInExRootPath, "data", "LilithTextInjector", "voice-runtime", "LilithVoiceHost.exe"),
            "Bundled local voice service host. The installer writes this relative game-local path.");
        VoiceReferencePath = Config.Bind("Voice", "ReferencePath",
            Path.Combine(Paths.BepInExRootPath, "data", "LilithTextInjector", "voice", "calm-reference.wav"),
            "Reference WAV used for Lilith's default calm delivery.");
        ExcitedVoiceReferencePath = Config.Bind("Voice", "ExcitedReferencePath",
            Path.Combine(Paths.BepInExRootPath, "data", "LilithTextInjector", "voice", "excited-reference.wav"),
            "Reference WAV used after a surprised or happy native reaction.");
        WrongedVoiceReferencePath = Config.Bind("Voice", "WrongedReferencePath",
            Path.Combine(Paths.BepInExRootPath, "data", "LilithTextInjector", "voice", "wronged-reference.wav"),
            "Reference WAV used after a hurt or disappointed native reaction.");
        SleepyVoiceReferencePath = Config.Bind("Voice", "SleepyReferencePath",
            Path.Combine(Paths.BepInExRootPath, "data", "LilithTextInjector", "voice", "sleepy-reference.wav"),
            "Auxiliary reference WAV used while Lilith is sleeping, lying down, or yawning.");
        JapaneseVoiceEndpoint = Config.Bind("JapaneseVoice", "Endpoint", "http://127.0.0.1:9881/tts",
            "GPT-SoVITS endpoint for Japanese speech while the game's voice setting is Japanese.");
        JapaneseVoiceSelected = Config.Bind("JapaneseVoice", "Selected", false,
            "Remember Japanese game/AI voice selection across restarts.");
        JapaneseVoiceReferencePath = Config.Bind("JapaneseVoice", "CalmReferencePath",
            Path.Combine(Paths.BepInExRootPath, "data", "LilithTextInjector", "voice", "jp", "calm-reference.wav"),
            "Primary Japanese reference WAV.");
        JapaneseCalmAuxVoiceReferencePath = Config.Bind("JapaneseVoice", "CalmAuxReferencePath",
            Path.Combine(Paths.BepInExRootPath, "data", "LilithTextInjector", "voice", "jp", "calm-aux-reference.wav"),
            "Japanese auxiliary reference blended with the primary reference for calm speech.");
        JapaneseExcitedVoiceReferencePath = Config.Bind("JapaneseVoice", "ExcitedReferencePath",
            Path.Combine(Paths.BepInExRootPath, "data", "LilithTextInjector", "voice", "jp", "excited-reference.wav"),
            "Japanese auxiliary reference for bright or teasing speech.");
        JapaneseWrongedVoiceReferencePath = Config.Bind("JapaneseVoice", "WrongedReferencePath",
            Path.Combine(Paths.BepInExRootPath, "data", "LilithTextInjector", "voice", "jp", "wronged-reference.wav"),
            "Japanese auxiliary reference for sad or vulnerable speech.");
        JapaneseSleepyVoiceReferencePath = Config.Bind("JapaneseVoice", "SleepyReferencePath",
            Path.Combine(Paths.BepInExRootPath, "data", "LilithTextInjector", "voice", "jp", "sleepy-reference.wav"),
            "Japanese auxiliary reference for sleepy speech.");
        WeatherEnabled = Config.Bind("Weather", "Enabled", true,
            "Provide current Open-Meteo weather conditions to Lilith.");
        TestNoteOnce = Config.Bind("Notes", "CreateOneTestNote", false,
            "Create one native-format test note, then turn this option off automatically.");
        AiNotesEnabled = Config.Bind("Notes", "AiNotesEnabled", true,
            "Schedule occasional personalized notes after meaningful conversations.");
        AiNoteMinimumDelayMinutes = Config.Bind("Notes", "MinimumDelayMinutes", 30,
            "Minimum delay before a meaningful conversation may become a note.");
        AiNoteMaximumDelayMinutes = Config.Bind("Notes", "MaximumDelayMinutes", 120,
            "Maximum randomized delay before a meaningful conversation may become a note.");
        AiNoteCooldownHours = Config.Bind("Notes", "CooldownHours", 24,
            "Minimum time between AI-generated notes.");
        AiNoteWeeklyLimit = Config.Bind("Notes", "WeeklyLimit", 3,
            "Maximum number of AI-generated notes in a rolling seven-day window.");
        AiNoteRequiredAwayMinutes = Config.Bind("Notes", "RequiredAwayMinutes", 10,
            "Required Windows idle time before a scheduled AI note may be delivered.");
        WeatherAutoDetectFromIp = Config.Bind("Weather", "AutoDetectFromIp", true,
            "Approximate the weather city from the public IP address. No IP address is stored.");
        WeatherLocationName = Config.Bind("Weather", "LocationName", DefaultWeatherLocationNameZhHant,
            "Human-readable location name included with weather context.");
        WeatherLatitude = Config.Bind("Weather", "Latitude", 25.0375,
            "Weather latitude. Default is Taipei.");
        WeatherLongitude = Config.Bind("Weather", "Longitude", 121.5637,
            "Weather longitude. Default is Taipei.");
        ReactionSoundsEnabled = Config.Bind("Voice", "ReactionSoundsEnabled", true,
            "Play a short original reaction before synthesized speech when the emotion is unambiguous.");
        ReactionSoundsDirectory = Config.Bind("Voice", "ReactionSoundsDirectory",
            Path.Combine(Paths.BepInExRootPath, "data", "LilithTextInjector", "voice", "reactions"),
            "Directory containing original reaction WAV files.");
        ReactionFollowupPitch = Config.Bind("Voice", "ReactionFollowupPitch", 1f,
            "Pitch multiplier for synthesized speech immediately following an original reaction sound.");
        CollectUnvoicedNativeLines = Config.Bind("Voice", "CollectUnvoicedNativeLines", true,
            "Record fixed native dialogue nodes that have no original voice into a TSV manifest for safe pre-generation.");
        NativeVoicePackEnabled = Config.Bind("Voice", "NativeVoicePackEnabled", true,
            "Play supplemental WAV files only for native dialogue lines that have no original or action voice.");
        NativeVoicePackDirectory = Config.Bind("Voice", "NativeVoicePackDirectory",
            Path.Combine(Paths.BepInExRootPath, "data", "LilithTextInjector", "native-voice-pack"),
            "Directory containing the Chinese manifest.tsv and supplemental native dialogue WAV files.");
        JapaneseNativeVoicePackDirectory = Config.Bind("JapaneseVoice", "NativeVoicePackDirectory",
            Path.Combine(Paths.BepInExRootPath, "data", "LilithTextInjector", "native-voice-pack-ja"),
            "Directory containing the Japanese manifest.tsv and supplemental native dialogue WAV files.");
        VoiceInputEnabled = Config.Bind("VoiceInput", "Enabled", true,
            "Hold F6 to record from the default microphone; release to transcribe with the active AI provider and send as chat input.");
        VoiceInputMaxSeconds = Config.Bind("VoiceInput", "MaxRecordingSeconds", 60,
            "Maximum duration of one push-to-talk recording (5-90 seconds).");
        VoiceInputDeviceName = Config.Bind("VoiceInput", "DeviceName", string.Empty,
            "Exact Unity microphone device name. Leave empty to use the Windows default input device.");
        TextInputKey = Config.Bind("Input", "TextChatKey", KeyCode.F7,
            "Key used to open or close the AI text input bubble. This can also be changed in the in-game settings.");
        VoiceInputKey = Config.Bind("Input", "PushToTalkKey", KeyCode.F6,
            "Key held while recording AI voice input. This can also be changed in the in-game settings.");
        AdvancedComputerActionsEnabled = Config.Bind("ComputerActions", "AdvancedEnabled", false,
            "Unlock reviewed advanced computer controls. This does not grant Windows administrator rights or allow arbitrary shell commands.");
        CodexBridgeEnabled = Config.Bind("CodexBridge", "Enabled", true,
            "Show local Codex lifecycle status through Lilith. No prompt text, API key, or Codex login data is read.");
        CodexBridgeVoiceEnabled = Config.Bind("CodexBridge", "VoiceEnabled", true,
            "Speak the important Codex lifecycle messages (started, approval required, and completed).");
        MigrateKnownMojibakeDefaults();
        DialogueManagerUpdatePatch.LoadMemory();
        DialogueManagerUpdatePatch.LoadAiNoteState();
        DialogueManagerUpdatePatch.EnsureApplicationLauncherFile();
        DialogueManagerUpdatePatch.LogOfficialApplicationCategories();
        TryCreateAndPatchAll(typeof(DialogueManagerUpdatePatch), PluginGuid, "core dialogue and input hooks");
        TryCreateAndPatchAll(typeof(GiftExchangeApiKeyPatch), PluginGuid + ".apikey", "API key window hooks");
        TryCreateAndPatchAll(typeof(GiftExchangeApiKeyHidePatch), PluginGuid + ".apikeyhide", "API key window isolation hooks");
        TryCreateAndPatchAll(typeof(TrayMenuLocalizationPatch), PluginGuid + ".traylocalization", "tray localization hook");
        TryCreateAndPatchAll(typeof(VoiceSettingsButtonClickPatch), PluginGuid + ".voicelanguage", "voice language preference hook");
        Log.LogInfo($"Loaded. Press {TextInputKey.Value} for text chat; hold {VoiceInputKey.Value} for push-to-talk voice input.");
        if (string.IsNullOrWhiteSpace(GeminiApiKey.Value))
            Log.LogWarning("Gemini ApiKey is empty. Set it in BepInEx/config/community.lilith.textinjector.cfg.");
    }

    private void TryCreateAndPatchAll(Type patchType, string harmonyId, string feature)
    {
        try
        {
            Harmony.CreateAndPatchAll(patchType, harmonyId);
            Log.LogInfo($"Initialized {feature}.");
        }
        catch (Exception exception)
        {
            Log.LogError($"Could not initialize {feature}; the remaining MOD features will continue: {exception}");
        }
    }

    private void MigrateKnownMojibakeDefaults()
    {
        var changed = false;
        changed |= MigrateKnownMojibake(PersonaPrompt, DefaultPersonaZhHant, "浣犳槸");
        changed |= MigrateKnownMojibake(CharacterLore, DefaultCharacterLoreZhHant, "鑾夎帀");
        changed |= MigrateKnownMojibake(EmotionGuidance, DefaultEmotionGuidanceZhHant, "鑾夎帀");
        changed |= MigrateKnownMojibake(WeatherLocationName, DefaultWeatherLocationNameZhHant, "鍙板寳");
        if (changed)
        {
            Config.Save();
            PluginLog.LogInfo("Migrated known mojibake character/weather defaults without replacing custom values.");
        }
    }

    private static bool MigrateKnownMojibake(ConfigEntry<string> entry, string correctedDefault, params string[] knownPrefixes)
    {
        var value = entry.Value ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value) || !knownPrefixes.Any(prefix => value.StartsWith(prefix, StringComparison.Ordinal)))
            return false;

        entry.Value = correctedDefault;
        return true;
    }
}
