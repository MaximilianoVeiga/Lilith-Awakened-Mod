namespace LilithTextInjector;

// Per-frame Update hook, chat input bubble lifecycle and AI page paging.
[HarmonyPatch(typeof(DialogueManager), "Update")]
internal static partial class DialogueManagerUpdatePatch
{
    private static void Postfix(DialogueManager __instance)
    {
        UpdateSettingsUiSafely();
        EnsureLocalVoiceHost();
        ObserveForegroundWindow();
        ProcessGeminiToolBatches();
        ProcessGeminiCompatibilityFallbacks();
        ProcessQwenToolBatches();
        ProcessLocalTimers(__instance);
        ProcessPendingSystemAction();
        EnsureApiKeyTrayMenu();
        ProcessApiKeyOpenRequest();
        ConfigureApiKeyDialog();
        UpdateInputPlaceholderLocalization();
        ObserveVoiceLanguageSelection();
        TryCreateOneTestNote();
        ProcessAiNoteScheduler();
        ObserveCurrentNativeNode(__instance);
        PollCodexBridgeEvents();
        ProcessPendingCodexSignal(__instance);
        if (!_nativeDatabaseDumpCompleted)
            TryDumpNativeDialogueDatabases(__instance);
        if (!_localizedLineDatabasesDumped)
            TryDumpLocalizedLineDatabases();

        if (_voicePitchResetAt >= 0f && Time.unscaledTime >= _voicePitchResetAt)
        {
            SetVoicePitch(1f);
            _voicePitchResetAt = -1f;
        }

        if (_delayedSpeechAudio != null && Time.unscaledTime >= _delayedSpeechPlayAt)
        {
            try
            {
                var pitch = Math.Clamp(Plugin.ReactionFollowupPitch.Value, 0.8f, 1.2f);
                SetVoicePitch(pitch);
                var clip = PlayWav(_delayedSpeechAudio, "generated speech");
                _voicePitchResetAt = Time.unscaledTime + clip.length / Math.Max(0.01f, pitch) + 0.05f;
            }
            catch (Exception exception)
            {
                Plugin.PluginLog.LogError($"Could not play generated voice: {exception}");
            }
            _delayedSpeechAudio = null;
            _delayedSpeechPlayAt = -1f;
        }
        else if (_delayedSpeechAudio == null && PendingVoiceAudio.TryDequeue(out var sequence))
        {
            try
            {
                if (sequence.Reaction != null)
                {
                    SetVoicePitch(1f);
                    var reactionClip = PlayWav(sequence.Reaction, "native reaction");
                    _delayedSpeechAudio = sequence.Speech;
                    _delayedSpeechPlayAt = Time.unscaledTime + reactionClip.length + 0.03f;
                }
                else
                {
                    SetVoicePitch(1f);
                    PlayWav(sequence.Speech, "generated speech");
                }
            }
            catch (Exception exception)
            {
                Plugin.PluginLog.LogError($"Could not play voice sequence: {exception}");
                _delayedSpeechAudio = null;
                _delayedSpeechPlayAt = -1f;
            }
        }

        if (_requestInFlight && PendingReplies.TryDequeue(out var pendingReply))
        {
            _requestInFlight = false;
            _aiPagesAwaitingAdvance = !PendingReplies.IsEmpty;
            _currentAiPageText = pendingReply;
            _aiTypingFinishedAt = -1f;
            if (PendingAiEmotions.TryDequeue(out var emotion))
                PlayAiEmotion(emotion);
            __instance.ForceSay(pendingReply, string.Empty, 30f);
        }

        HandleVoiceInput(__instance);
        if (PendingTranscriptionErrors.TryDequeue(out var transcriptionError))
            __instance.ForceSay(transcriptionError, string.Empty, 6f);
        if (PendingTranscripts.TryDequeue(out var transcript))
        {
            _pendingVoiceSubmitText = transcript;
            _pendingVoiceSubmitAt = Time.unscaledTime + 1.5f;
            __instance.ForceSay(
                ApiKeyText($"我聽見了：「{transcript}」", $"我听见了：“{transcript}”", $"「{transcript}」と聞こえたよ。", $"I heard: “{transcript}”"),
                string.Empty,
                4f);
        }
        if (_pendingVoiceSubmitText != null && Time.unscaledTime >= _pendingVoiceSubmitAt)
        {
            var voiceText = _pendingVoiceSubmitText;
            _pendingVoiceSubmitText = null;
            _pendingVoiceSubmitAt = -1f;
            SubmitAiInput(__instance, voiceText);
        }

        var textInputKeyDown = IsKeyCurrentlyDown(Plugin.TextInputKey.Value);
        var textInputKeyPressed = textInputKeyDown && !_textInputKeyWasDown;
        _textInputKeyWasDown = textInputKeyDown;
        if (_keyBindingTarget == 0 && textInputKeyPressed)
        {
            if (_requestInFlight || _aiPagesAwaitingAdvance)
            {
                Plugin.PluginLog.LogInfo($"Ignored {Plugin.TextInputKey.Value} text input hotkey because a dialogue or AI request is active.");
                return;
            }
            ToggleInputBubble();
            return;
        }

        if (_inputBubble != null && _inputField != null && _inputBubble.activeSelf)
        {
            if (!IsLilithForeground())
            {
                // TMP_InputField cannot reliably recover after this transparent
                // desktop window loses native focus. Close and dispose the bubble;
                // the next shortcut invocation creates a clean input session.
                CloseInputBubble(clear: true);
                return;
            }

            if (_focusNextFrame)
            {
                _focusNextFrame = false;
                _inputField.ActivateInputField();
                _inputField.Select();
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CloseInputBubble(clear: false);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                var submitted = _inputField!.text.Trim();
                if (submitted.Length > 0)
                {
                    CloseInputBubble(clear: true);
                    SubmitAiInput(__instance, submitted);
                }
                return;
            }
        }
    }

