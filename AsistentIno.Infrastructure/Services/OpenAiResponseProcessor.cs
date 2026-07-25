using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using AsistentIno.Models;

namespace AsistentIno.Services;

public class ResponsesProvider : ILLMProvider
{
    private readonly HttpClient _httpClient;
    private readonly ToolRegistry _tools;
    private readonly INotificationService _notification;
    public ResponsesProvider(ToolRegistry tools, HttpClient httpClient, INotificationService notification )
    {
        _tools = tools;
        _httpClient = httpClient;
        _notification = notification;
    }

    public virtual bool SupportsReasoning => true;
    public virtual string ProviderType => "openairesponse";
    protected virtual string DefaultEndpoint => "https://api.openai.com/v1";
    protected virtual bool AddReasoningEffort => true;

    public async Task<LLMResponse> SendMessageAsync(LLMConfig config, AgentConfig agent, List<AgentMessage> history, string userMessage, MessageAttachment? attachment = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var inputItems = new List<object>
            {
                new { role = "system", content = agent.SystemPrompt }
            };
            inputItems.AddRange(history.Select(x => new { role = x.Role, content = x.Content }));
            inputItems.Add(new { role = "user", content = BuildUserContent(userMessage, attachment) });

            long inputTokens = 0;
            long outputTokens = 0;
            var executedCalls = new List<ToolCall>();

            for (var iteration = 0; iteration < 12; iteration++)
            {
                var body = new Dictionary<string, object>
                {
                    ["model"] = config.Model,
                    ["input"] = inputItems
                };

                var toolDefinitions = ToolDefinitions.OpenAiResponse(_tools, agent.EnabledTools);
                if (toolDefinitions.Count > 0)
                {
                    body["tools"] = toolDefinitions;
                    body["tool_choice"] = "auto";
                }

                if (AddReasoningEffort && agent.ReasoningEffort != ReasoningEffort.None)
                    body["reasoning_effort"] = agent.ReasoningEffort.ToString().ToLowerInvariant();
                _notification.Notify("Sending request to OpenAI API...");
                var baseUrl = string.IsNullOrWhiteSpace(config.Endpoint) ? DefaultEndpoint : config.Endpoint.TrimEnd('/');
                using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/responses") { Content = JsonContent.Create(body) };
                request.Headers.Authorization = new("Bearer", config.ApiKey);
                using var response = await _httpClient.SendAsync(request, cancellationToken);
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!response.IsSuccessStatusCode) return new() { Error = $"API Error: {response.StatusCode} - {json}" };
               var tokenInfo = TokenInfoProcessor.Parse(json);  
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                // Responses API može imati usage na root nivou
                if (root.TryGetProperty("usage", out var usage))
                {
                    if (usage.TryGetProperty("input_tokens", out var input)) inputTokens += input.GetInt64();
                    if (usage.TryGetProperty("output_tokens", out var output1)) outputTokens += output1.GetInt64();
                }

                var output = root.GetProperty("output");
                var content = string.Empty;
                var hasToolCalls = false;
                var assistantToolCalls = new List<object>();
                var toolResults = new List<object>();

                foreach (var item in output.EnumerateArray())
                {
                    var type = item.GetProperty("type").GetString();

                    if (type == "message")
                    {
                        var contentArray = item.GetProperty("content");
                        foreach (var contentItem in contentArray.EnumerateArray())
                        {
                            if (contentItem.GetProperty("type").GetString() == "output_text")
                            {
                                content += contentItem.GetProperty("text").GetString() ?? string.Empty;
                            }
                        }
                    }
                    else if (type == "function_call")
                    {
                        hasToolCalls = true;
                        var callId = item.GetProperty("call_id").GetString() ?? Guid.NewGuid().ToString("N");
                        var name = item.GetProperty("name").GetString() ?? string.Empty;
                        var argumentsJson = item.GetProperty("arguments").GetString() ?? "{}";

                        assistantToolCalls.Add(new
                        {
                            type = "function_call",
                            id = callId,
                            call_id = callId,
                            name,
                            arguments = argumentsJson
                        });

                        _notification.Notify("Executing tool call: " + name);
                        // Izvrši tool
                        using var argsDoc = JsonDocument.Parse(argumentsJson);
                        var result = await _tools.ExecuteAsync(name, argsDoc.RootElement, cancellationToken);
                        executedCalls.Add(new ToolCall
                        {
                            ToolName = name,
                            Arguments = JsonSerializer.Deserialize<Dictionary<string, object>>(argumentsJson) ?? new Dictionary<string, object>(),
                            Result = result.Content,
                            Status = result.Success ? "success" : "error"
                        });

                        toolResults.Add(new
                        {
                            type = "function_call_output",
                            call_id = callId,
                            output = result.Content
                        });
                    }
                }

                if (!hasToolCalls)
                {
                    return new()
                    {
                        Success = true,
                        Content = content,
                        InputTokens = inputTokens,
                        OutputTokens = outputTokens,
                        ToolCalls = executedCalls
                    };
                }

                // Dodaj asistentovu poruku sa tool calls u input
                inputItems.Add(new
                {
                    role = "assistant",
                    content = string.IsNullOrEmpty(content) ? null : content,
                    tool_calls = assistantToolCalls
                });

                // Dodaj tool rezultate u input
                foreach (var toolResult in toolResults)
                {
                    inputItems.Add(toolResult);
                }
            }

            return new() { Error = "Agent je prekoračio maksimalan broj uzastopnih tool poziva." };
        }
        catch (OperationCanceledException) { return new() { Error = "Operacija je otkazana." }; }
        catch (Exception ex) { return new() { Error = ex.Message }; }
    }

    private static object BuildUserContent(string userMessage, MessageAttachment? attachment)
    {
        if (attachment is null)
            return userMessage;

        if (!attachment.IsImage)
            return $"{userMessage}\n\n--- Prilog: {attachment.FileName} ---\n{attachment.TextContent}";

        return new List<object>
        {
            new { type = "input_text", text = userMessage },
            new { type = "input_image", image_url = $"data:{attachment.MimeType};base64,{attachment.Base64Data}" }
        };
    }
}