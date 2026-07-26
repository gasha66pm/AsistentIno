using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using AsistentIno.Models;
 
namespace AsistentIno.Services;

public class AnthropicProvider : ILLMProvider
{
    private readonly HttpClient _httpClient;
    private readonly ToolRegistry _tools;
    private readonly INotificationService? _notification;

    public AnthropicProvider(ToolRegistry tools, HttpClient httpClient, INotificationService? notification = null)
    {
        _tools = tools;
        _httpClient = httpClient;
        _notification = notification;
    }

    public bool SupportsReasoning => true;
    public string ProviderType => "anthropic";

    public async Task<LLMResponse> SendMessageAsync(LLMConfig config, AgentConfig agent, List<AgentMessage> history, string userMessage, MessageAttachment? attachment = null, CancellationToken cancellationToken = default)
    {
        _notification?.Notify($"{ProviderType}: sending request...");
        try
        {
            var messages = history.Select(x => (object)new { role = x.Role, content = x.Content }).ToList();
            messages.Add(new { role = "user", content = BuildUserContent(userMessage, attachment) });
            long inputTokens = 0;
            long outputTokens = 0;
            long cacheTokens = 0;
            var executedCalls = new List<ToolCall>();

            for (var iteration = 0; iteration < 12; iteration++)
            {
                var body = new Dictionary<string, object>
                {
                    ["model"] = config.Model,
                    ["max_tokens"] = 4096,
                    ["system"] = agent.SystemPrompt,
                    ["messages"] = messages
                };
                var toolDefinitions = ToolDefinitions.Anthropic(_tools, agent.EnabledTools);
                if (toolDefinitions.Count > 0)
                    body["tools"] = toolDefinitions;
                if (agent.ReasoningEffort != ReasoningEffort.None)
                {
                    body["max_tokens"] = 16000;
                    body["thinking"] = new
                    {
                        type = "enabled",
                        budget_tokens = agent.ReasoningEffort switch
                        {
                            ReasoningEffort.Low => 1024,
                            ReasoningEffort.Medium => 5000,
                            ReasoningEffort.High => 10000,
                            _ => 1024
                        }
                    };
                }

                var baseUrl = string.IsNullOrWhiteSpace(config.Endpoint) ? "https://api.anthropic.com/v1" : config.Endpoint.TrimEnd('/');
                using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/messages") { Content = JsonContent.Create(body) };
                request.Headers.Add("x-api-key", config.ApiKey);
                request.Headers.Add("anthropic-version", "2023-06-01");
                using var response = await _httpClient.SendAsync(request, cancellationToken);
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _notification?.Notify($"{ProviderType}: API error {response.StatusCode}");
                    return new() { Error = $"API Error: {response.StatusCode} - {json}" };
                }

                var tokenInfo = TokenInfoProcessor.Parse(json);
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                if (root.TryGetProperty("usage", out var usage))
                {
                    if (usage.TryGetProperty("input_tokens", out var input)) inputTokens += input.GetInt64();
                    if (usage.TryGetProperty("output_tokens", out var output)) outputTokens += output.GetInt64();
                    if (usage.TryGetProperty("cache_creation_input_tokens", out var cache)) cacheTokens += cache.GetInt64();
                    if (usage.TryGetProperty("cache_read_input_tokens", out var cacheRead)) cacheTokens += cacheRead.GetInt64();
                }

                var contentBlocks = root.GetProperty("content").EnumerateArray().Select(x => x.Clone()).ToList();
                var text = string.Join("\n", contentBlocks
                    .Where(x => x.TryGetProperty("type", out var type) && type.GetString() == "text")
                    .Select(x => x.GetProperty("text").GetString())
                    .Where(x => !string.IsNullOrWhiteSpace(x)));
                var toolUses = contentBlocks.Where(x => x.TryGetProperty("type", out var type) && type.GetString() == "tool_use").ToList();

                if (toolUses.Count == 0)
                    return new() { Success = true, Content = text, InputTokens = inputTokens, OutputTokens = outputTokens, CacheTokens = cacheTokens, TokenInfo = tokenInfo, ToolCalls = executedCalls };

                messages.Add(new { role = "assistant", content = contentBlocks });
                var resultBlocks = new List<object>();
                foreach (var toolUse in toolUses)
                {
                    var id = toolUse.GetProperty("id").GetString() ?? string.Empty;
                    var name = toolUse.GetProperty("name").GetString() ?? string.Empty;
                    var input = toolUse.GetProperty("input");
                    var result = await _tools.ExecuteAsync(name, input, cancellationToken);
                    executedCalls.Add(new ToolCall
                    {
                        ToolName = name,
                        Arguments = JsonSerializer.Deserialize<Dictionary<string, object>>(input.GetRawText()) ?? [],
                        Result = result.Content,
                        Status = result.Success ? "success" : "error"
                    });
                    resultBlocks.Add(new { type = "tool_result", tool_use_id = id, content = result.Content, is_error = !result.Success });
                }
                messages.Add(new { role = "user", content = resultBlocks });
            }

            //_notification?.Notify($"{ProviderType}: finished processing with tool calls");
            return new() { Error = "Agent je prekoračio maksimalan broj uzastopnih tool poziva." };
        }
        catch (OperationCanceledException) { return new() { Error = "Operacija je otkazana." }; }
        catch (Exception ex)
        {
            _notification?.Notify($"{ProviderType}: error - {ex.Message}");
            return new() { Error = ex.Message };
        }
    }

    private static object BuildUserContent(string userMessage, MessageAttachment? attachment)
    {
        if (attachment is null)
            return userMessage;

        if (!attachment.IsImage)
            return $"{userMessage}\n\n--- Prilog: {attachment.FileName} ---\n{attachment.TextContent}";

        return new List<object>
        {
            new { type = "image", source = new { type = "base64", media_type = attachment.MimeType, data = attachment.Base64Data } },
            new { type = "text", text = userMessage }
        };
    }
}
