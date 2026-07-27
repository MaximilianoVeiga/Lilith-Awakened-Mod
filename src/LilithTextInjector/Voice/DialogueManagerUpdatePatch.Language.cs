namespace LilithTextInjector;

// Japanese/Chinese voice language preference and its in-game toggle.
internal static partial class DialogueManagerUpdatePatch
{
    private static void EnsureJapaneseVoiceOptionVisible()
    {
        if (!_voicePreferenceInitialized)
        {
            _voicePreferenceInitialized = true;
            _japaneseVoiceOverride = Plugin.JapaneseVoiceSelected.Value;
            Plugin.PluginLog.LogInfo($"Restored saved voice preference: {(Plugin.JapaneseVoiceSelected.Value ? "Japanese" : "Chinese/default")}.");
        }
        if (Time.unscaledTime < _nextJapaneseVoiceUiScanAt)
            return;
        _nextJapaneseVoiceUiScanAt = Time.unscaledTime + 1f;
        try
        {
            var japaneseButtonIsPressed = false;
            foreach (var button in Resources.FindObjectsOfTypeAll<ButtonPressedSwapSprite>())
            {
                if (button == null || button.gameObject == null)
                    continue;
                var path = GetTransformPath(button.transform);
                if (path.IndexOf("voice", StringComparison.OrdinalIgnoreCase) < 0
                    && path.IndexOf("語音", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                if (LoggedVoiceUiObjects.Add(path))
                    Plugin.PluginLog.LogInfo($"Voice setting button discovered: {path}, active={button.gameObject.activeSelf}.");
                var isJapaneseButton = Regex.IsMatch(button.gameObject.name, "(^|[_ -])(jp|ja|japan|japanese)($|[_ -])", RegexOptions.IgnoreCase)
                    || (path.IndexOf("/gameVoice/Buttons/", StringComparison.OrdinalIgnoreCase) >= 0
                        && string.Equals(button.gameObject.name, "Toggle_Right (2)", StringComparison.Ordinal));
                if (isJapaneseButton)
                {
                    if (!button.gameObject.activeSelf)
                    {
                        button.gameObject.SetActive(true);
                        Plugin.PluginLog.LogInfo($"Enabled existing Japanese voice setting button: {path}.");
                    }
                    if (Plugin.JapaneseVoiceSelected.Value && !_voicePreferenceAppliedToNativeUi)
                    {
                        if (PublishJapaneseVoiceSelection())
                        {
                            _voicePreferenceAppliedToNativeUi = true;
                            _nextJapaneseVoiceToggleRestoreAt = Time.unscaledTime + 3f;
                        }
                    }
                    var isPressed = button.IsPressed;
                    japaneseButtonIsPressed |= isPressed;
                }
            }

            if (Plugin.JapaneseVoiceSelected.Value
                && _japaneseVoiceOverride == true
                && !japaneseButtonIsPressed
                && Time.unscaledTime >= _nextJapaneseVoiceToggleRestoreAt)
            {
                _nextJapaneseVoiceToggleRestoreAt = Time.unscaledTime + 3f;
                if (PublishJapaneseVoiceSelection())
                    _voicePreferenceAppliedToNativeUi = true;
            }
        }
        catch (Exception exception)
        {
            Plugin.PluginLog.LogWarning($"Could not inspect Japanese voice setting UI: {exception.Message}");
        }
    }

    internal static void NotifyVoiceSettingsButtonClicked(ButtonPressedSwapSprite button)
    {
        try
        {
            if (button == null || button.gameObject == null || !button.IsPressed)
                return;

            var path = GetTransformPath(button.transform);
            if (path.IndexOf("/gameVoice/Buttons/", StringComparison.OrdinalIgnoreCase) < 0)
                return;

            var isJapaneseButton = Regex.IsMatch(button.gameObject.name, "(^|[_ -])(jp|ja|japan|japanese)($|[_ -])", RegexOptions.IgnoreCase)
                || string.Equals(button.gameObject.name, "Toggle_Right (2)", StringComparison.Ordinal);
            var isChineseButton = string.Equals(button.gameObject.name, "Toggle_Right", StringComparison.Ordinal);
            if (!isJapaneseButton && !isChineseButton)
                return;

            var japanese = isJapaneseButton;
            _japaneseVoiceOverride = japanese;
            Plugin.JapaneseVoiceSelected.Value = japanese;
            _voicePreferenceAppliedToNativeUi = true;
            _nextJapaneseVoiceToggleRestoreAt = japanese
                ? Time.unscaledTime + 3f
                : float.PositiveInfinity;
            Plugin.PluginLog.LogInfo(japanese
                ? "Japanese voice preference saved from a direct settings button click."
                : "Chinese/default voice preference saved from a direct settings button click; pending Japanese restoration cancelled.");
            EnsureNativeVoiceManifestLoaded();
        }
        catch (Exception exception)
        {
            Plugin.PluginLog.LogWarning($"Could not save the clicked voice setting: {exception.Message}");
        }
    }

    private static bool PublishJapaneseVoiceSelection()
    {
        try
        {
            var controllerMethod = typeof(TraySettingController).GetMethod(
                "OnGameVoiceChanged",
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance)
                ?? throw new MissingMethodException("TraySettingController.OnGameVoiceChanged was not found.");
            var voiceType = controllerMethod.GetParameters()[0].ParameterType;
            var japanese = voiceType.GetField(
                "Japanese",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null)
                ?? throw new MissingFieldException("Japanese voice enum value was not found.");

            var toggleMethod = typeof(TraySettingGameVoiceToggleButtons).GetMethod(
                "SetVoiceWithoutNotify",
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance)
                ?? throw new MissingMethodException("TraySettingGameVoiceToggleButtons.SetVoiceWithoutNotify was not found.");
            var toggleGroups = Resources.FindObjectsOfTypeAll<TraySettingGameVoiceToggleButtons>();
            if (toggleGroups == null || toggleGroups.Length == 0)
                throw new InvalidOperationException("Game voice toggle group was not found.");
            var synchronizedToggleGroups = 0;
            foreach (var toggleGroup in toggleGroups)
            {
                if (toggleGroup == null)
                    continue;
                toggleMethod.Invoke(toggleGroup, new[] { japanese });
                synchronizedToggleGroups++;
            }
            if (synchronizedToggleGroups == 0)
                throw new InvalidOperationException("No game voice toggle group could be synchronized.");

            var controllers = Resources.FindObjectsOfTypeAll<TraySettingController>();
            if (controllers == null || controllers.Length == 0)
                throw new InvalidOperationException("Active TraySettingController was not found.");
            controllerMethod.Invoke(controllers[0], new[] { japanese });
            Plugin.PluginLog.LogInfo($"Restored Japanese game voice and synchronized {synchronizedToggleGroups} native toggle group(s).");
            return true;
        }
        catch (Exception exception)
        {
            Plugin.PluginLog.LogWarning($"Could not publish Japanese game voice selection: {exception.Message}");
            return false;
        }
    }

    private static string GetTransformPath(Transform? transform)
    {
        var names = new List<string>();
        while (transform != null)
        {
            names.Add(transform.name);
            transform = transform.parent;
        }
        names.Reverse();
        return string.Join("/", names);
    }

    private static void ObserveVoiceLanguageSelection()
    {
        try
        {
            var language = LocalizationConfig.GetCurrentVoiceLanguage() ?? string.Empty;
            if (string.Equals(language, _lastObservedVoiceLanguage, StringComparison.OrdinalIgnoreCase))
                return;
            _lastObservedVoiceLanguage = language;
            Plugin.PluginLog.LogInfo($"Game voice language changed to '{language}'; selecting {(language.StartsWith("ja", StringComparison.OrdinalIgnoreCase) ? "Japanese" : "Chinese/default")} supplemental voice pack.");
            EnsureNativeVoiceManifestLoaded();
        }
        catch (Exception exception)
        {
            Plugin.PluginLog.LogWarning($"Could not observe game voice language: {exception.Message}");
        }
    }

    private static bool IsJapaneseVoiceMode()
    {
        if (_japaneseVoiceOverride.HasValue)
            return _japaneseVoiceOverride.Value;
        try
        {
            var language = LocalizationConfig.GetCurrentVoiceLanguage() ?? string.Empty;
            return language.StartsWith("ja", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
