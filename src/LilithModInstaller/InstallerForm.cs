namespace LilithModInstaller;

// Installer window: layout, localisation and shared UI state.
internal sealed partial class InstallerForm : Form
{
    private const string AppId = "4643090";
    private static readonly string[] AllowedReleaseOrigins =
    [
        "https://github.com/MaximilianoVeiga/Lilith-Awakened-Mod/",
        "https://github.com/MaximilianoVeiga/Lilith-Awakened-Assets/"
    ];
    private const string DefaultManifestUrl = "https://github.com/MaximilianoVeiga/Lilith-Awakened-Mod/releases/latest/download/release-manifest.json";
    private const int ContentMargin = 28;
    private const int ContentWidth = 764;
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
    private readonly ToolTip _pathTip = new();
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

        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96f, 96f);
        Text = L("莉莉絲 AI MOD 一鍵安裝程式", "莉莉丝 AI MOD 一键安装程序", "リリス AI MOD セットアップ", "Lilith AI Mod Setup");
        Font = new Font("Segoe UI", 10f);
        ClientSize = new Size(820, 640);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Padding = new Padding(ContentMargin);

        var title = new Label
        {
            Text = L("讓莉莉絲能聊天、聽你說話，並使用中日語音。", "让莉莉丝能聊天、听你说话，并使用中日语音。", "リリスとの会話・音声入力・中国語／日本語音声を追加します。", "Adds AI chat, voice input, and Chinese/Japanese voices to Lilith."),
            Font = new Font(Font.FontFamily, 11f, FontStyle.Bold),
            AutoSize = false,
            Bounds = new Rectangle(ContentMargin, 22, ContentWidth, 40)
        };
        Controls.Add(title);

        Controls.Add(new Label
        {
            Text = L("遊戲資料夾", "游戏文件夹", "ゲームフォルダー", "Game folder"),
            Font = new Font(Font, FontStyle.Bold),
            Bounds = new Rectangle(ContentMargin, 72, ContentWidth, 22)
        });

        const int browseWidth = 100;
        _path.Bounds = new Rectangle(ContentMargin, 98, ContentWidth - browseWidth - 10, 32);
        _path.TextChanged += (_, _) => _pathTip.SetToolTip(_path, _path.Text);
        _browse.Text = L("瀏覽…", "浏览…", "参照…", "Browse…");
        _browse.Bounds = new Rectangle(ContentMargin + ContentWidth - browseWidth, 97, browseWidth, 34);
        _browse.Click += (_, _) => BrowseForGame();
        Controls.Add(_path);
        Controls.Add(_browse);

        Controls.Add(new Label
        {
            Text = L("安裝項目", "安装项目", "インストール項目", "Components"),
            Font = new Font(Font, FontStyle.Bold),
            Bounds = new Rectangle(ContentMargin, 148, ContentWidth, 22)
        });

        ConfigureOption(_core,
            L("核心 MOD 與 BepInEx（必要）", "核心 MOD 与 BepInEx（必需）", "コアMODとBepInEx（必須）", "Core mod and BepInEx (required)"),
            new Rectangle(ContentMargin + 4, 176, ContentWidth - 4, 36),
            checkedByDefault: true,
            enabled: false);
        ConfigureOption(_voicePack,
            L("完整中／日文補充台詞語音（約 301 MB）", "完整中／日文补充台词语音（约 301 MB）", "中国語／日本語の追加台詞音声（約301 MB）", "Chinese/Japanese supplemental dialogue voices (~301 MB)"),
            new Rectangle(ContentMargin + 4, 214, ContentWidth - 4, 36),
            checkedByDefault: true);
        ConfigureOption(_dynamicVoice,
            L("AI 動態語音（模型約 1.98 GB；首次會自動建立推理環境）", "AI 动态语音（模型约 1.98 GB；首次会自动建立推理环境）", "AI動的音声（モデル約1.98 GB・初回に推論環境を自動構築）", "Dynamic AI voice (~1.98 GB; builds runtime on first install)"),
            new Rectangle(ContentMargin + 4, 252, ContentWidth - 4, 36),
            checkedByDefault: true);
        ConfigureOption(_launch,
            L("安裝完成後啟動桌寵", "安装完成后启动桌宠", "完了後にデスクトップペットを起動", "Launch the desktop pet after installation"),
            new Rectangle(ContentMargin + 4, 290, ContentWidth - 4, 36),
            checkedByDefault: true);
        Controls.AddRange([_core, _voicePack, _dynamicVoice, _launch]);

        var privacy = new Label
        {
            Text = L(
                "隱私：安裝包不含任何 API Key、聊天紀錄或開發者電腦路徑。\n更新時也不會覆蓋玩家資料。",
                "隐私：安装包不含任何 API Key、聊天记录或开发者电脑路径。\n更新时也不会覆盖玩家数据。",
                "プライバシー：APIキー、会話履歴、開発PCのパスは含まれません。\n更新時も個人データを上書きしません。",
                "Privacy: no API keys, chat history, or developer paths are included.\nUpgrades preserve player data."),
            AutoSize = false,
            ForeColor = Color.FromArgb(90, 90, 90),
            Bounds = new Rectangle(ContentMargin, 348, ContentWidth, 56)
        };
        Controls.Add(privacy);

        _progress.Bounds = new Rectangle(ContentMargin, 424, ContentWidth, 24);
        _progress.Style = ProgressBarStyle.Continuous;
        _status.Text = L("準備就緒", "准备就绪", "準備完了", "Ready");
        _status.AutoEllipsis = true;
        _status.ForeColor = Color.FromArgb(60, 60, 60);
        _status.Bounds = new Rectangle(ContentMargin, 458, ContentWidth, 24);
        Controls.Add(_progress);
        Controls.Add(_status);

        _install.Text = L("安裝／更新", "安装／更新", "インストール／更新", "Install / Update");
        _uninstall.Text = L("移除 MOD", "移除 MOD", "MODを削除", "Remove mod");
        _close.Text = L("關閉", "关闭", "閉じる", "Close");
        StylePrimaryButton(_install);
        LayoutActionButtons();
        AcceptButton = _install;
        CancelButton = _close;
        _install.Click += async (_, _) => await InstallAsync();
        _uninstall.Click += async (_, _) => await UninstallAsync();
        _close.Click += (_, _) => Close();
        Controls.AddRange([_install, _uninstall, _close]);

        FormClosed += (_, _) => _pathTip.Dispose();
        Load += async (_, _) => await InitializeInstallerAsync();
    }

    private static void ConfigureOption(CheckBox option, string text, Rectangle bounds, bool checkedByDefault, bool enabled = true)
    {
        option.Text = text;
        option.Checked = checkedByDefault;
        option.Enabled = enabled;
        option.AutoSize = false;
        option.CheckAlign = ContentAlignment.MiddleLeft;
        option.TextAlign = ContentAlignment.MiddleLeft;
        option.Bounds = bounds;
    }

    private static void StylePrimaryButton(Button button)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.BackColor = Color.FromArgb(0, 120, 215);
        button.ForeColor = Color.White;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(16, 110, 190);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(0, 90, 158);
        button.Cursor = Cursors.Hand;
        button.Font = new Font(button.Font, FontStyle.Bold);
    }

    private void LayoutActionButtons()
    {
        const int buttonHeight = 38;
        const int gap = 12;
        const int bottom = 572;
        var installWidth = Math.Max(160, TextRenderer.MeasureText(_install.Text, _install.Font).Width + 36);
        var uninstallWidth = Math.Max(118, TextRenderer.MeasureText(_uninstall.Text, Font).Width + 28);
        var closeWidth = Math.Max(100, TextRenderer.MeasureText(_close.Text, Font).Width + 28);
        var right = ContentMargin + ContentWidth;
        _close.Bounds = new Rectangle(right - closeWidth, bottom, closeWidth, buttonHeight);
        _uninstall.Bounds = new Rectangle(_close.Left - gap - uninstallWidth, bottom, uninstallWidth, buttonHeight);
        _install.Bounds = new Rectangle(_uninstall.Left - gap - installWidth, bottom, installWidth, buttonHeight);
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

    private static void EnsureAllowedReleaseUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !AllowedReleaseOrigins.Any(origin =>
                uri.AbsoluteUri.StartsWith(origin, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                "Download URLs must come from Lilith-Awakened-Mod or Lilith-Awakened-Assets GitHub releases.");
        }
    }

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
