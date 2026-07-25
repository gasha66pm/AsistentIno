namespace AsistentIno.Services;

[Obsolete("Koristi OpenAI procesor sa DeepSeek endpointom.")]
public sealed class DeepSeekProvider : OpenAIProvider
{
    public DeepSeekProvider(ToolRegistry tools, HttpClient httpClient, INotificationService? notification = null) : base(tools, httpClient, notification) { }
}
