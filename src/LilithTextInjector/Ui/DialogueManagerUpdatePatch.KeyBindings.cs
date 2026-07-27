namespace LilithTextInjector;

// In-game rebinding of the text chat and push-to-talk keys.
internal static partial class DialogueManagerUpdatePatch
{
    private static void EnsureKeyBindingSettings()
    {
        var controlsVisible = SyncInjectedSettingsVisibility();
        UpdateKeyBindingTexts();
        if (controlsVisible && ProcessKeyBindingInteraction())
            return;
        if (_textInputKeyRow != null && _voiceInputKeyRow != null
            && _textInputKeyButtonRect != null && _voiceInputKeyButtonRect != null)
            return;
        if (Time.unscaledTime < _nextKeyBindingsUiScanAt)
            return;
        _nextKeyBindingsUiScanAt = Time.unscaledTime + 0.75f;

        try
        {
            TraySettingView? view = null;
            foreach (var candidate in Resources.FindObjectsOfTypeAll<TraySettingView>())
            {
                if (candidate != null && candidate.gameObject != null && candidate._crossScreenDragToggle != null)
                {
                    view = candidate;
                    break;
                }
            }
            if (view == null)
                return;

            var templateRow = FindSettingRow(view._crossScreenDragToggle.transform, view.transform);
            if (templateRow == null || templateRow.parent == null)
                return;

            _settingsView = view;
            _settingsVisibilityTemplateRow = templateRow.gameObject;

            if (!CreateKeyBindingRow(templateRow, "LilithTextInputKey", 82f,
                    out _textInputKeyRow, out _textInputKeyButton, out _textInputKeyButtonRect, out _textInputKeyValue)
                || !CreateKeyBindingRow(templateRow, "LilithVoiceInputKey", 41f,
                    out _voiceInputKeyRow, out _voiceInputKeyButton, out _voiceInputKeyButtonRect, out _voiceInputKeyValue))
            {
                if (_textInputKeyRow != null) UnityEngine.Object.Destroy(_textInputKeyRow);
                if (_voiceInputKeyRow != null) UnityEngine.Object.Destroy(_voiceInputKeyRow);
                ResetKeyBindingUiReferences();
                return;
            }

            UpdateKeyBindingTexts();
            SyncInjectedSettingsVisibility();
            Plugin.PluginLog.LogInfo("Added native-style text input and push-to-talk key binding controls.");
        }
        catch (Exception exception)
        {
            Plugin.PluginLog.LogWarning($"Could not add key binding settings: {exception}");
            ResetKeyBindingUiReferences();
        }
    }

    private static bool CreateKeyBindingRow(
        Transform templateRow,
        string objectName,
        float verticalOffset,
        out GameObject? row,
        out ButtonToggle? button,
        out RectTransform? buttonRect,
        out TMP_Text? valueText)
    {
        row = null;
        button = null;
        buttonRect = null;
        valueText = null;
        if (templateRow.parent == null)
            return false;

        var clone = UnityEngine.Object.Instantiate(templateRow.gameObject, templateRow.parent);
        clone.name = objectName;
        clone.SetActive(true);
        var clonedButton = FindButtonToggle(clone.transform);
        var label = FindFirstText(clone.transform);
        if (clonedButton == null || label == null)
        {
            UnityEngine.Object.Destroy(clone);
            return false;
        }

        clonedButton.OnValueChanged = null;
        clonedButton.SetValue(false, false);
        var toggleRect = clonedButton.gameObject.GetComponent<RectTransform>();
        if (toggleRect == null)
        {
            UnityEngine.Object.Destroy(clone);
            return false;
        }
        toggleRect.sizeDelta = new Vector2(Math.Max(82f, toggleRect.sizeDelta.x), Math.Max(28f, toggleRect.sizeDelta.y));

        // Keep the native rounded frame on the ButtonToggle root, but hide its
        // child state marker. A key binding is a button, not an on/off switch.
        for (var index = 0; index < clonedButton.transform.childCount; index++)
            clonedButton.transform.GetChild(index).gameObject.SetActive(false);

        var valueObject = UnityEngine.Object.Instantiate(label.gameObject, clonedButton.transform);
        valueObject.name = "LilithKeyValue";
        valueObject.SetActive(true);
        var clonedValue = valueObject.GetComponent<TMP_Text>();
        var valueRect = valueObject.GetComponent<RectTransform>();
        if (clonedValue == null || valueRect == null)
        {
            UnityEngine.Object.Destroy(clone);
            return false;
        }
        valueRect.anchorMin = Vector2.zero;
        valueRect.anchorMax = Vector2.one;
        valueRect.offsetMin = Vector2.zero;
        valueRect.offsetMax = Vector2.zero;
        valueRect.anchoredPosition = Vector2.zero;
        clonedValue.alignment = TextAlignmentOptions.Center;
        clonedValue.raycastTarget = false;
        clonedValue.enableWordWrapping = false;

        PlaceKeyBindingRow(templateRow, clone.transform, verticalOffset);
        row = clone;
        button = clonedButton;
        buttonRect = toggleRect;
        valueText = clonedValue;
        return true;
    }