    private static void SubmitAiInput(DialogueManager manager, string submitted)
    {
        submitted = submitted.Trim();
        if (submitted.Length == 0)
            return;
        if (_requestInFlight)
        {
            manager.ForceSay(ApiKeyText("先等我說完。", "先等我说完。", "先に話し終えさせて……", "Let me finish speaking first."), string.Empty, 4f);
            return;
        }
        UpdatePendingAiNoteEvents(submitted);
        var activeProvider = NormalizeAiProvider(Plugin.AiProvider.Value);
        var useModelComputerTools = (string.Equals(activeProvider, "Gemini", StringComparison.Ordinal)
                || string.Equals(activeProvider, "Qwen", StringComparison.Ordinal))
            && Plugin.AdvancedComputerActionsEnabled.Value;
        // Qwen may answer a desktop request in prose without emitting a function call.
        // Route its explicit, locally verifiable commands before asking the model.
        var preferLocalComputerRouter = string.Equals(activeProvider, "Qwen", StringComparison.Ordinal)
            && Plugin.AdvancedComputerActionsEnabled.Value;
        if ((!useModelComputerTools || preferLocalComputerRouter) && TryHandleScreenshotCommand(submitted, out var screenshotReply))
        {
            _requestInFlight = true;
            AddMemoryTurn("user", submitted);
            AddMemoryTurn("model", screenshotReply);
            PendingAiEmotions.Enqueue("emoji_smile_1");
            PendingReplies.Enqueue(screenshotReply);
            if (Plugin.VoiceEnabled.Value)
                _ = RequestSpeechAsync(screenshotReply, poseStyle: CapturePoseContext().VoiceStyle);
            return;
        }
        if ((!useModelComputerTools || preferLocalComputerRouter) && TryHandleComputerCommand(submitted, out var computerReply))
        {
            _requestInFlight = true;
            AddMemoryTurn("user", submitted);
            AddMemoryTurn("model", computerReply);
            PendingAiEmotions.Enqueue("emoji_smile_1");
            PendingReplies.Enqueue(computerReply);
            if (Plugin.VoiceEnabled.Value)
                _ = RequestSpeechAsync(computerReply, poseStyle: CapturePoseContext().VoiceStyle);
            return;
        }
        if ((!useModelComputerTools || preferLocalComputerRouter) && TryHandleMediaCommand(submitted, out var mediaReply))
        {
            _requestInFlight = true;
            AddMemoryTurn("user", submitted);
            AddMemoryTurn("model", mediaReply);
            PendingAiEmotions.Enqueue("emoji_smile_1");
            PendingReplies.Enqueue(mediaReply);
            if (Plugin.VoiceEnabled.Value)
                _ = RequestSpeechAsync(mediaReply, poseStyle: CapturePoseContext().VoiceStyle);
            return;
        }
        if ((!useModelComputerTools || preferLocalComputerRouter) && TryLaunchApplicationCommand(submitted, out var launchReply))
        {
            _requestInFlight = true;
            AddMemoryTurn("user", submitted);
            AddMemoryTurn("model", launchReply);
            PendingAiEmotions.Enqueue("emoji_smile_1");
            PendingReplies.Enqueue(launchReply);
            if (Plugin.VoiceEnabled.Value)
                _ = RequestSpeechAsync(launchReply, poseStyle: CapturePoseContext().VoiceStyle);
            return;
        }
        if (string.IsNullOrWhiteSpace(GetActiveChatApiKey()))
        {
            manager.ForceSay(ApiKeyText("還沒有設定目前模型的 API Key。", "还没有设置当前模型的 API Key。", "現在のモデルのAPIキーがまだ設定されていないよ。", "The current model does not have an API key yet."), string.Empty, 6f);
            return;
        }
        _requestInFlight = true;
        manager.ForceSay("……", string.Empty, 30f);
        AddMemoryTurn("user", submitted);
        if (!useModelComputerTools && UsesTraditionalChineseInterface() && TryBuildLocalTimeReply(submitted, out var localTimeReply))
        {
            AddMemoryTurn("model", localTimeReply);
            PendingReplies.Enqueue(localTimeReply);
            if (Plugin.VoiceEnabled.Value)
                _ = RequestSpeechAsync(localTimeReply);
            Plugin.PluginLog.LogInfo("Answered time/date question from the local system clock.");
        }
        else
        {
            var playerName = Archive.Instance != null ? Archive.Instance.playerName : string.Empty;
            if (PlayerNameRule.IsUnsetName(playerName))
                playerName = string.Empty;
            _ = RequestGeminiAsync(submitted, playerName, CapturePoseContext());
        }
        Plugin.PluginLog.LogInfo($"Submitted AI input ({submitted.Length} chars).");
    }

