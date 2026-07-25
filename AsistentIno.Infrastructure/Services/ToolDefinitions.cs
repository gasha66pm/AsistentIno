namespace AsistentIno.Services;

internal static class ToolDefinitions
{
    public static IReadOnlyList<object> OpenAi(ToolRegistry registry, IEnumerable<string> enabled) => registry.GetOpenAiDefinitions(enabled);
    public static IReadOnlyList<object> Anthropic(ToolRegistry registry, IEnumerable<string> enabled) => registry.GetAnthropicDefinitions(enabled);

    public static IReadOnlyList<object> OpenAiResponse(ToolRegistry registry, IEnumerable<string> enabled) => registry.GetResponsesDefinitions(enabled);

    public static IReadOnlyList<object> Gemini(ToolRegistry registry, IEnumerable<string> enabled) => registry.GetGeminiDefinitions(enabled);
}