    private static void PlaceKeyBindingRow(Transform templateRow, Transform clonedRow, float verticalOffset)
    {
        var parent = templateRow.parent;
        var cloneRect = clonedRow.gameObject.GetComponent<RectTransform>();
        var templateRect = templateRow.gameObject.GetComponent<RectTransform>();
        if (parent == null || cloneRect == null || templateRect == null)
            return;
        var layoutElement = clonedRow.gameObject.GetComponent<LayoutElement>();
        if (layoutElement == null)
            layoutElement = clonedRow.gameObject.AddComponent<LayoutElement>();
        layoutElement.ignoreLayout = true;
        clonedRow.SetSiblingIndex(parent.childCount - 1);
        cloneRect.anchoredPosition = templateRect.anchoredPosition + new Vector2(300f, verticalOffset);
    }

    private static bool ProcessKeyBindingInteraction()
    {
        if (_keyBindingTarget != 0)
        {
            if (_keyBindingStartedAt >= 0f && Time.unscaledTime - _keyBindingStartedAt >= 15f)
            {
                _keyBindingTarget = 0;
                _keyBindingStartedAt = -1f;
                RebindingHeldVirtualKeys.Clear();
                UpdateKeyBindingTexts();
                Plugin.PluginLog.LogInfo("Key rebinding timed out and was cancelled.");
                return true;
            }
            // Read the Windows keyboard state directly. The updated settings
            // window no longer forwards keyboard events to Unity's legacy Input
            // API while it is focused.
            for (var code = (int)KeyCode.Backspace; code < (int)KeyCode.Mouse0; code++)
            {
                var key = (KeyCode)code;
                if (!TryGetWindowsVirtualKey(key, out var virtualKey))
                    continue;
                var down = IsVirtualKeyDown(virtualKey);
                if (!down)
                {
                    RebindingHeldVirtualKeys.Remove(virtualKey);
                    continue;
                }
                if (!RebindingHeldVirtualKeys.Add(virtualKey))
                    continue;
                if (key == KeyCode.Escape)
                {
                    _keyBindingTarget = 0;
                    _keyBindingStartedAt = -1f;
                    RebindingHeldVirtualKeys.Clear();
                    UpdateKeyBindingTexts();
                    Plugin.PluginLog.LogInfo("Key rebinding cancelled.");
                    return true;
                }
                ApplyKeyBinding(key);
                return true;
            }
            return true;
        }

        if (!Input.GetMouseButtonDown(0))
            return false;
        if (_textInputKeyButtonRect != null && _textInputKeyRow != null && _textInputKeyRow.activeInHierarchy
            && RectTransformUtility.RectangleContainsScreenPoint(_textInputKeyButtonRect, Input.mousePosition, null))
        {
            _keyBindingTarget = 1;
            _keyBindingStartedAt = Time.unscaledTime;
            CaptureHeldRebindingKeys();
            UpdateKeyBindingTexts();
            Plugin.PluginLog.LogInfo("Waiting for a new text input hotkey.");
            return true;
        }
        if (_voiceInputKeyButtonRect != null && _voiceInputKeyRow != null && _voiceInputKeyRow.activeInHierarchy
            && RectTransformUtility.RectangleContainsScreenPoint(_voiceInputKeyButtonRect, Input.mousePosition, null))
        {
            _keyBindingTarget = 2;
            _keyBindingStartedAt = Time.unscaledTime;
            CaptureHeldRebindingKeys();
            UpdateKeyBindingTexts();
            Plugin.PluginLog.LogInfo("Waiting for a new push-to-talk hotkey.");
            return true;
        }
        return false;
    }

