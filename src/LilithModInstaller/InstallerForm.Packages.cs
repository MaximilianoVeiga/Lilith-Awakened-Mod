namespace LilithModInstaller;

// Package download, verification, extraction and voice runtime preparation.
internal sealed partial class InstallerForm
{
    private async Task<string> AcquirePackageAsync(string name)
    {
        if (!_manifest.Packages.TryGetValue(name, out var spec))
            throw new InvalidOperationException($"Package '{name}' is missing from release-manifest.json.");
        var local = Path.Combine(_baseDirectory, "packages", spec.File);
        if (!File.Exists(local))
        {
            if (string.IsNullOrWhiteSpace(spec.Url))
                throw new FileNotFoundException(L("缺少安裝元件且尚未設定下載網址：", "缺少安装组件且尚未设置下载地址：", "コンポーネントがなく、ダウンロードURLも未設定です：", "A package is missing and has no download URL: ") + spec.File);
            EnsureAllowedReleaseUrl(spec.Url);
            Directory.CreateDirectory(Path.GetDirectoryName(local)!);
            var temporary = local + ".download";
            using var client = new HttpClient { Timeout = TimeSpan.FromHours(2) };
            using var response = await client.GetAsync(spec.Url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? spec.Bytes;
            await using var source = await response.Content.ReadAsStreamAsync();
            await using var destination = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None);
            var buffer = new byte[1024 * 256];
            long received = 0;
            int read;
            while ((read = await source.ReadAsync(buffer)) > 0)
            {
                await destination.WriteAsync(buffer.AsMemory(0, read));
                received += read;
                if (total > 0) _progress.Value = Math.Clamp((int)(received * 70 / total), 0, 70);
                SetStatus(string.Format(L("正在下載 {0}：{1:0.0} MB", "正在下载 {0}：{1:0.0} MB", "{0} をダウンロード中：{1:0.0} MB", "Downloading {0}: {1:0.0} MB"), name, received / 1048576d));
            }
            File.Move(temporary, local, true);
        }
        if (!string.IsNullOrWhiteSpace(spec.Sha256))
        {
            using var stream = File.OpenRead(local);
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream));
            if (!hash.Equals(spec.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Checksum mismatch for {spec.File}.");
        }
        return local;
    }

    private static List<string> ExtractPackage(string archive, string game)
    {
        var files = new List<string>();
        using var zip = ZipFile.OpenRead(archive);
        foreach (var entry in zip.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue;
            var destination = Path.GetFullPath(Path.Combine(game, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
            if (!destination.StartsWith(game + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Package contains an unsafe path.");
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            entry.ExtractToFile(destination, true);
            files.Add(Path.GetRelativePath(game, destination));
        }
        return files;
    }

    private async Task PrepareVoiceRuntimeAsync(string runtime)
    {
        var ready = Path.Combine(runtime, ".ready");
        if (File.Exists(ready)) return;
        var uv = Path.Combine(runtime, "uv.exe");
        var requirements = Path.Combine(runtime, "requirements-inference.txt");
        if (!File.Exists(uv) || !File.Exists(requirements))
            throw new FileNotFoundException("The dynamic voice package is incomplete (uv.exe or requirements-inference.txt is missing).");
        var pythonDirectory = Path.Combine(runtime, "python");
        SetStatus(L("正在準備獨立 Python 語音環境…", "正在准备独立 Python 语音环境…", "独立Python音声環境を準備中…", "Preparing the isolated Python voice environment…"));
        await RunProcessAsync(uv, $"venv \"{pythonDirectory}\" --python 3.10 --python-preference managed --relocatable", runtime);
        var python = Path.Combine(pythonDirectory, "Scripts", "python.exe");
        var nvidia = VoiceHost.HasNvidiaGpu();
        File.WriteAllText(Path.Combine(runtime, "device.txt"), nvidia ? "cuda" : "cpu");
        var torchIndex = nvidia ? "https://download.pytorch.org/whl/cu124" : "https://download.pytorch.org/whl/cpu";
        SetStatus(nvidia
            ? L("偵測到 NVIDIA 顯示卡，正在下載 GPU 語音元件…", "检测到 NVIDIA 显卡，正在下载 GPU 语音组件…", "NVIDIA GPUを検出。GPU音声コンポーネントを取得中…", "NVIDIA GPU detected; downloading GPU voice components…")
            : L("未偵測到相容 NVIDIA 顯示卡，正在下載 CPU 語音元件…", "未检测到兼容 NVIDIA 显卡，正在下载 CPU 语音组件…", "対応NVIDIA GPUなし。CPU音声コンポーネントを取得中…", "No compatible NVIDIA GPU detected; downloading CPU voice components…"));
        await RunProcessAsync(uv, $"pip install --python \"{python}\" torch==2.6.0 torchaudio==2.6.0 --index-url {torchIndex}", runtime);
        SetStatus(L("正在安裝語音辨識與合成相依元件…", "正在安装语音识别与合成依赖组件…", "音声合成の依存コンポーネントをインストール中…", "Installing voice synthesis dependencies…"));
        await RunProcessAsync(uv, $"pip install --python \"{python}\" -r \"{requirements}\"", runtime);
        File.WriteAllText(ready, DateTimeOffset.Now.ToString("O"));
    }

    private static async Task RunProcessAsync(string file, string arguments, string workingDirectory)
    {
        var log = Path.Combine(workingDirectory, "voice-runtime-install.log");
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = file,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        process.Start();
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var output = await outputTask;
        var error = await errorTask;
        await File.AppendAllTextAsync(log, $"> {Path.GetFileName(file)} {arguments}\n{output}\n{error}\n", new UTF8Encoding(false));
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Voice dependency installer exited with code {process.ExitCode}. See {log}");
    }

    private static bool IsSharedLoaderFile(string relative)
    {
        var normalized = relative.Replace('/', '\\');
        return normalized.StartsWith("BepInEx\\core\\", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("BepInEx\\patchers\\", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("BepInEx\\unity-libs\\", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("dotnet\\", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("winhttp.dll", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("doorstop_config.ini", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals(".doorstop_version", StringComparison.OrdinalIgnoreCase);
    }
}
