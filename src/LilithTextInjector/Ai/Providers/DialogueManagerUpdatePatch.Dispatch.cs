namespace LilithTextInjector;

// Chat request router: provider selection, reply post-processing and emotion selection.
// Entry point is RequestChatAsync (formerly RequestGeminiAsync).
internal static partial class DialogueManagerUpdatePatch
{
    private static async Task RequestChatAsync(string userText, string playerName, PoseContext poseContext)
    {
        GeminiAgentSession? agentSession = null;
        try
        {
            var japaneseVoiceMode = IsJapaneseVoiceMode();
            var interfaceLanguage = GetAiInterfaceLanguage();
            var model = Uri.EscapeDataString(Plugin.GeminiModel.Value.Trim());
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";
            var contents = BuildGeminiContents();
            var nameContext = BuildPlayerNameContext(userText, playerName);
            var timeContext = BuildLocalTimeContext();
            var weatherContext = await BuildWeatherContextAsync().ConfigureAwait(false);
            var useGoogleSearch = ShouldUseGeminiGoogleSearch(userText);
            var activeProvider = NormalizeAiProvider(Plugin.AiProvider.Value);
            var systemInstruction = Plugin.PersonaPrompt.Value + "\n角色事實：" + Plugin.CharacterLore.Value
                + "\n情緒表達：" + Plugin.EmotionGuidance.Value + nameContext + poseContext.Prompt + timeContext + weatherContext
                + BuildCanonicalStyleGuide(poseContext)
                + $"\n語言規則：目前遊戲介面語言是{interfaceLanguage.Name}。無論使用者輸入哪種語言，氣泡顯示內容都必須使用{interfaceLanguage.Name}；只有無法翻譯的專有名詞可以保留原文。若角色設定中原有的語言要求不同，以本條規則為準。每次回答必須完成最後一句，不可停在半句、連接詞或未閉合的引號。{interfaceLanguage.ExtraRule}"
                + (japaneseVoiceMode
                    ? $"\n目前為日文語音模式。只輸出一個 JSON 物件，格式為 {{\"display_text\":\"{interfaceLanguage.Example}\",\"speech_ja\":\"語意相同但適合自然口語演出的日文\"}}。display_text 必須使用{interfaceLanguage.Name}；speech_ja 必須使用日文且不可逐字硬譯，要保留莉莉絲的情緒、停頓與女性口吻。兩個欄位都必須是完整句子，不要輸出 JSON 以外內容。"
                    : string.Empty);
            if (useGoogleSearch)
                systemInstruction += "\nThis question explicitly requests a lookup or depends on current facts. You must use the available web-search tool before answering, answer concisely in character, and never invent facts absent from the results.";
            else if (string.Equals(activeProvider, "Qwen", StringComparison.Ordinal))
                systemInstruction += "\nA web-search tool is available. Use it whenever the answer materially depends on recent or changeable facts such as news, current people or policies, prices, weather, schedules, software/model versions, service availability, or product features. Do not search for casual conversation, roleplay, personal advice, or stable facts.";
            var desktopToolsEnabled = Plugin.AdvancedComputerActionsEnabled.Value;
            if (desktopToolsEnabled
                && (string.Equals(activeProvider, "Gemini", StringComparison.Ordinal)
                    || string.Equals(activeProvider, "Qwen", StringComparison.Ordinal)))
            {
                systemInstruction += "\nDesktop agent policy: You may use the declared local desktop tools whenever they help fulfill the user's intent. Prefer tools over asking the user to repeat an exact command, and you may call several independent tools in parallel to complete a routine. Never claim an action succeeded unless its function result says success. All tools operate locally. Never request or expose passwords, API keys, OTPs, clipboard contents, file contents, browsing history, screenshots, precise location, or personal data. Never infer sleep or lock merely because the user says they are tired; call those tools only when the user explicitly asks the computer to sleep or lock. Destructive file operations, closing apps, shutdown, restart, arbitrary typing, arbitrary shortcuts, shell commands, and privilege elevation are unavailable. If a tool is unavailable, explain naturally without pretending it ran.";
            }
            if (string.Equals(activeProvider, "Qwen", StringComparison.Ordinal))
            {
                await RequestQwenResponsesAsync(systemInstruction, userText, poseContext, japaneseVoiceMode,
                    useWebSearch: true, forceWebSearch: useGoogleSearch, desktopToolsEnabled).ConfigureAwait(false);
                return;
            }
            if (!string.Equals(activeProvider, "Gemini", StringComparison.Ordinal))
            {
                await OpenAiCompatibleClient.RequestAsync(activeProvider, systemInstruction, userText, poseContext, japaneseVoiceMode, CompleteAiReply).ConfigureAwait(false);
                return;
            }
            agentSession = new GeminiAgentSession
            {
                Url = url,
                SystemInstruction = systemInstruction,
                UserText = userText,
                PoseContext = poseContext,
                JapaneseVoiceMode = japaneseVoiceMode,
                UseGoogleSearch = useGoogleSearch,
                DesktopToolsEnabled = desktopToolsEnabled,
                Contents = contents.Cast<object>().ToList()
            };
            await SendGeminiAgentRequestAsync(agentSession).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            if (agentSession?.DesktopToolsEnabled == true
                && exception is HttpRequestException
                && Regex.IsMatch(exception.Message, "Gemini HTTP (400|404|422)", RegexOptions.IgnoreCase))
            {
                Plugin.PluginLog.LogWarning($"Gemini desktop tools were rejected by model '{Plugin.GeminiModel.Value}'. Falling back safely: {exception.Message}");
                PendingGeminiCompatibilityFallbacks.Enqueue(agentSession);
                return;
            }
            Plugin.PluginLog.LogError(exception);
            if (string.Equals(NormalizeAiProvider(Plugin.AiProvider.Value), "Qwen", StringComparison.Ordinal)
                && IsQwenAccountUnavailable(exception))
            {
                PendingReplies.Enqueue(QwenAccountUnavailableReply());
                return;
            }
            PendingReplies.Enqueue(ApiKeyText("連線好像出了點問題。晚點再試吧。", "连接好像出了点问题。稍后再试吧。", "接続に少し問題があるみたい。あとでまた試してみて。", "There seems to be a connection problem. Please try again later."));
        }
    }