    private static void ApplyKeyBinding(KeyCode key)
    {
        if (_keyBindingTarget == 1)
        {
            var previous = Plugin.TextInputKey.Value;
            if (key == Plugin.VoiceInputKey.Value)
                Plugin.VoiceInputKey.Value = previous;
            Plugin.TextInputKey.Value = key;
            Plugin.PluginLog.LogInfo($"Text input hotkey changed to {key}.");
        }
        else if (_keyBindingTarget == 2)
        {
            var previous = Plugin.VoiceInputKey.Value;
            if (key == Plugin.TextInputKey.Value)
                Plugin.TextInputKey.Value = previous;
            Plugin.VoiceInputKey.Value = key;
            Plugin.PluginLog.LogInfo($"Push-to-talk hotkey changed to {key}.");
        }
        _keyBindingTarget = 0;
        _keyBindingStartedAt = -1f;
        RebindingHeldVirtualKeys.Clear();
        _textInputKeyWasDown = IsKeyCurrentlyDown(Plugin.TextInputKey.Value);
        VoiceInputService.KeyWasDown = IsKeyCurrentlyDown(Plugin.VoiceInputKey.Value);
        UpdateKeyBindingTexts();
    }

    private static void UpdateKeyBindingTexts()
    {
        if (_textInputKeyRow != null)
        {
            var label = FindFirstText(_textInputKeyRow.transform);
            if (label != null && label != _textInputKeyValue)
                label.text = ApiKeyText("文字輸入按鍵", "文字输入按键", "文字入力キー", "Text input key");
        }
        if (_voiceInputKeyRow != null)
        {
            var label = FindFirstText(_voiceInputKeyRow.transform);
            if (label != null && label != _voiceInputKeyValue)
                label.text = ApiKeyText("按住說話按鍵", "按住说话按键", "長押し会話キー", "Push-to-talk key");
        }
        if (_textInputKeyValue != null)
            _textInputKeyValue.text = _keyBindingTarget == 1
                ? ApiKeyText("按任意鍵…", "按任意键…", "キーを押す…", "Press a key…")
                : FormatKeyCode(Plugin.TextInputKey.Value);
        if (_voiceInputKeyValue != null)
            _voiceInputKeyValue.text = _keyBindingTarget == 2
                ? ApiKeyText("按任意鍵…", "按任意键…", "キーを押す…", "Press a key…")
                : FormatKeyCode(Plugin.VoiceInputKey.Value);
        if (_textInputKeyButton != null && _textInputKeyButton.IsOn)
            _textInputKeyButton.SetValue(false, false);
        if (_voiceInputKeyButton != null && _voiceInputKeyButton.IsOn)
            _voiceInputKeyButton.SetValue(false, false);
    }

    private static string FormatKeyCode(KeyCode key)
    {
        var name = key.ToString();
        if (name.StartsWith("Alpha", StringComparison.Ordinal) && name.Length == 6)
            return name.Substring(5);
        return name
            .Replace("LeftControl", "L Ctrl", StringComparison.Ordinal)
            .Replace("RightControl", "R Ctrl", StringComparison.Ordinal)
            .Replace("LeftShift", "L Shift", StringComparison.Ordinal)
            .Replace("RightShift", "R Shift", StringComparison.Ordinal)
            .Replace("LeftAlt", "L Alt", StringComparison.Ordinal)
            .Replace("RightAlt", "R Alt", StringComparison.Ordinal)
            .Replace("Keypad", "Num ", StringComparison.Ordinal);
    }

    private static void ResetKeyBindingUiReferences()
    {
        _textInputKeyRow = null;
        _voiceInputKeyRow = null;
        _textInputKeyButton = null;
        _voiceInputKeyButton = null;
        _textInputKeyButtonRect = null;
        _voiceInputKeyButtonRect = null;
        _textInputKeyValue = null;
        _voiceInputKeyValue = null;
        _keyBindingTarget = 0;
        _keyBindingStartedAt = -1f;
        RebindingHeldVirtualKeys.Clear();
    }
}
