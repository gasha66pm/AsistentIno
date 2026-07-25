using System.Net.Http;
using AsistentIno.Models;
using AsistentIno.Services;

namespace AsistentIno.Services;

public class LLMProviderFactory
{
    private readonly Dictionary<ProcessorType, ILLMProvider> _providers;

    public LLMProviderFactory(ToolRegistry tools, INotificationService notification) : this(tools, new HttpClient { Timeout = TimeSpan.FromMinutes(20) }, notification)
    {
    }

    public LLMProviderFactory(ToolRegistry tools, HttpClient httpClient, INotificationService notification)
    {
        _providers = new()
        {
            [ProcessorType.OpenAI] = new OpenAIProvider(tools, httpClient, notification),
            [ProcessorType.Anthropic] = new AnthropicProvider(tools, httpClient, notification),
            [ProcessorType.GoogleGemini] = new GeminiProvider(tools, httpClient, notification),
            [ProcessorType.OpenAIResponse] = new ResponsesProvider(tools, httpClient, notification),
        };
    }

    public ILLMProvider? GetProvider(ProcessorType processor) => _providers.GetValueOrDefault(processor);
}
