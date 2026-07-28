using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using AsistentIno.Models;

namespace AsistentIno.Services;

public class OpenAIProvider : ILLMProvider
{
    private readonly HttpClient _httpClient;
    private readonly ToolRegistry _tools;
    private readonly INotificationService _notification;
    public OpenAIProvider(ToolRegistry tools, HttpClient httpClient, INotificationService notification)
    {
        _tools = tools;
        _httpClient = httpClient;
        _notification = notification;
    }

    public virtual bool SupportsReasoning => true;
    public virtual string ProviderType => "openai";
    protected virtual string DefaultEndpoint => "https://api.openai.com/v1";
    protected virtual bool AddReasoningEffort => true;

    public async Task<LLMResponse> SendMessageAsync(LLMConfig config, AgentConfig agent, List<AgentMessage> history, string userMessage, MessageAttachment? attachment = null, CancellationToken cancellationToken = default)
    {
        //_notification.Notify($"{ProviderType}: sending request...");
        try
        {
            var messages = new List<object> { new { role = "system", content = agent.SystemPrompt } };
            messages.AddRange(history.Select(x => (object)new { role = x.Role, content = x.Content }));
            messages.Add(new { role = "user", content = BuildUserContent(userMessage, attachment) });

            long inputTokens = 0;
            long outputTokens = 0;
            var executedCalls = new List<ToolCall>();

            for (var iteration = 0; iteration < 12; iteration++)
            {
                var body = new Dictionary<string, object>
                {
                    ["model"] = config.Model,
                    ["messages"] = messages
                };
                var toolDefinitions = ToolDefinitions.OpenAi(_tools, agent.EnabledTools);
                if (toolDefinitions.Count > 0)
                {
                    body["tools"] = toolDefinitions;
                    body["tool_choice"] = "auto";
                }
                if (AddReasoningEffort && agent.ReasoningEffort != ReasoningEffort.None)
                    body["reasoning_effort"] = agent.ReasoningEffort.ToString().ToLowerInvariant();

                var baseUrl = string.IsNullOrWhiteSpace(config.Endpoint) ? DefaultEndpoint : config.Endpoint.TrimEnd('/');
                using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions") { Content = JsonContent.Create(body) };
                request.Headers.Authorization = new("Bearer", config.ApiKey);
                //_notification.Notify ($"Sending request to OpenAI: /chat/completions (model: {config.Model})");
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
                    if (usage.TryGetProperty("prompt_tokens", out var input)) inputTokens += input.GetInt64();
                    if (usage.TryGetProperty("completion_tokens", out var output)) outputTokens += output.GetInt64();
                }

                var message = root.GetProperty("choices")[0].GetProperty("message");
                var content = message.TryGetProperty("content", out var contentNode) && contentNode.ValueKind == JsonValueKind.String
                    ? contentNode.GetString() ?? string.Empty
                    : string.Empty;

                if (!message.TryGetProperty("tool_calls", out var toolCallsNode) || toolCallsNode.ValueKind != JsonValueKind.Array || toolCallsNode.GetArrayLength() == 0)
                    return new() { Success = true, Content = content, InputTokens = inputTokens, OutputTokens = outputTokens, TokenInfo = tokenInfo, ToolCalls = executedCalls };

                var assistantCalls = new List<object>();
                foreach (var call in toolCallsNode.EnumerateArray())
                {
                    var id = call.GetProperty("id").GetString() ?? Guid.NewGuid().ToString("N");
                    var function = call.GetProperty("function");
                    var name = function.GetProperty("name").GetString() ?? string.Empty;
                    var argumentsJson = function.GetProperty("arguments").GetString() ?? "{}";
                    assistantCalls.Add(new { id, type = "function", function = new { name, arguments = argumentsJson } });
                }
                messages.Add(new { role = "assistant", content = string.IsNullOrEmpty(content) ? null : content, tool_calls = assistantCalls });

                foreach (var call in toolCallsNode.EnumerateArray())
                {
                    var id = call.GetProperty("id").GetString() ?? string.Empty;
                    var function = call.GetProperty("function");
                    var name = function.GetProperty("name").GetString() ?? string.Empty;
                    var argumentsJson = function.GetProperty("arguments").GetString() ?? "{}";
                    using var argsDoc = JsonDocument.Parse(argumentsJson);
                    //_notification.Notify("Executing tool call: " + name);
                    var result = await _tools.ExecuteAsync(name, argsDoc.RootElement, cancellationToken);
                    executedCalls.Add(new ToolCall
                    {
                        ToolName = name,
                        Arguments = JsonSerializer.Deserialize<Dictionary<string, object>>(argumentsJson) ?? [],
                        Result = result.Content,
                        Status = result.Success ? "success" : "error"
                    });
                    messages.Add(new { role = "tool", tool_call_id = id, content = result.Content });
                }
            }

                //_notification.Notify($"{ProviderType}: finished processing with tool calls");
                return new() { Error = "Agent je prekoračio maksimalan broj uzastopnih tool poziva." };
        }
        catch (OperationCanceledException) { return new() { Error = "Operacija je otkazana." }; }
        catch (Exception ex)
        {
            _notification?.Notify($"{ProviderType}: error - {ex.Message}");
            return new() { Error = ex.Message };
        }
    }

    protected static object BuildUserContent(string userMessage, MessageAttachment? attachment)
    {
        if (attachment is null)
            return userMessage;

        if (!attachment.IsImage)
        {
            var combined = $"{userMessage}\n\n--- Prilog: {attachment.FileName} ---\n{attachment.TextContent}";
            return combined;
        }

        var parts = new List<object>
        {
            new { type = "text", text = userMessage }
        };
        parts.Add(new { type = "image_url", image_url = new { url = $"data:{attachment.MimeType};base64,{attachment.Base64Data}" } });
        return parts;
    }
}
