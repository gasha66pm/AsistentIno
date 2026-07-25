using AsistentIno.Models;

namespace AsistentIno.Services;

public interface ILLMProvider
{
    Task<LLMResponse> SendMessageAsync(
        LLMConfig config,
        AgentConfig agent,
        List<AgentMessage> conversationHistory,
        string userMessage,
        MessageAttachment? attachment = null,
        CancellationToken cancellationToken = default);

    bool SupportsReasoning { get; }
    string ProviderType { get; }
}

public class LLMResponse
{
    public string Content { get; set; } = "";
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    public long CacheTokens { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
    public TokenInfo? TokenInfo { get; set; }
    public List<ToolCall> ToolCalls { get; set; } = [];
}
