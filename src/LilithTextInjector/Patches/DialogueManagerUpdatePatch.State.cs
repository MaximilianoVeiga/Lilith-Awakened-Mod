namespace LilithTextInjector;

// Shared mutable state for the dialogue update patch.
// Disk paths live in ModDataPaths; chat memory and note persistence live in
// ChatMemory / AiNoteStore. Queues and Unity-tick flags remain here.
internal static partial class DialogueManagerUpdatePatch
{
    private static GameObject? _inputBubble;
    private static TMP_InputField? _inputField;
    private static TextMeshProUGUI? _inputPlaceholder;
    private static bool _focusNextFrame;
    private static readonly ConcurrentQueue<string> PendingReplies = new();
    private static readonly ConcurrentQueue<string> PendingAiEmotions = new();
    internal static readonly ConcurrentQueue<string> PendingTranscripts = new();
    internal static readonly ConcurrentQueue<string> PendingTranscriptionErrors = new();
    private static readonly ConcurrentQueue<GeneratedAiNote> PendingAiNotes = new();
    private static readonly ConcurrentQueue<GeminiToolBatch> PendingGeminiToolBatches = new();
    private static readonly ConcurrentQueue<GeminiAgentSession> PendingGeminiCompatibilityFallbacks = new();
    private static readonly ConcurrentQueue<QwenToolBatch> PendingQwenToolBatches = new();
    private static bool _aiNoteGenerationInFlight;
    private static float _nextAiNoteCheckAt;
    internal static bool _requestInFlight;
    private static bool _aiPagesAwaitingAdvance;
    private static string _currentAiPageText = string.Empty;
    private static float _aiTypingFinishedAt = -1f;
    private static int _repliesSincePlayerNameWasOffered = 3;
    private static DateTimeOffset _weatherFetchedAt = DateTimeOffset.MinValue;
    private static string _cachedWeatherContext = string.Empty;
    private static bool _ipWeatherLocationResolved;

    private static float _nextJapaneseVoiceUiScanAt;
    private static string _lastObservedVoiceLanguage = string.Empty;
    private static readonly HashSet<string> LoggedVoiceUiObjects = new();
    private static bool? _japaneseVoiceOverride;
    private static bool _voicePreferenceInitialized;
    private static bool _voicePreferenceAppliedToNativeUi;
    private static float _nextJapaneseVoiceToggleRestoreAt;
    private static IntPtr _apiKeyTrayPointer;
    private static bool _apiKeyDialogMode;
    private static volatile bool _apiKeyOpenRequested;
    private static float _apiKeyOpenRequestedAt = -1f;
    private static bool _apiKeyMissingViewLogged;
    private static GiftExchangeView? _apiKeyView;
    private static string _pendingApiKeyProvider = "Gemini";
    private static readonly List<ApiKeyDialogLabelSnapshot> ApiKeyDialogLabels = new();
    private static bool _apiKeyDialogStateCaptured;
    private static TMP_InputField.ContentType _apiKeyOriginalContentType;
    private static TMP_InputField.LineType _apiKeyOriginalLineType;
    private static int _apiKeyOriginalCharacterLimit;
    private static string? _pendingVoiceSubmitText;
    private static float _pendingVoiceSubmitAt = -1f;
    private static bool _testNoteAttempted;
    private static float _nextAdvancedActionsUiScanAt;
    private static GameObject? _advancedActionsRow;
    private static ButtonToggle? _advancedActionsToggle;
    private static Il2CppSystem.Action<bool>? _advancedActionsChanged;
    private static float _nextKeyBindingsUiScanAt;
    private static GameObject? _textInputKeyRow;
    private static GameObject? _voiceInputKeyRow;
    private static RectTransform? _textInputKeyButtonRect;
    private static RectTransform? _voiceInputKeyButtonRect;
    private static ButtonToggle? _textInputKeyButton;
    private static ButtonToggle? _voiceInputKeyButton;
    private static TMP_Text? _textInputKeyValue;
    private static TMP_Text? _voiceInputKeyValue;
    internal static int _keyBindingTarget;
    private static float _keyBindingStartedAt = -1f;
    private static bool _textInputKeyWasDown;
    private static readonly HashSet<int> RebindingHeldVirtualKeys = new();
    private static TraySettingView? _settingsView;
    private static GameObject? _settingsVisibilityTemplateRow;
    private static float _nextForegroundWindowScanAt;
    private static IntPtr _lastExternalForegroundWindow;
    private static readonly List<LocalTimer> LocalTimers = new();
    private static PendingSystemAction? _pendingSystemAction;
    private static readonly ConcurrentQueue<CodexBridgeSignal> PendingCodexSignals = new();
    private static readonly string CodexBridgeEventsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LilithAiMod", "CodexBridge", "events");
    private static float _nextCodexBridgePollAt;
    private static float _nextCodexSignalAt;
}
