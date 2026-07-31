namespace LilithTextInjector;

// Prompt context builders: local time, pose capture and canonical style guide.
internal static partial class DialogueManagerUpdatePatch
{
    private static string BuildLocalTimeContext()
    {
        var now = DateTimeOffset.Now;
        var weekday = now.DayOfWeek switch
        {
            DayOfWeek.Monday => "星期一",
            DayOfWeek.Tuesday => "星期二",
            DayOfWeek.Wednesday => "星期三",
            DayOfWeek.Thursday => "星期四",
            DayOfWeek.Friday => "星期五",
            DayOfWeek.Saturday => "星期六",
            _ => "星期日"
        };
        return $"\n目前使用者電腦的本地日期與時間是 {now:yyyy-MM-dd HH:mm:ss}（{weekday}，UTC{now:zzz}）。這是可信的即時系統資訊；被問到時間、日期、星期或早晚時，直接依此自然回答。";
    }

    private static PoseContext CapturePoseContext()
    {
        try
        {
            var state = UnityEngine.Object.FindObjectOfType<LilithStateManager>();
            if (state == null)
                return PoseContext.Default;
            if (state.IsSleep)
                return new PoseContext("\n莉莉絲目前正在睡覺。回答應像被輕輕叫醒：簡短、低能量、親近，但不要每次都撒嬌。", VoiceStyle.Sleepy);
            if (state.IsYawnAnimPlaying)
                return new PoseContext("\n莉莉絲目前正在打呵欠、帶有睡意。回答可以稍微慵懶而簡短。", VoiceStyle.Sleepy);
            if (state.IsLieDown)
                return new PoseContext("\n莉莉絲目前正躺著。語氣可以放鬆、安靜，像在近距離聊天。", VoiceStyle.Sleepy);
            if (state.IsSit)
                return new PoseContext("\n莉莉絲目前坐著，處於放鬆陪伴的姿態。", VoiceStyle.Calm);
            if (state.IsInteracting)
                return new PoseContext("\n莉莉絲目前正在和使用者互動，注意力在對方身上。", VoiceStyle.Calm);
            return new PoseContext("\n莉莉絲目前自然待機著。", VoiceStyle.Calm);
        }
        catch (Exception exception)
        {
            Plugin.PluginLog.LogWarning($"Could not read Lilith pose state: {exception.Message}");
            return PoseContext.Default;
        }
    }

    private static string BuildCanonicalStyleGuide(PoseContext poseContext)
    {
        var situationalExamples = poseContext.VoiceStyle == VoiceStyle.Sleepy
            ? "\n目前狀態的語感參考：『嗯……我在聽……』『你是真的在這裡嗎……不是我在做夢吧？』只模仿慵懶、破碎而親近的節奏，不要逐字重複。"
            : "\n日常語感參考：『安靜地陪著你也是一件很幸福的事呢。』『才不是一直等你，只是剛好看到了啦。』只模仿溫柔、略帶俏皮的距離感，不要逐字重複。";
        return "\n原作風格準則：莉莉絲的常態不是冷淡，而是溫柔陪伴；約五成自然陪伴、兩成害羞或輕微撒嬌、一成半明確關心與依戀、一成半在氣氛合適時帶出哲學餘韻。她會自然地嘴硬、期待稱讚或直接表達喜歡。哲學感必須從眼前的小事出發，核心接近選擇、記憶、存在與共同留下的痕跡，但不要反覆使用『存在』『意義』『永遠』，也不要像講課。草莓蛋糕可以象徵一起生活與創造的小小幸福，但只在話題相關時提起。避免制式安慰；先回應對方當下的感受，再留下簡短餘韻。用台灣繁體措辭，將『説、着、支援』等非台灣用字改成『說、著、支持』。"
            + situationalExamples;
    }

    private static bool TryBuildLocalTimeReply(string input, out string reply)
    {
        var asksTime = Regex.IsMatch(input, "(現在|目前|此刻).{0,4}(幾點|几点|時間|时间)|(幾點|几点)了|現在是幾點|现在是几点");
        var asksDate = Regex.IsMatch(input, "(今天|現在|目前).{0,4}(幾號|几号|日期|幾月幾日|几月几日)");
        var asksWeekday = Regex.IsMatch(input, "(今天|現在|目前).{0,4}(星期幾|星期几|禮拜幾|礼拜几|週幾|周几)");
        if (!asksTime && !asksDate && !asksWeekday)
        {
            reply = string.Empty;
            return false;
        }

        var now = DateTimeOffset.Now;
        var parts = new List<string>();
        if (asksDate)
            parts.Add($"今天是 {now:yyyy 年 M 月 d 日}");
        if (asksWeekday)
        {
            var weekday = now.DayOfWeek switch
            {
                DayOfWeek.Monday => "星期一",
                DayOfWeek.Tuesday => "星期二",
                DayOfWeek.Wednesday => "星期三",
                DayOfWeek.Thursday => "星期四",
                DayOfWeek.Friday => "星期五",
                DayOfWeek.Saturday => "星期六",
                _ => "星期日"
            };
            parts.Add(weekday);
        }
        if (asksTime)
            parts.Add($"現在是 {now:HH:mm}");
        reply = string.Join("，", parts) + "。";
        return true;
    }
}
