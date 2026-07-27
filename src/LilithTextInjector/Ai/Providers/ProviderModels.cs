namespace LilithTextInjector;

internal sealed class GeminiAgentSession
{
    public string Url { get; set; } = string.Empty;
    public string SystemInstruction { get; set; } = string.Empty;
    public string UserText { get; set; } = string.Empty;
    public PoseContext PoseContext { get; set; } = PoseContext.Default;
    public bool JapaneseVoiceMode { get; set; }
    public bool UseGoogleSearch { get; set; }
    public bool DesktopToolsEnabled { get; set; }
    public int ToolRounds { get; set; }
    public List<object> Contents { get; set; } = new();
}

internal sealed class GeminiFunctionCallData
{
    public string Name { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public JsonElement Args { get; set; }
}

internal sealed class GeminiToolBatch
{
    public GeminiAgentSession Session { get; set; } = new();
    public List<GeminiFunctionCallData> Calls { get; set; } = new();
}

internal sealed class GeminiToolResult
{
    public string Name { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

internal sealed class QwenAgentSession
{
    public string Url { get; set; } = string.Empty;
    public string SystemInstruction { get; set; } = string.Empty;
    public string UserText { get; set; } = string.Empty;
    public PoseContext PoseContext { get; set; } = PoseContext.Default;
    public bool JapaneseVoiceMode { get; set; }
    public bool UseWebSearch { get; set; }
    public bool ForceWebSearch { get; set; }
    public bool DesktopToolsEnabled { get; set; }
    public int ToolRounds { get; set; }
    public List<object> Input { get; set; } = new();
}

internal sealed class QwenToolBatch
{
    public QwenAgentSession Session { get; set; } = new();
    public List<GeminiFunctionCallData> Calls { get; set; } = new();
}
