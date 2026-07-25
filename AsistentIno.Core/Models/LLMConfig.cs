using System.Text.Json.Serialization;

namespace AsistentIno.Models;

[JsonConverter(typeof(JsonStringEnumConverter<ProcessorType>))]
public enum ProcessorType
{
    OpenAI,
    Anthropic,
    GoogleGemini,
    OpenAIResponse
}

[JsonConverter(typeof(JsonStringEnumConverter<ReasoningEffort>))]
public enum ReasoningEffort
{
    None,
    Low,
    Medium,
    High
}

public class LLMConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "Novi LLM";
    public string Model { get; set; } = "";
    public string Endpoint { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public ProcessorType Processor { get; set; } = ProcessorType.OpenAI;
}

public class AgentConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "Novi agent";
    public string SystemPrompt { get; set; } = "Ti si virtuelni asistent za Arduino razvoj.";
    public string LlmId { get; set; } = "";
    public ReasoningEffort ReasoningEffort { get; set; } = ReasoningEffort.None;
    public List<string> EnabledTools { get; set; } = [];
}

public class AppConfig
{
    /// <summary>Folder u kojem se čuvaju Agents.json i LLMs.json. Prazno = koristi se podrazumevana putanja (apppath\DATA).</summary>
    public string DataFolder { get; set; } = "";
    public string LastOpenedFolder { get; set; } = "";
    public double WindowWidth { get; set; } = 1400;
    public double WindowHeight { get; set; } = 900;

    /// <summary>Nisu deo perzistentne konfiguracije - učitavaju se iz posebnih fajlova u DataFolder.</summary>
    [JsonIgnore]
    public List<LLMConfig> LLMs { get; set; } = [];

    [JsonIgnore]
    public List<AgentConfig> Agents { get; set; } = [];

    public string ArduinoCliPath { get; set; } = "";
}
