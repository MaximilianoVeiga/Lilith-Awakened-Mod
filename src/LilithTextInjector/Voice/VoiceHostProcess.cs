namespace LilithTextInjector;

// Lifecycle of the bundled local GPT-SoVITS voice service.
internal static class VoiceHostProcess
{
    private static bool _launchAttempted;
    private static Process? _process;
    private static bool? _japaneseMode;
    private static float _restartAt;

    internal static void EnsureRunning(bool useJapanese)
    {
        if (Time.unscaledTime < 2f)
            return;

        if (_process != null)
        {
            try
            {
                if (_process.HasExited)
                {
                    _process.Dispose();
                    _process = null;
                    _japaneseMode = null;
                    _launchAttempted = false;
                }
                else if (_japaneseMode.HasValue && _japaneseMode.Value != useJapanese)
                {
                    Stop();
                    _restartAt = Time.unscaledTime + 1f;
                    Plugin.PluginLog.LogInfo($"Voice language changed to {(useJapanese ? "Japanese" : "Chinese")}; restarting the single local voice service.");
                    return;
                }
            }
            catch
            {
                Stop();
            }
        }

        if (Time.unscaledTime < _restartAt || _launchAttempted)
            return;

        if (!Plugin.VoiceEnabled.Value || !Plugin.VoiceAutoStartLocalService.Value)
        {
            Stop();
            return;
        }

        try
        {
            var endpoint = (useJapanese ? Plugin.JapaneseVoiceEndpoint.Value : Plugin.VoiceEndpoint.Value).Trim();
            if (!endpoint.StartsWith("http://127.0.0.1:", StringComparison.OrdinalIgnoreCase))
                return;
            _launchAttempted = true;
            var hostPath = Environment.ExpandEnvironmentVariables(Plugin.VoiceHostPath.Value.Trim());
            if (!File.Exists(hostPath))
            {
                Plugin.PluginLog.LogInfo("Bundled local voice service is not installed; dynamic voice remains available through configured external endpoints.");
                return;
            }
            var startInfo = new ProcessStartInfo
            {
                FileName = hostPath,
                Arguments = $"--voice-host --parent {Environment.ProcessId} --language {(useJapanese ? "ja" : "zh")}",
                WorkingDirectory = Path.GetDirectoryName(hostPath) ?? Paths.GameRootPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            var bundledDotnet = Path.Combine(Paths.GameRootPath, "dotnet");
            if (Directory.Exists(bundledDotnet))
                startInfo.Environment["DOTNET_ROOT"] = bundledDotnet;
            _process = Process.Start(startInfo);
            _japaneseMode = useJapanese;
            Plugin.PluginLog.LogInfo($"Started the bundled local {(useJapanese ? "Japanese" : "Chinese")} voice host without a console window.");
        }
        catch (Exception exception)
        {
            _launchAttempted = false;
            Plugin.PluginLog.LogWarning($"Could not start the bundled local voice host: {exception.Message}");
        }
    }

    internal static void Stop()
    {
        var process = _process;
        _process = null;
        _japaneseMode = null;
        _launchAttempted = false;
        if (process == null)
            return;

        try
        {
            if (!process.HasExited)
                process.Kill(true);
        }
        catch (Exception exception)
        {
            Plugin.PluginLog.LogWarning($"Could not stop the previous local voice host: {exception.Message}");
        }
        finally
        {
            process.Dispose();
        }
    }
}
