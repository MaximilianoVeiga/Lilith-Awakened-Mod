namespace LilithTextInjector;

// Foreground window tracking, local timers and deferred system actions.
internal static partial class DialogueManagerUpdatePatch
{
    private sealed class LocalTimer
    {
        public DateTimeOffset DueAt { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    private sealed class PendingSystemAction
    {
        public string Action { get; set; } = string.Empty;
        public float ExecuteAfter { get; set; }
    }

    private static void CaptureHeldRebindingKeys()
    {
        RebindingHeldVirtualKeys.Clear();
        for (var code = (int)KeyCode.Backspace; code < (int)KeyCode.Mouse0; code++)
        {
            if (TryGetWindowsVirtualKey((KeyCode)code, out var virtualKey) && IsVirtualKeyDown(virtualKey))
                RebindingHeldVirtualKeys.Add(virtualKey);
        }
    }

    private static void ObserveForegroundWindow()
    {
        if (!OperatingSystem.IsWindows() || Time.unscaledTime < _nextForegroundWindowScanAt)
            return;
        _nextForegroundWindowScanAt = Time.unscaledTime + 0.25f;
        var window = GetForegroundWindow();
        if (window == IntPtr.Zero || !IsWindowVisible(window))
            return;
        GetWindowThreadProcessId(window, out var processId);
        if (processId != (uint)Environment.ProcessId)
            _lastExternalForegroundWindow = window;
    }

    internal static IntPtr GetControllableWindow()
    {
        var foreground = GetForegroundWindow();
        if (foreground != IntPtr.Zero && IsWindowVisible(foreground))
        {
            GetWindowThreadProcessId(foreground, out var processId);
            if (processId != (uint)Environment.ProcessId)
                return foreground;
        }
        if (_lastExternalForegroundWindow != IntPtr.Zero && IsWindowVisible(_lastExternalForegroundWindow))
            return _lastExternalForegroundWindow;
        return IntPtr.Zero;
    }

    private static void ProcessLocalTimers(DialogueManager manager)
    {
        if (LocalTimers.Count == 0 || manager.IsBusy || _requestInFlight)
            return;
        var now = DateTimeOffset.Now;
        var due = LocalTimers.Where(timer => timer.DueAt <= now).OrderBy(timer => timer.DueAt).ToList();
        if (due.Count == 0)
            return;
        foreach (var timer in due)
            LocalTimers.Remove(timer);
        var message = due.Count == 1
            ? due[0].Message
            : ApiKeyText($"有 {due.Count} 個計時器到時間了。{due[0].Message}", $"有 {due.Count} 个计时器到时间了。{due[0].Message}", $"{due.Count}件のタイマーが時間になったよ。{due[0].Message}", $"{due.Count} timers are due. {due[0].Message}");
        manager.ForceSay(message, string.Empty, 12f);
        if (Plugin.VoiceEnabled.Value)
            _ = RequestSpeechAsync(message, poseStyle: CapturePoseContext().VoiceStyle);
        Plugin.PluginLog.LogInfo($"Delivered {due.Count} local Lilith timer(s)." );
    }

    private static void ProcessPendingSystemAction()
    {
        var pending = _pendingSystemAction;
        if (pending == null)
            return;
        if (_requestInFlight || !PendingReplies.IsEmpty)
        {
            pending.ExecuteAfter = Math.Max(pending.ExecuteAfter, Time.unscaledTime + 12f);
            return;
        }
        if (Time.unscaledTime < pending.ExecuteAfter)
            return;
        _pendingSystemAction = null;
        try
        {
            var success = pending.Action switch
            {
                "lock" => LockWorkStation(),
                "sleep" => SetSuspendState(false, false, false),
                _ => false
            };
            if (!success)
                Plugin.PluginLog.LogWarning($"Pending system action '{pending.Action}' was rejected by Windows or is unavailable on this PC.");
            else
                Plugin.PluginLog.LogInfo($"Executed explicit user-requested system action '{pending.Action}'.");
        }
        catch (Exception exception)
        {
            Plugin.PluginLog.LogWarning($"Pending system action '{pending.Action}' failed: {exception.Message}");
        }
    }
}
