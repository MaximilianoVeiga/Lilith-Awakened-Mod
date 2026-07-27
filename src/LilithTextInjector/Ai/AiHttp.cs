namespace LilithTextInjector;

// Shared HTTP client for AI provider and note generation requests.
internal static class AiHttp
{
    internal static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(90) };
}
