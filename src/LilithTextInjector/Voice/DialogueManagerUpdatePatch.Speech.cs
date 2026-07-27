namespace LilithTextInjector;

// Text-to-speech synthesis, native reaction sounds and playback.
internal static partial class DialogueManagerUpdatePatch
{
    private static NativeReaction? GetNativeReaction(string userText, string reply)
    {
        if (!Plugin.ReactionSoundsEnabled.Value)
            return null;

        string? fileName = null;
        if (Regex.IsMatch(userText, "(草莓蛋糕|送妳|送你|禮物|礼物|驚喜|惊喜|特地買|特地买)")
            || Regex.IsMatch(reply, "(欸|咦|真的嗎|沒想到|居然|竟然|原來如此)[！!？?]?"))
            fileName = "surprised.wav";
        else if (Regex.IsMatch(userText, "(不理妳|不理你|要走了|離開妳|离开你|忘記妳|忘记你|討厭妳|讨厌你)")
            || Regex.IsMatch(reply, "(不要走|不理我|寂寞|難過|委屈|討厭我|忘記我|對不起)"))
            fileName = "wronged.wav";
        if (fileName == null)
            return null;

        try
        {
            var path = Path.Combine(Plugin.ReactionSoundsDirectory.Value, fileName);
            if (!File.Exists(path))
                return null;
            Plugin.PluginLog.LogInfo($"Selected native reaction sound: {fileName}");
            return new NativeReaction
            {
                Audio = File.ReadAllBytes(path),
                Style = fileName == "surprised.wav" ? VoiceStyle.Excited : VoiceStyle.Wronged
            };
        }
        catch (Exception exception)
        {
            Plugin.PluginLog.LogWarning($"Could not queue native reaction: {exception.Message}");
            return null;
        }
    }

    private static string RemoveLeadingReactionText(string text)
    {
        var cleaned = Regex.Replace(
            text.TrimStart(),
            @"^(?:(?:嗯|唔|嗚|呜|哼|欸|诶|咦|啊|呀|唉|呵){1,3}|真的嗎|真的吗)[\s…\.，,。！？!?～~]*",
            string.Empty,
            RegexOptions.IgnoreCase).TrimStart();
        if (!string.Equals(cleaned, text, StringComparison.Ordinal))
            Plugin.PluginLog.LogInfo($"Removed voiced reaction prefix before TTS ({text.Length} -> {cleaned.Length} chars).");
        return cleaned;
    }