    private static void ToggleInputBubble()
    {
        if (_inputBubble == null && !TryCreateInputBubble())
            return;

        UpdateInputPlaceholderLocalization();

        if (_inputBubble!.activeSelf)
        {
            // The global shortcut can still be detected while another desktop
            // application owns the keyboard. In that case the visible bubble is
            // not being toggled off: the user is asking to type into it again.
            if (!IsLilithForeground())
            {
                TryBringLilithToForeground();
                _focusNextFrame = true;
                return;
            }
            CloseInputBubble(clear: false);
            return;
        }

        _inputBubble.SetActive(true);
        var canvasGroup = _inputBubble.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        TryBringLilithToForeground();
        _focusNextFrame = true;
    }

    private static bool IsLilithForeground()
    {
        if (!OperatingSystem.IsWindows())
            return Application.isFocused;
        var foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero)
            return false;
        GetWindowThreadProcessId(foreground, out var processId);
        return processId == (uint)Environment.ProcessId;
    }

    private static void TryBringLilithToForeground()
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            using var process = Process.GetCurrentProcess();
            var gameWindow = process.MainWindowHandle;
            if (gameWindow == IntPtr.Zero)
            {
                Plugin.PluginLog.LogWarning("Could not focus text input because the Lilith window handle was not found.");
                return;
            }

            ShowWindow(gameWindow, 9);
            if (SetForegroundWindow(gameWindow))
                return;

            // Windows can reject a foreground change made by a background
            // process. Temporarily join the current foreground input queue so
            // the user-initiated chat shortcut can activate Lilith reliably.
            var foreground = GetForegroundWindow();
            var foregroundThread = foreground == IntPtr.Zero
                ? 0u
                : GetWindowThreadProcessId(foreground, out _);
            var currentThread = GetCurrentThreadId();
            var attached = foregroundThread != 0 && foregroundThread != currentThread
                && AttachThreadInput(currentThread, foregroundThread, true);
            try
            {
                BringWindowToTop(gameWindow);
                SetForegroundWindow(gameWindow);
                SetFocus(gameWindow);
            }
            finally
            {
                if (attached)
                    AttachThreadInput(currentThread, foregroundThread, false);
            }
        }
        catch (Exception exception)
        {
            Plugin.PluginLog.LogWarning($"Could not return Windows focus to the text input bubble: {exception.Message}");
        }
    }

    private static bool TryCreateInputBubble()
    {
        try
        {
            var sourceUi = UnityEngine.Object.FindObjectOfType<DialogueBubbleUI>();
            if (sourceUi == null)
            {
                Plugin.PluginLog.LogWarning("DialogueBubbleUI was not found yet.");
                return false;
            }

            var sourceText = sourceUi.gameObject.GetComponentInChildren<TextMeshProUGUI>();
            if (sourceText == null)
            {
                Plugin.PluginLog.LogWarning("Dialogue bubble text component was not found.");
                return false;
            }

            var bubbleSprite = FindSprite("Choise_Bubble");
            if (bubbleSprite == null)
                throw new InvalidOperationException("Sprite 'Choise_Bubble' was not found.");

            _inputBubble = new GameObject(
                "LilithAiInputBubble",
                Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>(),
                Il2CppInterop.Runtime.Il2CppType.Of<CanvasRenderer>(),
                Il2CppInterop.Runtime.Il2CppType.Of<Image>(),
                Il2CppInterop.Runtime.Il2CppType.Of<TMP_InputField>());
            _inputBubble.transform.SetParent(sourceUi.transform.parent, false);

            var rootRect = _inputBubble.GetComponent<RectTransform>();
            var sourceRect = sourceUi.GetComponent<RectTransform>();
            rootRect.anchorMin = sourceRect.anchorMin;
            rootRect.anchorMax = sourceRect.anchorMax;
            rootRect.pivot = sourceRect.pivot;
            rootRect.sizeDelta = new Vector2(203f, 39f);
            rootRect.anchoredPosition = sourceRect.anchoredPosition + new Vector2(0f, 45f);

            var background = _inputBubble.GetComponent<Image>();
            background.sprite = bubbleSprite;
            background.type = Image.Type.Sliced;
            background.raycastTarget = true;

            var viewportObject = new GameObject(
                "Text Area",
                Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>(),
                Il2CppInterop.Runtime.Il2CppType.Of<CanvasRenderer>(),
                Il2CppInterop.Runtime.Il2CppType.Of<RectMask2D>());
            viewportObject.transform.SetParent(_inputBubble.transform, false);
            var viewport = viewportObject.GetComponent<RectTransform>();
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(20f, 5f);
            viewport.offsetMax = new Vector2(-20f, -5f);

            var inputTextObject = UnityEngine.Object.Instantiate(sourceText.gameObject, viewport);
            inputTextObject.name = "Text";
            var typewriter = inputTextObject.GetComponent<TypewriterEffect>();
            if (typewriter != null)
                typewriter.enabled = false;
            var inputText = inputTextObject.GetComponent<TextMeshProUGUI>();
            inputText.text = string.Empty;
            inputText.raycastTarget = true;
            inputText.fontSize = 15f;
            inputText.enableWordWrapping = false;
            inputText.overflowMode = TextOverflowModes.Masking;
            inputText.alignment = TextAlignmentOptions.MidlineLeft;
            var inputTextRect = inputText.rectTransform;
            inputTextRect.anchorMin = Vector2.zero;
            inputTextRect.anchorMax = Vector2.one;
            inputTextRect.offsetMin = Vector2.zero;
            inputTextRect.offsetMax = Vector2.zero;

            var placeholderObject = UnityEngine.Object.Instantiate(inputText.gameObject, viewport);
            placeholderObject.name = "Placeholder";
            var placeholder = placeholderObject.GetComponent<TextMeshProUGUI>();
            _inputPlaceholder = placeholder;
            UpdateInputPlaceholderLocalization();
            placeholder.fontSize = 15f;
            var placeholderColor = placeholder.color;
            placeholderColor.a = 0.45f;
            placeholder.color = placeholderColor;

            _inputField = _inputBubble.GetComponent<TMP_InputField>();
            if (_inputField == null)
                _inputField = _inputBubble.AddComponent<TMP_InputField>();

            _inputField.textComponent = inputText;
            _inputField.placeholder = placeholder;
            _inputField.textViewport = viewport;
            _inputField.targetGraphic = background;
            _inputField.lineType = TMP_InputField.LineType.SingleLine;
            _inputField.characterLimit = 240;

            _inputBubble.SetActive(false);
            Plugin.PluginLog.LogInfo("Created input field from the native dialogue bubble.");
            return true;
        }
        catch (Exception exception)
        {
            Plugin.PluginLog.LogError(exception);
            if (_inputBubble != null)
                UnityEngine.Object.Destroy(_inputBubble);
            _inputBubble = null;
            _inputField = null;
            _inputPlaceholder = null;
            _focusNextFrame = false;
            return false;
        }
    }

    private static void UpdateInputPlaceholderLocalization()
    {
        if (_inputPlaceholder == null)
            return;
        _inputPlaceholder.text = ApiKeyText(
            "想對莉莉絲說什麼……",
            "想对莉莉丝说什么……",
            "リリスに何を話そう……",
            "What would you like to say to Lilith…");
    }

    private static string[] SplitIntoBubblePages(string text)
    {
        var completeText = text.Trim();
        return completeText.Length == 0 ? Array.Empty<string>() : new[] { completeText };
    }

    internal static bool TryAdvanceAiPage(DialogueManager manager)
    {
        if (!_aiPagesAwaitingAdvance)
            return false;
        var currentNode = manager.CurrentNode;
        if (currentNode == null || !string.Equals(currentNode.text, _currentAiPageText, StringComparison.Ordinal))
        {
            CancelPendingAiPages("Native dialogue took control.");
            return false;
        }
        if (!PendingReplies.TryDequeue(out var page))
        {
            _aiPagesAwaitingAdvance = false;
            return false;
        }
        _aiPagesAwaitingAdvance = !PendingReplies.IsEmpty;
        _currentAiPageText = page;
        _aiTypingFinishedAt = -1f;
        manager.ForceSay(page, string.Empty, 30f);
        return true;
    }

    private static void CancelPendingAiPages(string reason)
    {
        while (PendingReplies.TryDequeue(out _)) { }
        _aiPagesAwaitingAdvance = false;
        _currentAiPageText = string.Empty;
        Plugin.PluginLog.LogInfo($"Cancelled pending AI pages: {reason}");
    }

    internal static void NotifyAiTypingState(bool isTyping)
    {
        if (string.IsNullOrEmpty(_currentAiPageText))
            return;
        _aiTypingFinishedAt = isTyping ? -1f : Time.unscaledTime;
    }

    internal static bool ShouldDelayAiCompletion(DialogueManager manager)
    {
        var node = manager.CurrentNode;
        if (node == null || !string.Equals(node.text, _currentAiPageText, StringComparison.Ordinal))
            return false;
        // Keep the current bubble alive while another AI page is waiting for the
        // user's click. Speech is generated for the complete reply and may continue
        // past the first page, so allowing Unity to close here makes the text look
        // truncated even though the response itself is complete.
        if (_aiPagesAwaitingAdvance)
            return true;
        if (_aiTypingFinishedAt < 0f)
            return true;
        return Time.unscaledTime - _aiTypingFinishedAt < Math.Max(0f, Plugin.PostTypingHoldSeconds.Value);
    }

    private static void CloseInputBubble(bool clear)
    {
        if (_inputField != null)
        {
            _inputField.DeactivateInputField();
            if (clear)
                _inputField.text = string.Empty;
        }

        // TMP_InputField can retain a stale activation state after its parent is
        // hidden in this IL2CPP build. Recreate the lightweight bubble on the next
        // invocation so every F7 session starts with a clean input component.
        if (_inputBubble != null)
            UnityEngine.Object.Destroy(_inputBubble);
        _inputBubble = null;
        _inputField = null;
        _inputPlaceholder = null;
        _focusNextFrame = false;
    }
}
