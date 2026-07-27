namespace LilithTextInjector;

// OpenAI-compatible chat completions, used for OpenAI and DeepSeek.
internal static class OpenAiCompatibleClient
{
    internal static async Task RequestAsync(string provider, string systemInstruction, string userText,
        PoseContext poseContext, bool japaneseVoiceMode,
        Action<string, string, PoseContext, bool> completeReply)
    {
        var endpoint = provider == "DeepSeek"
            ? "https://api.deepseek.com/chat/completions"
            : "https://api.openai.com/v1/chat/completions";
        var model = provider == "DeepSeek" ? Plugin.DeepSeekModel.Value.Trim() : Plugin.OpenAiModel.Value.Trim();
        var key = provider == "DeepSeek" ? Plugin.DeepSeekApiKey.Value.Trim() : Plugin.OpenAiApiKey.Value.Trim();
        var messages = BuildMessages(systemInstruction);
        var payload = new { model, messages, max_tokens = 1024, temperature = 0.8 };
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await AiHttp.Client.SendAsync(request).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"{provider} HTTP {(int)response.StatusCode}: {responseBody}");
        using var document = JsonDocument.Parse(responseBody);
        var choice = document.RootElement.GetProperty("choices")[0];
        var rawReply = choice.GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
        var finishReason = choice.TryGetProperty("finish_reason", out var finish) ? finish.GetString() ?? "UNKNOWN" : "UNKNOWN";
        var usageSummary = "unavailable";
        if (document.RootElement.TryGetProperty("usage", out var usage))
        {
            var prompt = usage.TryGetProperty("prompt_tokens", out var promptElement) ? promptElement.GetInt32() : -1;
            var output = usage.TryGetProperty("completion_tokens", out var outputElement) ? outputElement.GetInt32() : -1;
            usageSummary = $"prompt={prompt}, output={output}";
        }
        Plugin.PluginLog.LogInfo($"{provider} completed: finish={finishReason}, rawChars={rawReply.Length}, {usageSummary}.");
        completeReply(rawReply, userText, poseContext, japaneseVoiceMode);
    }

    private static object[] BuildMessages(string systemInstruction)
    {
        var messages = new List<object> { new { role = "system", content = systemInstruction } };
        ChatMemory.ForEach(turn =>
            messages.Add(new { role = turn.Role == "model" ? "assistant" : "user", content = turn.Text }));
        return messages.ToArray();
    }
}
