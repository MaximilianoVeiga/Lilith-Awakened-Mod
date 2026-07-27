namespace LilithTextInjector;

// Rolling conversation memory persisted to memory.json.
internal static class ChatMemory
{
    private const int MaxRememberedTurns = 32;
    private static readonly object Lock = new();
    private static readonly List<ChatTurn> Turns = new();

    internal static void Load()
    {
        try
        {
            System.IO.Directory.CreateDirectory(ModDataPaths.Directory);
            if (!File.Exists(ModDataPaths.MemoryPath))
                return;
            var loaded = JsonSerializer.Deserialize<List<ChatTurn>>(File.ReadAllText(ModDataPaths.MemoryPath));
            if (loaded == null)
                return;
            lock (Lock)
            {
                Turns.Clear();
                Turns.AddRange(loaded.GetRange(
                    Math.Max(0, loaded.Count - MaxRememberedTurns),
                    Math.Min(MaxRememberedTurns, loaded.Count)));
            }
            Plugin.PluginLog.LogInfo($"Loaded {Turns.Count} remembered chat turns.");
        }
        catch (Exception exception)
        {
            Plugin.PluginLog.LogWarning($"Could not load chat memory: {exception.Message}");
        }
    }

    internal static void AddTurn(string role, string text)
    {
        lock (Lock)
        {
            Turns.Add(new ChatTurn { Role = role, Text = text });
            while (Turns.Count > MaxRememberedTurns)
                Turns.RemoveAt(0);
            try
            {
                System.IO.Directory.CreateDirectory(ModDataPaths.Directory);
                File.WriteAllText(ModDataPaths.MemoryPath,
                    JsonSerializer.Serialize(Turns, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception exception)
            {
                Plugin.PluginLog.LogWarning($"Could not save chat memory: {exception.Message}");
            }
        }
    }

    internal static List<ChatTurn> Snapshot()
    {
        lock (Lock)
            return new List<ChatTurn>(Turns);
    }

    internal static void ForEach(Action<ChatTurn> action)
    {
        lock (Lock)
        {
            foreach (var turn in Turns)
                action(turn);
        }
    }
}

internal sealed class ChatTurn
{
    public string Role { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}