    internal static void CompleteAiReply(string rawReply, string userText, PoseContext poseContext, bool japaneseVoiceMode)
    {
        var bilingual = japaneseVoiceMode ? ParseBilingualReply(rawReply) : null;
        var reply = CleanReply(bilingual?.DisplayText ?? rawReply);
        var japaneseSpeech = bilingual?.JapaneseSpeech ?? string.Empty;
        if (reply.Length > 0)
            AddMemoryTurn("model", reply);
        if (reply.Length > 0)
            ConsiderAiNoteEvent(userText, reply);
        PendingAiEmotions.Enqueue(ChooseAiEmotion(userText, reply, poseContext));
        foreach (var page in SplitIntoBubblePages(reply.Length > 0 ? reply : "……"))
            PendingReplies.Enqueue(page);
        if (reply.Length == 0 || !Plugin.VoiceEnabled.Value)
            return;
        var reaction = japaneseVoiceMode ? null : GetNativeReaction(userText, reply);
        var speechText = reaction == null ? reply : RemoveLeadingReactionText(reply);
        if (japaneseVoiceMode && !string.IsNullOrWhiteSpace(japaneseSpeech))
            speechText = japaneseSpeech;
        _ = RequestSpeechAsync(speechText.Length > 0 ? speechText : reply, reaction, poseContext.VoiceStyle, japaneseVoiceMode);
    }

    internal static string NormalizeAiProvider(string? provider)
    {
        if (string.Equals(provider, "Qwen", StringComparison.OrdinalIgnoreCase)
            || string.Equals(provider, "Tongyi", StringComparison.OrdinalIgnoreCase)
            || string.Equals(provider, "DashScope", StringComparison.OrdinalIgnoreCase)) return "Qwen";
        if (string.Equals(provider, "OpenAI", StringComparison.OrdinalIgnoreCase)) return "OpenAI";
        if (string.Equals(provider, "DeepSeek", StringComparison.OrdinalIgnoreCase)) return "DeepSeek";
        return "Gemini";
    }

    private static string GetActiveChatApiKey()
    {
        return NormalizeAiProvider(Plugin.AiProvider.Value) switch
        {
            "Qwen" => Plugin.QwenApiKey.Value,
            "OpenAI" => Plugin.OpenAiApiKey.Value,
            "DeepSeek" => Plugin.DeepSeekApiKey.Value,
            _ => Plugin.GeminiApiKey.Value
        };
    }

