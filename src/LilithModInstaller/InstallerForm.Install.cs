namespace LilithModInstaller;

// Install and uninstall flows, including BepInEx console suppression.
internal sealed partial class InstallerForm
{
    private async Task InitializeInstallerAsync()
    {
        var manifestReady = false;
        SetBusy(true);
        try
        {
            var manifestPath = Path.Combine(_baseDirectory, "release-manifest.json");
            string manifestJson;
            if (File.Exists(manifestPath))
            {
                manifestJson = await File.ReadAllTextAsync(manifestPath);
            }
            else
            {
                SetStatus(L("正在取得最新發佈資訊…", "正在获取最新发布信息…", "最新のリリース情報を取得中…", "Retrieving the latest release information…"));
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                manifestJson = await client.GetStringAsync(DefaultManifestUrl);
            }

            _manifest = JsonSerializer.Deserialize<ReleaseManifest>(manifestJson, JsonOptions()) ?? new ReleaseManifest();
            if (_manifest.Packages.Count == 0)
                throw new InvalidDataException("The release manifest does not contain any installable packages.");
            manifestReady = true;
        }
        catch (Exception exception)
        {
            SetStatus(L("無法讀取發佈資訊：", "无法读取发布信息：", "リリース情報を読み込めません：", "Could not read release information: ") + exception.Message);
        }
        finally
        {
            _path.Text = SteamLocator.FindGameDirectory() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(_path.Text))
                SetStatus(L("未自動找到遊戲，請按「瀏覽」。", "未自动找到游戏，请点击“浏览”。", "ゲームが見つかりません。［参照］で選択してください。", "Game not found automatically. Please use Browse."));
            else if (manifestReady)
                SetStatus(L("準備就緒", "准备就绪", "準備完了", "Ready"));
            SetBusy(false);
            _install.Enabled = manifestReady;
        }
    }

    private async Task InstallAsync()
    {
        var game = Path.GetFullPath(_path.Text.Trim());
        if (!SteamLocator.IsGameDirectory(game))
        {
            MessageBox.Show(this, L("請選擇正確的遊戲資料夾（必須包含 Lilith.exe）。", "请选择正确的游戏文件夹（必须包含 Lilith.exe）。", "正しいゲームフォルダー（Lilith.exeを含む）を選択してください。", "Select the correct game folder containing Lilith.exe."), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (Process.GetProcessesByName("Lilith").Length > 0)
        {
            MessageBox.Show(this, L("請先關閉莉莉絲桌寵，再重新按安裝。", "请先关闭莉莉丝桌宠，再重新点击安装。", "リリスを終了してから、もう一度インストールしてください。", "Close Lilith before installing, then try again."), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        SetBusy(true);
        var installed = new InstalledManifest { Version = _manifest.Version, InstalledAt = DateTimeOffset.Now };
        try
        {
            var selections = new List<string> { "core" };
            if (_voicePack.Checked) selections.Add("voicePack");
            if (_dynamicVoice.Checked) selections.Add("voiceRuntime");
            var step = 0;
            foreach (var name in selections)
            {
                step++;
                _progress.Value = Math.Min(90, (step - 1) * 80 / Math.Max(1, selections.Count));
                var package = await AcquirePackageAsync(name);
                SetStatus(string.Format(L("正在安裝 {0}…", "正在安装 {0}…", "{0} をインストール中…", "Installing {0}…"), name));
                installed.Files[name] = ExtractPackage(package, game);
            }

            DisableBepInExConsole(game);

            if (_dynamicVoice.Checked)
            {
                var runtime = Path.Combine(game, "BepInEx", "data", "LilithTextInjector", "voice-runtime");
                Directory.CreateDirectory(runtime);
                if (!File.Exists(Path.Combine(runtime, "LilithVoiceHost.exe")))
                    throw new FileNotFoundException("The dynamic voice package does not contain LilithVoiceHost.exe.");
                await PrepareVoiceRuntimeAsync(runtime);
            }

            var manifestDirectory = Path.Combine(game, "BepInEx", "data", "LilithTextInjector");
            Directory.CreateDirectory(manifestDirectory);
            File.WriteAllText(Path.Combine(manifestDirectory, "installed-files.json"), JsonSerializer.Serialize(installed, JsonOptions(true)), new UTF8Encoding(false));
            _progress.Value = 100;
            SetStatus(L("安裝完成。API Key 請在左下角莉莉絲選單中由玩家自行輸入。", "安装完成。API Key 请在左下角莉莉丝菜单中由玩家自行输入。", "インストール完了。APIキーは左下のリリスメニューから入力してください。", "Installation complete. Enter your own API key from Lilith's lower-left tray menu."));
            if (_launch.Checked)
                Process.Start(new ProcessStartInfo(Path.Combine(game, "Lilith.exe")) { WorkingDirectory = game, UseShellExecute = true });
        }
        catch (UnauthorizedAccessException)
        {
            SetStatus(L("需要系統管理員權限，正在重新開啟安裝程式…", "需要管理员权限，正在重新打开安装程序…", "管理者権限で再起動します…", "Restarting the installer with administrator privileges…"));
            RestartElevated();
            Close();
        }
        catch (Exception exception)
        {
            _progress.Value = 0;
            SetStatus(L("安裝失敗：", "安装失败：", "インストール失敗：", "Installation failed: ") + exception.Message);
            MessageBox.Show(this, exception.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private static void DisableBepInExConsole(string game)
    {
        var configDirectory = Path.Combine(game, "BepInEx", "config");
        Directory.CreateDirectory(configDirectory);
        var path = Path.Combine(configDirectory, "BepInEx.cfg");
        var lines = File.Exists(path)
            ? File.ReadAllLines(path).ToList()
            : new List<string>();

        var sectionIndex = lines.FindIndex(line =>
            string.Equals(line.Trim(), "[Logging.Console]", StringComparison.OrdinalIgnoreCase));
        if (sectionIndex < 0)
        {
            if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[^1])) lines.Add(string.Empty);
            lines.Add("[Logging.Console]");
            lines.Add(string.Empty);
            lines.Add("Enabled = false");
        }
        else
        {
            var nextSection = lines.FindIndex(sectionIndex + 1, line =>
                line.TrimStart().StartsWith("[", StringComparison.Ordinal));
            if (nextSection < 0) nextSection = lines.Count;
            var enabledIndex = -1;
            for (var index = sectionIndex + 1; index < nextSection; index++)
            {
                if (Regex.IsMatch(lines[index], @"^\s*Enabled\s*=", RegexOptions.IgnoreCase))
                {
                    enabledIndex = index;
                    break;
                }
            }
            if (enabledIndex >= 0)
                lines[enabledIndex] = "Enabled = false";
            else
                lines.Insert(sectionIndex + 1, "Enabled = false");
        }

        File.WriteAllLines(path, lines, new UTF8Encoding(false));
    }

    private async Task UninstallAsync()
    {
        var game = Path.GetFullPath(_path.Text.Trim());
        if (!SteamLocator.IsGameDirectory(game)) return;
        var answer = MessageBox.Show(this,
            L("移除 MOD 檔案？API Key、聊天記憶與玩家設定會保留。", "移除 MOD 文件？API Key、聊天记忆和玩家设置将会保留。", "MODを削除しますか？APIキー、会話履歴、設定は保持されます。", "Remove mod files? API keys, chat memory, and player settings will be preserved."),
            Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (answer != DialogResult.Yes) return;
        SetBusy(true);
        try
        {
            var manifestPath = Path.Combine(game, "BepInEx", "data", "LilithTextInjector", "installed-files.json");
            var installed = File.Exists(manifestPath)
                ? JsonSerializer.Deserialize<InstalledManifest>(File.ReadAllText(manifestPath), JsonOptions())
                : null;
            if (installed != null)
            {
                foreach (var relative in installed.Files.SelectMany(pair => pair.Value).Distinct(StringComparer.OrdinalIgnoreCase).OrderByDescending(value => value.Length))
                {
                    if (IsSharedLoaderFile(relative)) continue;
                    var full = Path.GetFullPath(Path.Combine(game, relative));
                    if (full.StartsWith(game + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && File.Exists(full))
                        File.Delete(full);
                }
            }
            else
            {
                foreach (var name in new[] { "LilithTextInjector.dll", "NAudio.dll", "NAudio.Core.dll", "NAudio.Wasapi.dll" })
                {
                    var path = Path.Combine(game, "BepInEx", "plugins", name);
                    if (File.Exists(path)) File.Delete(path);
                }
            }
            if (File.Exists(manifestPath)) File.Delete(manifestPath);
            _progress.Value = 100;
            SetStatus(L("MOD 已移除；個人設定與 API Key 已保留。", "MOD 已移除；个人设置和 API Key 已保留。", "MODを削除しました。個人設定とAPIキーは保持されています。", "Mod removed; personal settings and API keys were preserved."));
        }
        catch (Exception exception)
        {
            SetStatus(L("移除失敗：", "移除失败：", "削除失敗：", "Removal failed: ") + exception.Message);
        }
        finally { SetBusy(false); }
        await Task.CompletedTask;
    }
}
