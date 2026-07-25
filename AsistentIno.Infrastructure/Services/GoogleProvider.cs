using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using AsistentIno.Models;

namespace AsistentIno.Services;

public class GeminiProvider : ILLMProvider
{
    private readonly HttpClient _httpClient;
    private readonly ToolRegistry _tools;
    private readonly INotificationService _notification;

    public GeminiProvider(ToolRegistry tools, HttpClient httpClient, INotificationService notification)
    {
        _tools = tools;
        _httpClient = httpClient;
        _notification = notification;
    }

    public virtual bool SupportsReasoning => false;
    public virtual string ProviderType => "gemini";
    protected virtual string DefaultEndpoint => "https://generativelanguage.googleapis.com/v1beta";

    public async Task<LLMResponse> SendMessageAsync(LLMConfig config, AgentConfig agent, List<AgentMessage> history, string userMessage, MessageAttachment? attachment = null, CancellationToken cancellationToken = default)
    {
        try
        {
            long inputTokens = 0;
            long outputTokens = 0;
            var executedCalls = new List<ToolCall>();

            var effectiveMessage = userMessage;
            if (attachment is not null && !attachment.IsImage)
                effectiveMessage = $"{userMessage}\n\n--- Prilog: {attachment.FileName} ---\n{attachment.TextContent}";

            var userContentParts = new List<object> { new { type = "text", text = effectiveMessage } };
            if (attachment is not null && attachment.IsImage)
                userContentParts.Add(new { type = "image", data = attachment.Base64Data, mime_type = attachment.MimeType });

            // Konstruiši input kao niz content blokova
            var inputItems = new List<object>
            {
                new
                {
                    type = "user_input",
                    content = userContentParts
                }
            };

            string? previousInteractionId = null;

            for (var iteration = 0; iteration < 20; iteration++)
            {
                var body = new Dictionary<string, object>
                {
                    ["model"] = config.Model,
                    ["store"] = true,  // Stateful mode - server upravlja istorijom
                    ["input"] = inputItems
                };

                // system_instruction je posebno polje, ne u user_input
                if (!string.IsNullOrWhiteSpace(agent.SystemPrompt))
                    body["system_instruction"] = agent.SystemPrompt;

                var toolDefinitions = _tools.GetGeminiDefinitions(agent.EnabledTools);
                if (toolDefinitions.Count > 0)
                {
                    body["tools"] = toolDefinitions;
                }

                if (previousInteractionId != null)
                    body["previous_interaction_id"] = previousInteractionId;

                var baseUrl = string.IsNullOrWhiteSpace(config.Endpoint) ? DefaultEndpoint : config.Endpoint.TrimEnd('/');
                var url = $"{baseUrl}/interactions";
                _notification.Notify($"Slanje poruke na Gemini: {url} (model: {config.Model})");
                using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
                request.Headers.Add("x-goog-api-key", config.ApiKey);
                using var response = await _httpClient.SendAsync(request, cancellationToken);
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!response.IsSuccessStatusCode) return new() { Error = $"API Error: {response.StatusCode} - {json}" };

                var tokenInfo = TokenInfoProcessor.Parse(json);
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                if (root.TryGetProperty("error", out var errorNode))
                {
                    var errorMsg = errorNode.TryGetProperty("message", out var msgNode) ? msgNode.GetString() : json;
                    return new() { Error = $"API Error: {errorMsg}" };
                }

                if (root.TryGetProperty("usage_metadata", out var usage))
                {
                    if (usage.TryGetProperty("prompt_token_count", out var input)) inputTokens += input.GetInt64();
                    if (usage.TryGetProperty("candidates_token_count", out var output)) outputTokens += output.GetInt64();
                }

                if (root.TryGetProperty("id", out var idNode))
                    previousInteractionId = idNode.GetString();

                var steps = root.GetProperty("steps");
                var content = string.Empty;
                var hasToolCalls = false;
                var toolResults = new List<object>();

                foreach (var step in steps.EnumerateArray())
                {
                    var stepType = step.GetProperty("type").GetString();

                    if (stepType == "model_output")
                    {
                        var stepContent = step.GetProperty("content");
                        foreach (var part in stepContent.EnumerateArray())
                        {
                            if (part.GetProperty("type").GetString() == "text")
                            {
                                content += part.GetProperty("text").GetString() ?? string.Empty;
                            }
                        }
                    }
                    else if (stepType == "function_call")
                    {
                        hasToolCalls = true;
                        var callId = step.GetProperty("id").GetString() ?? Guid.NewGuid().ToString("N");
                        var name = step.GetProperty("name").GetString() ?? string.Empty;
                        var args = step.GetProperty("arguments");
                        var argsJson = args.GetRawText();
                        _notification.Notify("Gemini je zatražio poziv alata: " + name);
                        var result = await _tools.ExecuteAsync(name, args, cancellationToken);
                        executedCalls.Add(new ToolCall
                        {
                            ToolName = name,
                            Arguments = JsonSerializer.Deserialize<Dictionary<string, object>>(argsJson) ?? [],
                            Result = result.Content,
                            Status = result.Success ? "success" : "error"
                        });

                        // function_result: result može biti string (prema Python primeru)
                        toolResults.Add(new
                        {
                            type = "function_result",
                            name = name,
                            call_id = callId,
                            result = result.Content  // String, ne niz blokova
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
                        TokenInfo = tokenInfo,
                        ToolCalls = executedCalls
                    };
                }

                // Sledeći zahtev: samo function_result steps sa previous_interaction_id
                // (server upravlja istorijom zbog store=true)
                inputItems = toolResults.Cast<object>().ToList();
            }

            return new() { Error = "Agent je prekoračio maksimalan broj uzastopnih tool poziva." };
        }
        catch (OperationCanceledException) { return new() { Error = "Operacija je otkazana." }; }
        catch (Exception ex) { return new() { Error = ex.Message }; }
    }
}
