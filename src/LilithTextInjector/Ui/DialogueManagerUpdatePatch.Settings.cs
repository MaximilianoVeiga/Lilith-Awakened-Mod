namespace LilithTextInjector;

// Injection of custom rows into the native settings tray.
internal static partial class DialogueManagerUpdatePatch
{
    private static void EnsureAdvancedComputerActionsToggle()
    {
        SyncInjectedSettingsVisibility();
        if (Time.unscaledTime < _nextAdvancedActionsUiScanAt)
            return;
        _nextAdvancedActionsUiScanAt = Time.unscaledTime + 0.75f;
        try
        {
            if (_advancedActionsRow != null && _advancedActionsToggle != null)
            {
                SetAdvancedActionsLabel(_advancedActionsRow.transform);
                if (_advancedActionsToggle.IsOn != Plugin.AdvancedComputerActionsEnabled.Value)
                    _advancedActionsToggle.SetValue(Plugin.AdvancedComputerActionsEnabled.Value, false);
                SyncInjectedSettingsVisibility();
                return;
            }

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

            var templateToggle = view._crossScreenDragToggle;
            var templateRow = FindSettingRow(templateToggle.transform, view.transform);
            if (templateRow == null || templateRow.parent == null)
                return;

            _settingsView = view;
            _settingsVisibilityTemplateRow = templateRow.gameObject;

            var clone = UnityEngine.Object.Instantiate(templateRow.gameObject, templateRow.parent);
            clone.name = "LilithAdvancedComputerActions";
            clone.SetActive(true);
            var clonedToggle = FindButtonToggle(clone.transform);
            if (clonedToggle == null)
            {
                UnityEngine.Object.Destroy(clone);
                return;
            }

            // A cloned ButtonToggle also clones the official row's managed callback.
            // Replace it so this control cannot accidentally change cross-screen dragging.
            clonedToggle.OnValueChanged = null;
            _advancedActionsChanged = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<bool>>(
                new System.Action<bool>(enabled =>
                {
                    Plugin.AdvancedComputerActionsEnabled.Value = enabled;
                    Plugin.PluginLog.LogInfo($"Advanced computer actions {(enabled ? "enabled" : "disabled")} by the player.");
                }));
            clonedToggle.OnValueChanged = _advancedActionsChanged;
            clonedToggle.SetValue(Plugin.AdvancedComputerActionsEnabled.Value, false);
            SetAdvancedActionsLabel(clone.transform);
            PlaceAdvancedActionsRow(templateRow, clone.transform);

            _advancedActionsRow = clone;
            _advancedActionsToggle = clonedToggle;
            SyncInjectedSettingsVisibility();
            Plugin.PluginLog.LogInfo($"Added the native-style advanced computer actions toggle at {GetTransformPath(clone.transform)} (default={Plugin.AdvancedComputerActionsEnabled.Value}).");
        }
        catch (Exception exception)
        {
            Plugin.PluginLog.LogWarning($"Could not add the advanced computer actions setting: {exception}");
            _advancedActionsRow = null;
            _advancedActionsToggle = null;
            _advancedActionsChanged = null;
        }
    }

    private static void UpdateSettingsUiSafely()
    {
        try
        {
            EnsureJapaneseVoiceOptionVisible();
        }
        catch (Exception exception)
        {
            Plugin.PluginLog.LogWarning($"Could not update the game voice setting: {exception}");
        }

        try
        {
            EnsureAdvancedComputerActionsToggle();
        }
        catch (Exception exception)
        {
            Plugin.PluginLog.LogWarning($"Could not update the advanced actions setting: {exception}");
        }

        try
        {
            EnsureKeyBindingSettings();
        }
        catch (Exception exception)
        {
            Plugin.PluginLog.LogWarning($"Could not update key binding settings: {exception}");
            ResetKeyBindingUiReferences();
        }
    }

    private static Transform? FindSettingRow(Transform start, Transform viewRoot)
    {
        var current = start;
        for (var depth = 0; current != null && current != viewRoot && depth < 6; depth++)
        {
            if (FindFirstText(current) != null)
                return current;
            current = current.parent;
        }
        return start.parent;
    }

    private static TMP_Text? FindFirstText(Transform root)
    {
        var own = root.gameObject.GetComponent<TMP_Text>();
        if (own != null)
            return own;
        for (var index = 0; index < root.childCount; index++)
        {
            var found = FindFirstText(root.GetChild(index));
            if (found != null)
                return found;
        }
        return null;
    }

    private static ButtonToggle? FindButtonToggle(Transform root)
    {
        var own = root.gameObject.GetComponent<ButtonToggle>();
        if (own != null)
            return own;
        for (var index = 0; index < root.childCount; index++)
        {
            var found = FindButtonToggle(root.GetChild(index));
            if (found != null)
                return found;
        }
        return null;
    }

    private static void SetAdvancedActionsLabel(Transform row)
    {
        var label = FindFirstText(row);
        if (label == null)
            return;
        label.text = ApiKeyText("進階電腦操作", "高级电脑操作", "高度なPC操作", "Advanced PC controls");
    }

    private static void PlaceAdvancedActionsRow(Transform templateRow, Transform clonedRow)
    {
        var parent = templateRow.parent;
        var cloneRect = clonedRow.gameObject.GetComponent<RectTransform>();
        var templateRect = templateRow.gameObject.GetComponent<RectTransform>();
        if (parent == null || cloneRect == null || templateRect == null)
            return;

        // The lower half of the settings panel is already full. Keep the cloned
        // row out of the vertical layout and use the empty right side instead.
        var layoutElement = clonedRow.gameObject.GetComponent<LayoutElement>();
        if (layoutElement == null)
            layoutElement = clonedRow.gameObject.AddComponent<LayoutElement>();
        layoutElement.ignoreLayout = true;
        clonedRow.SetSiblingIndex(parent.childCount - 1);
        cloneRect.anchoredPosition = templateRect.anchoredPosition + new Vector2(300f, 0f);
    }

    private static bool SyncInjectedSettingsVisibility()
    {
        var controlsVisible = false;
        try
        {
            controlsVisible = _settingsView != null
                && _settingsView.gameObject != null
                && _settingsView.gameObject.activeInHierarchy
                && _settingsView._currentTab == TraySettingView.TabControls;
        }
        catch
        {
            // Older game builds do not expose tabs. Retain the legacy behavior
            // there, while current builds use the official selected-tab state.
            controlsVisible = _settingsVisibilityTemplateRow != null
                && _settingsVisibilityTemplateRow.activeInHierarchy;
        }

        SetInjectedSettingsRowActive(_advancedActionsRow, controlsVisible);
        SetInjectedSettingsRowActive(_textInputKeyRow, controlsVisible);
        SetInjectedSettingsRowActive(_voiceInputKeyRow, controlsVisible);

        if (!controlsVisible && _keyBindingTarget != 0)
        {
            _keyBindingTarget = 0;
            _keyBindingStartedAt = -1f;
            RebindingHeldVirtualKeys.Clear();
            UpdateKeyBindingTexts();
            Plugin.PluginLog.LogInfo("Key rebinding cancelled because the Controls settings tab was closed.");
        }
        return controlsVisible;
    }

    private static void SetInjectedSettingsRowActive(GameObject? row, bool active)
    {
        if (row != null && row.activeSelf != active)
            row.SetActive(active);
    }
}
