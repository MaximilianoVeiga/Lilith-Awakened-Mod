namespace LilithModInstaller;

// Installer window: layout, localisation and shared UI state.
internal sealed partial class InstallerForm : Form
{
    private const string AppId = "4643090";
    private const string DefaultManifestUrl = "https://github.com/mimimi6666/Lilith-AI-Mod/releases/download/v0.1.1-rc2/release-manifest.json";
    private readonly bool _zhTraditional;
    private readonly bool _zhSimplified;
    private readonly bool _japanese;
    private readonly TextBox _path = new();
    private readonly Button _browse = new();
    private readonly CheckBox _core = new();
    private readonly CheckBox _voicePack = new();
    private readonly CheckBox _dynamicVoice = new();
    private readonly CheckBox _launch = new();
    private readonly ProgressBar _progress = new();
    private readonly Label _status = new();
    private readonly Button _install = new();
    private readonly Button _uninstall = new();
    private readonly Button _close = new();
    private readonly string _baseDirectory = AppContext.BaseDirectory;
    private ReleaseManifest _manifest = new();

    internal InstallerForm()
    {
        var culture = CultureInfo.CurrentUICulture.Name;
        _zhSimplified = culture.StartsWith("zh-CN", StringComparison.OrdinalIgnoreCase)
            || culture.StartsWith("zh-SG", StringComparison.OrdinalIgnoreCase)
            || culture.StartsWith("zh-Hans", StringComparison.OrdinalIgnoreCase);
        _zhTraditional = !_zhSimplified && culture.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
        _japanese = culture.StartsWith("ja", StringComparison.OrdinalIgnoreCase);

        Text = L("莉莉絲 AI MOD 一鍵安裝程式", "莉莉丝 AI MOD 一键安装程序", "リリス AI MOD セットアップ", "Lilith AI Mod Setup");
        Font = new Font("Segoe UI", 10f);
        ClientSize = new Size(680, 485);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;

        var title = new Label
        {
            Text = L("讓莉莉絲能聊天、聽你說話，並使用中日語音。", "让莉莉丝能聊天、听你说话，并使用中日语音。", "リリスとの会話・音声入力・中国語／日本語音声を追加します。", "Adds AI chat, voice input, and Chinese/Japanese voices to Lilith."),
            Font = new Font(Font, FontStyle.Bold),
            AutoSize = false,
            Bounds = new Rectangle(24, 20, 630, 28)
        };
        Controls.Add(title);

        Controls.Add(new Label { Text = L("遊戲資料夾", "游戏文件夹", "ゲームフォルダー", "Game folder"), Bounds = new Rectangle(24, 66, 630, 24) });
        _path.Bounds = new Rectangle(24, 92, 535, 30);
        _browse.Text = L("瀏覽…", "浏览…", "参照…", "Browse…");
        _browse.Bounds = new Rectangle(570, 91, 84, 31);
        _browse.Click += (_, _) => BrowseForGame();
        Controls.Add(_path);
        Controls.Add(_browse);

        _core.Text = L("核心 MOD 與 BepInEx（必要）", "核心 MOD 与 BepInEx（必需）", "コアMODとBepInEx（必須）", "Core mod and BepInEx (required)");
        _core.Checked = true;
        _core.Enabled = false;
        _core.Bounds = new Rectangle(32, 145, 610, 28);
        _voicePack.Text = L("完整中／日文補充台詞語音（約 301 MB）", "完整中／日文补充台词语音（约 301 MB）", "中国語／日本語の追加台詞音声（約301 MB）", "Complete Chinese/Japanese supplemental dialogue voices (~301 MB)");
        _voicePack.Checked = true;
        _voicePack.Bounds = new Rectangle(32, 180, 610, 28);
        _dynamicVoice.Text = L("AI 動態語音（模型約 1.98 GB；首次會自動建立推理環境）", "AI 动态语音（模型约 1.98 GB；首次会自动建立推理环境）", "AI動的音声（モデル約1.98 GB・初回に推論環境を自動構築）", "Dynamic AI voice (~1.98 GB models; builds its inference environment on first install)");
        _dynamicVoice.Checked = true;
        _dynamicVoice.Bounds = new Rectangle(32, 215, 620, 28);
        _launch.Text = L("安裝完成後啟動桌寵", "安装完成后启动桌宠", "完了後にデスクトップペットを起動", "Launch the desktop pet after installation");
        _launch.Checked = true;
        _launch.Bounds = new Rectangle(32, 250, 610, 28);
        Controls.AddRange([_core, _voicePack, _dynamicVoice, _launch]);

        var privacy = new Label
        {
            Text = L("隱私：安裝包不含任何 API Key、聊天紀錄或開發者電腦路徑；更新時也不會覆蓋玩家資料。", "隐私：安装包不含任何 API Key、聊天记录或开发者电脑路径；更新时也不会覆盖玩家数据。", "プライバシー：APIキー、会話履歴、開発PCのパスは含まれず、更新時も個人データを上書きしません。", "Privacy: no API keys, chat history, or developer paths are included; upgrades preserve player data."),
            AutoSize = false,
            ForeColor = Color.DimGray,
            Bounds = new Rectangle(24, 292, 630, 48)
        };
        Controls.Add(privacy);

        _progress.Bounds = new Rectangle(24, 350, 630, 20);
        _status.Text = L("準備就緒", "准备就绪", "準備完了", "Ready");
        _status.AutoEllipsis = true;
        _status.Bounds = new Rectangle(24, 378, 630, 28);
        Controls.Add(_progress);
        Controls.Add(_status);

        _install.Text = L("安裝／更新", "安装／更新", "インストール／更新", "Install / Update");
        _uninstall.Text = L("移除 MOD", "移除 MOD", "MODを削除", "Remove mod");
        _close.Text = L("關閉", "关闭", "閉じる", "Close");
        _install.Bounds = new Rectangle(326, 425, 112, 36);
        _uninstall.Bounds = new Rectangle(446, 425, 100, 36);
        _close.Bounds = new Rectangle(554, 425, 100, 36);
        _install.Click += async (_, _) => await InstallAsync();
        _uninstall.Click += async (_, _) => await UninstallAsync();
        _close.Click += (_, _) => Close();
        Controls.AddRange([_install, _uninstall, _close]);

        Load += async (_, _) => await InitializeInstallerAsync();
    }

    private string L(string zhTw, string zhCn, string ja, string en)
        => _zhTraditional ? zhTw : _zhSimplified ? zhCn : _japanese ? ja : en;

    private void BrowseForGame()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = L("選擇包含 Lilith.exe 的遊戲資料夾", "选择包含 Lilith.exe 的游戏文件夹", "Lilith.exe があるゲームフォルダーを選択", "Select the game folder containing Lilith.exe"),
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
            InitialDirectory = Directory.Exists(_path.Text) ? _path.Text : Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            _path.Text = dialog.SelectedPath;
    }

    private void SetBusy(bool busy)
    {
        _install.Enabled = !busy;
        _uninstall.Enabled = !busy;
        _browse.Enabled = !busy;
        _close.Enabled = !busy;
        UseWaitCursor = busy;
    }

    private void SetStatus(string text)
    {
        _status.Text = text;
        _status.Refresh();
    }

    private static JsonSerializerOptions JsonOptions(bool indented = false) => new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = indented
    };

    private void RestartElevated()
    {
        var executable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executable)) return;
        try
        {
            Process.Start(new ProcessStartInfo(executable) { UseShellExecute = true, Verb = "runas" });
        }
        catch { }
    }
}
