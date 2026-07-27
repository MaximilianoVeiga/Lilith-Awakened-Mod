namespace LilithTextInjector;

// Rolling conversation memory persisted to memory.json.
internal static partial class DialogueManagerUpdatePatch
{
    internal static void LoadMemory()
    {
        try
        {
            Directory.CreateDirectory(MemoryDirectory);
            if (!File.Exists(MemoryPath))
                return;
            var loaded = JsonSerializer.Deserialize<List<ChatTurn>>(File.ReadAllText(MemoryPath));
            if (loaded == null)
                return;
            lock (MemoryLock)
            {
                RecentConversation.Clear();
                RecentConversation.AddRange(loaded.GetRange(Math.Max(0, loaded.Count - MaxRememberedTurns), Math.Min(MaxRememberedTurns, loaded.Count)));
            }
            Plugin.PluginLog.LogInfo($"Loaded {RecentConversation.Count} remembered chat turns.");
        }
        catch (Exception exception)
        {
            Plugin.PluginLog.LogWarning($"Could not load chat memory: {exception.Message}");
        }
    }

    private static void AddMemoryTurn(string role, string text)
    {
        lock (MemoryLock)
        {
            RecentConversation.Add(new ChatTurn { Role = role, Text = text });
            while (RecentConversation.Count > MaxRememberedTurns)
                RecentConversation.RemoveAt(0);
            try
            {
                Directory.CreateDirectory(MemoryDirectory);
                File.WriteAllText(MemoryPath, JsonSerializer.Serialize(RecentConversation, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception exception)
            {
                Plugin.PluginLog.LogWarning($"Could not save chat memory: {exception.Message}");
            }
        }
    }

    public sealed class ChatTurn
    {
        public string Role { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }
}
