namespace LilithTextInjector;

// Persistence and event scheduling for AI-written notes.
internal static class AiNoteStore
{
    private static readonly object Lock = new();
    private static AiNoteState State = new();

    internal static void Load()
    {
        lock (Lock)
        {
            try
            {
                Directory.CreateDirectory(ModDataPaths.Directory);
                if (File.Exists(ModDataPaths.AiNoteStatePath))
                    State = JsonSerializer.Deserialize<AiNoteState>(File.ReadAllText(ModDataPaths.AiNoteStatePath)) ?? new AiNoteState();
                foreach (var item in State.Pending)
                    if (item.Status == "generating") item.Status = "pending";
                State.DeliveredAt.RemoveAll(value => value < DateTimeOffset.Now.AddDays(-30));
                Save();
                Plugin.PluginLog.LogInfo($"Loaded AI note scheduler: pending={State.Pending.Count}, recentDeliveries={State.DeliveredAt.Count}.");
            }
            catch (Exception exception)
            {
                State = new AiNoteState();
                Plugin.PluginLog.LogWarning($"Could not load AI note state: {exception.Message}");
            }
        }
    }

    internal static void Save()
    {
        lock (Lock)
        {
            Directory.CreateDirectory(ModDataPaths.Directory);
            File.WriteAllText(ModDataPaths.AiNoteStatePath, JsonSerializer.Serialize(State,
                new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }), Encoding.UTF8);
        }
    }

    internal static void ConsiderEvent(string userText, string reply)
    {
        if (!Plugin.AiNotesEnabled.Value || ContainsSensitiveNoteData(userText))
            return;
        if (Regex.IsMatch(userText, "(取消|算了|不用了|別再提|别再提|忘了吧|キャンセル|やめて|forget it|cancel)", RegexOptions.IgnoreCase))
            return;

        string category;
        string emotion;
        if (Regex.IsMatch(userText, "(明天|後天|下週|下周|等等要|待會要|考試|面試|報告|手術|約會|旅行|出發|deadline|tomorrow|exam|interview|明日|試験|面接)", RegexOptions.IgnoreCase))
        {
            category = "upcoming";
            emotion = Regex.IsMatch(userText, "(緊張|害怕|擔心|焦慮|不安|怖い|心配|nervous|worried)", RegexOptions.IgnoreCase) ? "緊張，需要溫柔支持" : "期待中的重要安排";
        }
        else if (Regex.IsMatch(userText, "(完成了|成功了|做到了|通過了|过了|終於|终于|畢業|毕业|錄取|得獎|できた|合格|finished|succeeded|passed)", RegexOptions.IgnoreCase))
        {
            category = "achievement";
            emotion = "值得一起慶祝與記住";
        }
        else if (Regex.IsMatch(userText, "(難過|伤心|傷心|哭了|寂寞|孤單|失戀|分手|失敗|失败|被罵|壓力|压力|悲しい|寂しい|つらい|sad|lonely|heartbroken)", RegexOptions.IgnoreCase))
        {
            category = "comfort";
            emotion = "低落，需要安靜陪伴而非說教";
        }
        else if (Regex.IsMatch(userText, "(約定|答應我|記得|不要忘記|我們說好|说好|約束|覚えて|promise|remember this)", RegexOptions.IgnoreCase))
        {
            category = "promise";
            emotion = "兩人之間值得珍惜的約定";
        }
        else if (Regex.IsMatch(userText, "(喜歡妳|喜欢你|愛妳|爱你|謝謝妳|谢谢你|有妳真好|陪著我|大好き|愛してる|ありがとう|love you|thank you)", RegexOptions.IgnoreCase))
        {
            category = "bond";
            emotion = "親近、感謝與依戀";
        }
        else
        {
            return;
        }

        var now = DateTimeOffset.Now;
        var minimum = Math.Clamp(Plugin.AiNoteMinimumDelayMinutes.Value, 1, 1440);
        var maximum = Math.Clamp(Plugin.AiNoteMaximumDelayMinutes.Value, minimum, 2880);
        var delay = System.Random.Shared.Next(minimum, maximum + 1);
        var compactUser = CompactNoteContext(userText, 360);
        var compactReply = CompactNoteContext(reply, 360);
        lock (Lock)
        {
            var existing = State.Pending.LastOrDefault(item => item.Status == "pending" && item.Category == category && item.CreatedAt > now.AddHours(-6));
            if (existing != null)
            {
                existing.Topic = compactUser;
                existing.LatestContext = compactReply;
                existing.Emotion = emotion;
                Save();
                Plugin.PluginLog.LogInfo($"Updated pending AI note event '{category}'.");
                return;
            }
            State.Pending.Add(new AiNoteEvent
            {
                Id = Guid.NewGuid().ToString("N"),
                Category = category,
                Topic = compactUser,
                LatestContext = compactReply,
                Emotion = emotion,
                CreatedAt = now,
                DeliverAfter = now.AddMinutes(delay),
                Status = "pending"
            });
            while (State.Pending.Count(item => item.Status == "pending") > 5)
                State.Pending.Remove(State.Pending.First(item => item.Status == "pending"));
            Save();
        }
        Plugin.PluginLog.LogInfo($"Scheduled AI note event '{category}' after {delay} minutes (content hidden from log).");
    }

    internal static void UpdatePendingFromUserText(string userText)
    {
        if (!Plugin.AiNotesEnabled.Value)
            return;
        var cancel = Regex.IsMatch(userText, "(取消了|取消吧|算了|不用了|不去了|別再提|别再提|忘了吧|キャンセル|中止|やめて|cancel|forget it)", RegexOptions.IgnoreCase);
        var completed = Regex.IsMatch(userText, "(完成了|結束了|结束了|考完了|做完了|成功了|通過了|过了|終於好了|終わった|できた|finished|done|passed)", RegexOptions.IgnoreCase);
        if (!cancel && !completed)
            return;
        lock (Lock)
        {
            var item = State.Pending.LastOrDefault(value => value.Status == "pending");
            if (item == null) return;
            if (cancel)
            {
                item.Status = "cancelled";
                Plugin.PluginLog.LogInfo($"Cancelled pending AI note event '{item.Category}' from follow-up context.");
            }
            else
            {
                item.Category = "achievement";
                item.Emotion = "事情已經完成，適合祝賀並關心結果";
                item.LatestContext = CompactNoteContext(userText, 360);
                Plugin.PluginLog.LogInfo("Updated pending AI note event after the user reported completion.");
            }
            Save();
        }
    }

    internal static void MarkDelivered(string eventId, string path)
    {
        lock (Lock)
        {
            var item = State.Pending.FirstOrDefault(value => value.Id == eventId);
            if (item != null) item.Status = "delivered";
            State.DeliveredAt.Add(DateTimeOffset.Now);
            State.DeliveredPaths.Add(path);
            State.DeliveredAt.RemoveAll(value => value < DateTimeOffset.Now.AddDays(-30));
            Save();
        }
    }

    internal static AiNoteEvent? FindDue(DateTimeOffset now)
    {
        lock (Lock)
            return State.Pending.FirstOrDefault(item => item.Status == "pending" && item.DeliverAfter <= now);
    }

    internal static bool IsWithinCooldown(DateTimeOffset now, TimeSpan cooldown)
    {
        lock (Lock)
            return State.DeliveredAt.Count > 0 && now - State.DeliveredAt.Max() < cooldown;
    }

    internal static bool WeeklyLimitReached(DateTimeOffset now, int weeklyLimit)
    {
        lock (Lock)
            return State.DeliveredAt.Count(value => value >= now.AddDays(-7)) >= weeklyLimit;
    }

    internal static bool OwnsDeliveredPath(string newestPath)
    {
        lock (Lock)
            return State.DeliveredPaths.Any(path =>
                string.Equals(Path.GetFullPath(path), Path.GetFullPath(newestPath), StringComparison.OrdinalIgnoreCase));
    }

    internal static void MarkGenerating(AiNoteEvent item)
    {
        item.Status = "generating";
        Save();
    }

    internal static void DeferAfterFailure(AiNoteEvent item)
    {
        lock (Lock)
        {
            item.Status = "pending";
            item.DeliverAfter = DateTimeOffset.Now.AddHours(1);
            Save();
        }
    }

    internal static bool ContainsSensitiveNoteData(string text)
    {
        return Regex.IsMatch(text, "(api[ _-]?key|密碼|密码|驗證碼|验证码|信用卡|身分證|身份证|護照|passport|password|token|secret)", RegexOptions.IgnoreCase)
            || Regex.IsMatch(text, @"\b(?:\d[ -]*?){12,19}\b")
            || Regex.IsMatch(text, @"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b", RegexOptions.IgnoreCase);
    }

    private static string CompactNoteContext(string text, int maximum)
    {
        var compact = Regex.Replace(text, @"\s+", " ").Trim();
        return compact.Length <= maximum ? compact : compact[..maximum] + "…";
    }
}

internal sealed class AiNoteState
{
    public List<AiNoteEvent> Pending { get; set; } = new();
    public List<DateTimeOffset> DeliveredAt { get; set; } = new();
    public List<string> DeliveredPaths { get; set; } = new();
}

internal sealed class AiNoteEvent
{
    public string Id { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string LatestContext { get; set; } = string.Empty;
    public string Emotion { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset DeliverAfter { get; set; }
    public string Status { get; set; } = "pending";
}

internal sealed class GeneratedAiNote
{
    public string EventId { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}
