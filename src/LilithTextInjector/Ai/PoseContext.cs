namespace LilithTextInjector;

internal enum VoiceStyle
{
    Calm,
    Excited,
    Wronged,
    Sleepy
}

internal sealed class PoseContext
{
    public static readonly PoseContext Default = new(string.Empty, VoiceStyle.Calm);
    public string Prompt { get; }
    public VoiceStyle VoiceStyle { get; }

    public PoseContext(string prompt, VoiceStyle voiceStyle)
    {
        Prompt = prompt;
        VoiceStyle = voiceStyle;
    }
}
