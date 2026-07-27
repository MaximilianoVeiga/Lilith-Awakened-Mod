namespace LilithModInstaller;

// Runs the bundled GPT-SoVITS services when the installer is launched with
// --voice-host. Mirrors src/LilithVoiceHost/Program.cs; keep the two in sync.
internal static class VoiceHost
{
    internal static async Task RunAsync(int parentPid)
    {
        using var mutex = new Mutex(true, "Local\\LilithAIVoiceHost", out var created);
        if (!created) return;
        var root = AppContext.BaseDirectory;
        Directory.CreateDirectory(Path.Combine(root, "logs"));
        var log = Path.Combine(root, "logs", "voice-host.log");
        var owned = new List<Process>();
        try
        {
            var python = Path.Combine(root, "python", "Scripts", "python.exe");
            var api = Path.Combine(root, "gpt-sovits", "api_v2.py");
            if (!File.Exists(python) || !File.Exists(api) || !File.Exists(Path.Combine(root, ".ready")))
            {
                await LogAsync(log, "Voice runtime is not ready.");
                return;
            }
            var device = File.Exists(Path.Combine(root, "device.txt"))
                ? File.ReadAllText(Path.Combine(root, "device.txt")).Trim().ToLowerInvariant()
                : HasNvidiaGpu() ? "cuda" : "cpu";
            foreach (var service in new[] { (Port: 9880, Name: "zh"), (Port: 9881, Name: "ja") })
            {
                if (await PortOpenAsync(service.Port, 300)) continue;
                var config = Path.Combine(root, "config", $"{service.Name}-{device}.yaml");
                if (!File.Exists(config)) throw new FileNotFoundException("Voice configuration is missing.", config);
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = python,
                    Arguments = $"\"{api}\" -a 127.0.0.1 -p {service.Port} -c \"{config}\"",
                    WorkingDirectory = Path.Combine(root, "gpt-sovits"),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }) ?? throw new InvalidOperationException("Could not start the voice service.");
                process.OutputDataReceived += async (_, e) => { if (e.Data != null) await LogAsync(log, $"[{service.Name}] {e.Data}"); };
                process.ErrorDataReceived += async (_, e) => { if (e.Data != null) await LogAsync(log, $"[{service.Name}:err] {e.Data}"); };
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                owned.Add(process);
            }
            await LogAsync(log, $"Voice host started ({device}); owned processes={owned.Count}.");
            while (parentPid > 0)
            {
                try
                {
                    using var parent = Process.GetProcessById(parentPid);
                    if (parent.HasExited) break;
                }
                catch { break; }
                await Task.Delay(2000);
            }
        }
        catch (Exception exception)
        {
            await LogAsync(log, exception.ToString());
        }
        finally
        {
            foreach (var process in owned)
            {
                try { if (!process.HasExited) process.Kill(true); } catch { }
                process.Dispose();
            }
            await LogAsync(log, "Voice host stopped.");
        }
    }

    internal static bool HasNvidiaGpu()
    {
        foreach (var candidate in new[]
                 {
                     "nvidia-smi.exe",
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "NVIDIA Corporation", "NVSMI", "nvidia-smi.exe")
                 })
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo(candidate, "-L")
                {
                    UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
                });
                if (process == null) continue;
                if (process.WaitForExit(3000) && process.ExitCode == 0 && process.StandardOutput.ReadToEnd().Contains("NVIDIA", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            catch { }
        }
        return false;
    }

    private static async Task<bool> PortOpenAsync(int port, int timeoutMs)
    {
        try
        {
            using var client = new TcpClient();
            using var timeout = new CancellationTokenSource(timeoutMs);
            await client.ConnectAsync("127.0.0.1", port, timeout.Token);
            return true;
        }
        catch { return false; }
    }

    private static readonly SemaphoreSlim LogLock = new(1, 1);
    private static async Task LogAsync(string path, string message)
    {
        await LogLock.WaitAsync();
        try { await File.AppendAllTextAsync(path, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}", new UTF8Encoding(false)); }
        finally { LogLock.Release(); }
    }
}