    private static async Task RequestSpeechAsync(string text, NativeReaction? reaction = null, VoiceStyle poseStyle = VoiceStyle.Calm, bool? japaneseVoiceMode = null)
    {
        var generationTimer = Stopwatch.StartNew();
        try
        {
            var useJapanese = japaneseVoiceMode ?? IsJapaneseVoiceMode();
            var speechText = PrepareTextForSpeech(text);
            if (speechText.Length == 0)
                return;
            var referencePath = useJapanese ? Plugin.JapaneseVoiceReferencePath.Value.Trim() : Plugin.VoiceReferencePath.Value.Trim();
            var promptText = useJapanese
                ? "これは儀式でもあるの。君に私の存在を感じてもらうための儀式ね。"
                : "你的選擇創造了我，所以我的存在本身就是你的善意。";
            var effectiveStyle = reaction?.Style ?? poseStyle;
            var auxiliaryReferences = useJapanese ? effectiveStyle switch
            {
                VoiceStyle.Excited => new[] { Plugin.JapaneseExcitedVoiceReferencePath.Value.Trim() },
                VoiceStyle.Wronged => new[] { Plugin.JapaneseWrongedVoiceReferencePath.Value.Trim() },
                VoiceStyle.Sleepy => new[] { Plugin.JapaneseSleepyVoiceReferencePath.Value.Trim() },
                _ => new[] { Plugin.JapaneseCalmAuxVoiceReferencePath.Value.Trim() }
            } : effectiveStyle switch
            {
                VoiceStyle.Excited => new[] { Plugin.ExcitedVoiceReferencePath.Value.Trim() },
                VoiceStyle.Wronged => new[] { Plugin.WrongedVoiceReferencePath.Value.Trim() },
                VoiceStyle.Sleepy => new[] { Plugin.SleepyVoiceReferencePath.Value.Trim() },
                _ => Array.Empty<string>()
            };
            auxiliaryReferences = Array.FindAll(auxiliaryReferences, File.Exists);
            if (!File.Exists(referencePath))
            {
                Plugin.PluginLog.LogWarning($"Voice reference was not found: {referencePath}");
                return;
            }

            var payload = new
            {
                text = speechText,
                text_lang = useJapanese ? "ja" : "zh",
                ref_audio_path = referencePath,
                aux_ref_audio_paths = auxiliaryReferences,
                prompt_lang = useJapanese ? "ja" : "zh",
                prompt_text = promptText,
                text_split_method = "cut0",
                batch_size = 1,
                media_type = "wav",
                streaming_mode = false,
                seed = 42
            };
            var endpoint = useJapanese ? Plugin.JapaneseVoiceEndpoint.Value.Trim() : Plugin.VoiceEndpoint.Value.Trim();
            var payloadJson = JsonSerializer.Serialize(payload);
            var localEndpoint = IsLocalVoiceEndpoint(endpoint);
            var maximumAttempts = localEndpoint && Plugin.VoiceAutoStartLocalService.Value ? 7 : 1;
            for (var attempt = 1; attempt <= maximumAttempts; attempt++)
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                    request.Content = new StringContent(payloadJson, Encoding.UTF8, "application/json");
                    using var response = await Http.SendAsync(request).ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        var error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        throw new HttpRequestException($"TTS HTTP {(int)response.StatusCode}: {error}");
                    }
                    var speech = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    PendingVoiceAudio.Enqueue(new VoiceSequence { Reaction = reaction?.Audio, Speech = speech });
                    Plugin.PluginLog.LogInfo($"Local voice generation completed in {generationTimer.Elapsed.TotalSeconds:F2}s ({speech.Length} bytes)." );
                    return;
                }
                catch (HttpRequestException exception) when (attempt < maximumAttempts)
                {
                    Plugin.PluginLog.LogInfo($"Local voice service is still starting; retrying in 5 seconds ({attempt}/{maximumAttempts}): {exception.Message}");
                    await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                }
            }
        }
        catch (Exception exception)
        {
            Plugin.PluginLog.LogWarning($"Voice generation failed; text chat continues: {exception.Message}");
        }
    }

    private static bool IsLocalVoiceEndpoint(string endpoint)
    {
        return Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) && uri.IsLoopback;
    }

    private static string PrepareTextForSpeech(string text)
    {
        var cleaned = Regex.Replace(text, @"\[([^\]]+)\]\(https?://[^\s\)]+\)", "$1", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"https?://\S+", string.Empty, RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"(?:來源|资料来源|資料來源|出典|Sources?)\s*[:：]\s*$", string.Empty,
            RegexOptions.IgnoreCase | RegexOptions.Multiline);
        cleaned = Regex.Replace(cleaned, @"[ \t]+", " ");
        cleaned = Regex.Replace(cleaned, @"\n{3,}", "\n\n").Trim();
        if (!string.Equals(cleaned, text, StringComparison.Ordinal))
            Plugin.PluginLog.LogInfo($"Removed web citation markup before TTS ({text.Length} -> {cleaned.Length} chars).");
        return cleaned;
    }

    private static void SetVoicePitch(float pitch)
    {
        var manager = AudioManager.instance;
        if (manager != null && manager.source_Voice != null)
            manager.source_Voice.pitch = pitch;
    }

    private static AudioClip PlayWav(byte[] wav, string label)
    {
        var clip = CreateAudioClipFromWav(wav);
        AudioManager.PlayVoice(clip, false, true);
        Plugin.PluginLog.LogInfo($"Playing {label} ({wav.Length} bytes, {clip.length:0.00}s).");
        return clip;
    }

    private sealed class VoiceSequence
    {
        public byte[]? Reaction { get; set; }
        public byte[] Speech { get; set; } = Array.Empty<byte>();
    }

    private sealed class NativeReaction
    {
        public byte[] Audio { get; set; } = Array.Empty<byte>();
        public VoiceStyle Style { get; set; }
    }

    private enum VoiceStyle
    {
        Calm,
        Excited,
        Wronged,
        Sleepy
    }
}