    private static string BuildPlayerNameContext(string userText, string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName))
            return string.Empty;

        var cooldownComplete = _repliesSincePlayerNameWasOffered >= 3;
        var emotionalContext = Regex.IsMatch(userText,
            "(難過|难过|傷心|伤心|寂寞|害怕|累了|想妳|想你|喜歡妳|喜欢你|愛妳|爱你|晚安|早安|おやすみ|寂しい|怖い|疲れた|好き|大好き|good night|miss you|love you)",
            RegexOptions.IgnoreCase);
        var probability = emotionalContext ? 0.30 : 0.12;
        var offerName = cooldownComplete && System.Random.Shared.NextDouble() < probability;

        if (offerName)
        {
            _repliesSincePlayerNameWasOffered = 0;
            return $"\n使用者在遊戲中設定的名字是「{playerName}」。本次只有在情感與語境自然時才可稱呼一次；不自然時仍不要使用。";
        }

        _repliesSincePlayerNameWasOffered++;
        return "\n本次回覆不要主動稱呼使用者的名字，即使近期對話記憶中曾經出現過。";
    }

    private static string ChooseAiEmotion(string userText, string reply, PoseContext poseContext)
    {
        var combined = userText + "\n" + reply;
        if (poseContext.VoiceStyle == VoiceStyle.Sleepy
            || Regex.IsMatch(combined, "(想睡|睏|困了|睡覺|睡觉|晚安|おやすみ|眠い|寝る|sleepy|good night)", RegexOptions.IgnoreCase))
            return "emoji_sleepy_1";
        if (Regex.IsMatch(combined, "(生氣|生气|討厭|讨厌|笨蛋|不准|不許|不许|怒|むかつく|嫌い|angry|mad)", RegexOptions.IgnoreCase))
            return "emoji_angry_1";
        if (Regex.IsMatch(combined, "(害怕|可怕|恐怖|擔心|担心|怖い|不安|scared|afraid)", RegexOptions.IgnoreCase))
            return "emoji_fear_1";
        if (Regex.IsMatch(combined, "(難過|难过|傷心|伤心|哭|寂寞|孤單|孤单|悲しい|寂しい|sad|lonely)", RegexOptions.IgnoreCase))
            return "emoji_sad_1";
        if (Regex.IsMatch(combined, "(委屈|不理我|忘記我|忘记我|不要走|置いていか|wronged|leave me)", RegexOptions.IgnoreCase))
            return "emoji_wronged_1";
        if (Regex.IsMatch(combined, "(真的嗎|真的吗|居然|竟然|沒想到|没想到|驚訝|惊讶|びっくり|本当|really|surpris)", RegexOptions.IgnoreCase))
            return "emoji_surprise_2";
        if (Regex.IsMatch(combined, "(不懂|奇怪|為什麼|为什么|怎麼會|怎么会|困惑|分からない|なぜ|confus|why)", RegexOptions.IgnoreCase))
            return "emoji_daze_1";
        if (Regex.IsMatch(combined, "(開心|开心|喜歡|喜欢|愛|爱|謝謝|谢谢|草莓蛋糕|可愛|可爱|嬉しい|好き|ありがとう|happy|love|cute|thank)", RegexOptions.IgnoreCase))
            return "emoji_smile_3";
        return "emoji_calm_1";
    }

    private static void PlayAiEmotion(string emotion)
    {
        try
        {
            var character = UnityEngine.Object.FindObjectOfType<global::CharacterController>();
            if (character == null)
            {
                Plugin.PluginLog.LogWarning("Could not find CharacterController for AI emotion playback.");
                return;
            }
            var played = character.PlayEmotion(emotion, loop: false, instant: false, bypassFollowLock: false);
            Plugin.PluginLog.LogInfo($"AI Live2D emotion '{emotion}' requested; played={played}.");
        }
        catch (Exception exception)
        {
            Plugin.PluginLog.LogWarning($"Could not play AI Live2D emotion '{emotion}': {exception.Message}");
        }
    }

    internal static string CleanReply(string? reply)
    {
        if (string.IsNullOrWhiteSpace(reply))
            return string.Empty;
        var cleaned = Regex.Replace(reply, "[`*_#>]", string.Empty);
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
        return cleaned;
    }

    private static BilingualReply? ParseBilingualReply(string raw)
    {
        try
        {
            var json = raw.Trim();
            if (json.StartsWith("```", StringComparison.Ordinal))
            {
                var firstNewline = json.IndexOf('\n');
                var lastFence = json.LastIndexOf("```", StringComparison.Ordinal);
                if (firstNewline >= 0 && lastFence > firstNewline)
                    json = json.Substring(firstNewline + 1, lastFence - firstNewline - 1).Trim();
            }
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var display = root.TryGetProperty("display_text", out var displayElement)
                ? displayElement.GetString()
                : root.TryGetProperty("display_zh", out var legacyDisplayElement) ? legacyDisplayElement.GetString() : null;
            var speech = root.TryGetProperty("speech_ja", out var speechElement) ? speechElement.GetString() : null;
            if (string.IsNullOrWhiteSpace(display) || string.IsNullOrWhiteSpace(speech))
                return null;
            return new BilingualReply(display!, speech!);
        }
        catch (Exception exception)
        {
            Plugin.PluginLog.LogWarning($"Could not parse bilingual Gemini reply; falling back to displayed text: {exception.Message}");
            return null;
        }
    }

    private sealed class BilingualReply
    {
        public string DisplayText { get; }
        public string JapaneseSpeech { get; }

        public BilingualReply(string displayText, string japaneseSpeech)
        {
            DisplayText = displayText;
            JapaneseSpeech = japaneseSpeech;
        }
    }
}
