namespace LilithTextInjector;

// Scheduling, rate limiting and generation of AI-written notes.
// Persistence lives in AiNoteStore; this partial owns Unity-tick + HTTP generation.
internal static partial class DialogueManagerUpdatePatch
{
    private static void TryCreateOneTestNote()
    {
        if (_testNoteAttempted || !Plugin.TestNoteOnce.Value || Time.unscaledTime < 8f)
            return;
        _testNoteAttempted = true;
        Plugin.TestNoteOnce.Value = false;
        try
        {
            var text = ApiKeyText(
                "剛才聊到，要把那些原本藏在日常裡的小巧思延續下去。若你看見這封信，就代表莉莉絲真的能把我們的談話留在這裡了。——莉莉絲",
                "刚才聊到，要把那些原本藏在日常里的小巧思延续下去。若你看见这封信，就代表莉莉丝真的能把我们的谈话留在这里了。——莉莉丝",
                "さっき、日常に隠れている小さな仕掛けを、これからも大切にしようって話したね。この手紙が届いたなら、私たちの会話をここに残せたということ。——リリス",
                "We talked about carrying those little details hidden in everyday life forward. If this letter reached you, Lilith can truly leave a trace of our conversation here. —Lilith");
            var path = NoteImageSaver.SaveNote(text, false);
            NoteInbox.NotifySaved();
            Plugin.PluginLog.LogInfo($"Created one native-format test note: {path}");
        }
        catch (Exception exception)
        {
            Plugin.PluginLog.LogWarning($"Could not create the one-time test note: {exception.Message}");
        }
    }

    internal static void LoadAiNoteState() => AiNoteStore.Load();

    private static void ConsiderAiNoteEvent(string userText, string reply) => AiNoteStore.ConsiderEvent(userText, reply);

    private static void UpdatePendingAiNoteEvents(string userText) => AiNoteStore.UpdatePendingFromUserText(userText);

    private static void ProcessAiNoteScheduler()
    {
        while (PendingAiNotes.TryDequeue(out var generated))
        {
            try
            {
                var path = NoteImageSaver.SaveNote(generated.Text, false);
                NoteInbox.NotifySaved();
                AiNoteStore.MarkDelivered(generated.EventId, path);
                Plugin.PluginLog.LogInfo($"Delivered scheduled AI note through the native inbox: {path}");
            }
            catch (Exception exception)
            {
                Plugin.PluginLog.LogWarning($"Could not save generated AI note: {exception.Message}");
            }
            _aiNoteGenerationInFlight = false;
        }

        if (!Plugin.AiNotesEnabled.Value || _aiNoteGenerationInFlight || Time.unscaledTime < _nextAiNoteCheckAt)
            return;
        _nextAiNoteCheckAt = Time.unscaledTime + 30f;
        var now = DateTimeOffset.Now;
        var due = AiNoteStore.FindDue(now);
        if (due == null)
            return;
        if (GetWindowsIdleSeconds() < Math.Clamp(Plugin.AiNoteRequiredAwayMinutes.Value, 0, 1440) * 60)
            return;
        var cooldown = TimeSpan.FromHours(Math.Clamp(Plugin.AiNoteCooldownHours.Value, 1, 720));
        if (AiNoteStore.IsWithinCooldown(now, cooldown))
            return;
        if (AiNoteStore.WeeklyLimitReached(now, Math.Clamp(Plugin.AiNoteWeeklyLimit.Value, 1, 20)))
            return;
        if (OfficialNoteWasJustDelivered(now))
            return;
        if (string.IsNullOrWhiteSpace(Plugin.GeminiApiKey.Value))
            return;

        AiNoteStore.MarkGenerating(due);
        _aiNoteGenerationInFlight = true;
        var language = GetAiInterfaceLanguage().Name;
        _ = GenerateAiNoteAsync(due, language);
    }

    private static bool OfficialNoteWasJustDelivered(DateTimeOffset now)
    {
        try
        {
            var directory = NoteInbox.NotesDirectory;
            if (!Directory.Exists(directory)) return false;
            var newest = Directory.GetFiles(directory, "note_*.png").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            if (newest == null || now - File.GetLastWriteTime(newest) > TimeSpan.FromMinutes(30)) return false;
            return !AiNoteStore.OwnsDeliveredPath(newest);
        }
        catch { return false; }
    }

    private static async Task GenerateAiNoteAsync(AiNoteEvent item, string language)
    {
        try
        {
            var model = Uri.EscapeDataString(Plugin.GeminiModel.Value.Trim());
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";
            var prompt = $"Write one private desktop-pet letter in {language} as Lilith. Event category: {item.Category}. User topic: {item.Topic}. Earlier Lilith reply: {item.LatestContext}. Emotional direction: {item.Emotion}. Write 45-110 natural words or equivalent characters. Be warm, slightly playful and intimate, with subtle themes of memory, choice, and shared existence only when natural. Do not mention AI, scheduling, stored memory, APIs, or monitoring. Do not give medical or professional claims. Do not repeat sensitive details. End with an em dash and Lilith's localized name. Output only the letter text.";
            var payload = new
            {
                contents = new[] { new { role = "user", parts = new[] { new { text = prompt } } } },
                generationConfig = new { maxOutputTokens = 320, thinkingConfig = new { thinkingLevel = "MINIMAL" } }
            };
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("x-goog-api-key", Plugin.GeminiApiKey.Value.Trim());
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var response = await AiHttp.Client.SendAsync(request).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) throw new HttpRequestException($"Gemini note HTTP {(int)response.StatusCode}: {body}");
            using var document = JsonDocument.Parse(body);
            var parts = document.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts");
            var text = string.Concat(parts.EnumerateArray().Where(part => part.TryGetProperty("text", out _)).Select(part => part.GetProperty("text").GetString())).Trim();
            text = CleanReply(text);
            if (text.Length == 0) throw new InvalidOperationException("Gemini returned an empty note.");
            PendingAiNotes.Enqueue(new GeneratedAiNote { EventId = item.Id, Text = text });
        }
        catch (Exception exception)
        {
            AiNoteStore.DeferAfterFailure(item);
            _aiNoteGenerationInFlight = false;
            Plugin.PluginLog.LogWarning($"AI note generation failed and was deferred: {exception.Message}");
        }
    }
}
