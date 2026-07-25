using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AsistentIno.Models;

namespace AsistentIno.Services;

public class ConfigService
{
    private const string AgentsFileName = "Agents.json";
    private const string LLMsFileName = "LLMs.json";
    private const string PricingFolderName = "Pricing";
    private const string UsageFolderName = "Usage";

    private readonly string _configPath;
    private readonly Dictionary<string, ModelPricing> _pricingByLlmId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, LlmTokenUsage> _usageByLlmId = new(StringComparer.OrdinalIgnoreCase);
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public AppConfig CurrentConfig { get; private set; } = new();

    /// <summary>Podrazumevani folder za podatke: apppath\DATA.</summary>
    public static string DefaultDataFolder => Path.Combine(AppContext.BaseDirectory, "DATA");

    /// <summary>Efektivni folder u kojem se čuvaju Agents.json i LLMs.json.</summary>
    public string DataFolder => string.IsNullOrWhiteSpace(CurrentConfig.DataFolder) ? DefaultDataFolder : CurrentConfig.DataFolder;

    public ConfigService()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AsistentIno");
        Directory.CreateDirectory(folder);
        _configPath = Path.Combine(folder, "config.json");
        LoadConfig();
    }

    public void LoadConfig()
    {
        try
        {
            CurrentConfig = File.Exists(_configPath)
                ? JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(_configPath), _jsonOptions) ?? new AppConfig()
                : new AppConfig();
        }
        catch
        {
            CurrentConfig = new AppConfig();
        }

        Directory.CreateDirectory(DataFolder);
        LoadLLMs();
        LoadAgents();
        MigrateLegacyDataIfNeeded();

        SaveConfig();
    }

    /// <summary>Migrira LLMs/Agents koji su ranije bili ugrađeni direktno u config.json.</summary>
    private void MigrateLegacyDataIfNeeded()
    {
        if (CurrentConfig.LLMs.Count > 0 || CurrentConfig.Agents.Count > 0) return;
        if (!File.Exists(_configPath)) return;

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(_configPath));
            var root = doc.RootElement;

            if (root.TryGetProperty("LLMs", out var llmsElement) && llmsElement.ValueKind == JsonValueKind.Array)
            {
                var llms = JsonSerializer.Deserialize<List<LLMConfig>>(llmsElement.GetRawText(), _jsonOptions);
                if (llms is { Count: > 0 })
                {
                    CurrentConfig.LLMs = llms;
                    SaveLLMs();
                }
            }

            if (root.TryGetProperty("Agents", out var agentsElement) && agentsElement.ValueKind == JsonValueKind.Array)
            {
                var agents = JsonSerializer.Deserialize<List<AgentConfig>>(agentsElement.GetRawText(), _jsonOptions);
                if (agents is { Count: > 0 })
                {
                    CurrentConfig.Agents = agents;
                    SaveAgents();
                }
            }
        }
        catch
        {
            // Nema šta da se migrira - ignoriši.
        }
    }

    private void LoadLLMs()
    {
        var path = Path.Combine(DataFolder, LLMsFileName);
        try
        {
            CurrentConfig.LLMs = File.Exists(path)
                ? JsonSerializer.Deserialize<List<LLMConfig>>(File.ReadAllText(path), _jsonOptions) ?? []
                : [];
        }
        catch
        {
            CurrentConfig.LLMs = [];
        }
    }

    private void LoadAgents()
    {
        var path = Path.Combine(DataFolder, AgentsFileName);
        try
        {
            CurrentConfig.Agents = File.Exists(path)
                ? JsonSerializer.Deserialize<List<AgentConfig>>(File.ReadAllText(path), _jsonOptions) ?? []
                : [];
        }
        catch
        {
            CurrentConfig.Agents = [];
        }
    }

    public void SaveConfig() => File.WriteAllText(_configPath, JsonSerializer.Serialize(CurrentConfig, _jsonOptions));

    public void SaveLLMs()
    {
        Directory.CreateDirectory(DataFolder);
        File.WriteAllText(Path.Combine(DataFolder, LLMsFileName), JsonSerializer.Serialize(CurrentConfig.LLMs, _jsonOptions));
    }

    public void SaveAgents()
    {
        Directory.CreateDirectory(DataFolder);
        File.WriteAllText(Path.Combine(DataFolder, AgentsFileName), JsonSerializer.Serialize(CurrentConfig.Agents, _jsonOptions));
    }

    public void AddLLM(LLMConfig item) { CurrentConfig.LLMs.Add(item); SaveLLMs(); }
    public void UpdateLLM(LLMConfig item) { Replace(CurrentConfig.LLMs, item.Id, item, x => x.Id); SaveLLMs(); }
    public bool RemoveLLM(string id)
    {
        if (CurrentConfig.Agents.Any(x => x.LlmId == id)) return false;
        CurrentConfig.LLMs.RemoveAll(x => x.Id == id); SaveLLMs(); return true;
    }

    public void AddAgent(AgentConfig item) { CurrentConfig.Agents.Add(item); SaveAgents(); }
    public void UpdateAgent(AgentConfig item) { Replace(CurrentConfig.Agents, item.Id, item, x => x.Id); SaveAgents(); }
    public void RemoveAgent(string id) { CurrentConfig.Agents.RemoveAll(x => x.Id == id); SaveAgents(); }

    public void SetLastOpenedFolder(string folder) { CurrentConfig.LastOpenedFolder = folder; SaveConfig(); }

    /// <summary>Menja folder u kojem se čuvaju Agents.json/LLMs.json. Premešta postojeće podatke u novi folder.</summary>
    public void SetDataFolder(string folder)
    {
        var newFolder = string.IsNullOrWhiteSpace(folder) ? DefaultDataFolder : folder;
        if (string.Equals(Path.GetFullPath(newFolder), Path.GetFullPath(DataFolder), StringComparison.OrdinalIgnoreCase))
            return;

        CurrentConfig.DataFolder = newFolder;
        Directory.CreateDirectory(DataFolder);
        SaveLLMs();
        SaveAgents();
        SaveConfig();
    }
    public void SetArduinoCliPath(string path) { CurrentConfig.ArduinoCliPath = path; SaveConfig(); }
    private static void Replace<T>(List<T> list, string id, T item, Func<T, string> key)
    {
        var index = list.FindIndex(x => key(x) == id);
        if (index >= 0) list[index] = item;
    }

    // --- Cenovnik po LLM-u (Pricing\{LlmId}.json) ---

    private string PricingFolder => Path.Combine(DataFolder, PricingFolderName);
    private string PricingPath(string llmId) => Path.Combine(PricingFolder, $"{llmId}.json");

    /// <summary>Vraća cenovnik za dati LLM. Ako ne postoji, vraća prazan cenovnik (sve cene 0) vezan za model LLM-a.</summary>
    public ModelPricing GetPricing(LLMConfig llm)
    {
        if (_pricingByLlmId.TryGetValue(llm.Id, out var cached)) return cached;

        var path = PricingPath(llm.Id);
        ModelPricing pricing;
        try
        {
            pricing = File.Exists(path)
                ? JsonSerializer.Deserialize<ModelPricing>(File.ReadAllText(path), _jsonOptions) ?? new ModelPricing { Model = llm.Model }
                : new ModelPricing { Model = llm.Model };
        }
        catch
        {
            pricing = new ModelPricing { Model = llm.Model };
        }

        _pricingByLlmId[llm.Id] = pricing;
        return pricing;
    }

    public void SavePricing(string llmId, ModelPricing pricing)
    {
        _pricingByLlmId[llmId] = pricing;
        Directory.CreateDirectory(PricingFolder);
        File.WriteAllText(PricingPath(llmId), JsonSerializer.Serialize(pricing, _jsonOptions));
    }

    // --- Kumulativna potrošnja po LLM-u (Usage\{LlmId}.json) ---

    private string UsageFolder => Path.Combine(DataFolder, UsageFolderName);
    private string UsagePath(string llmId) => Path.Combine(UsageFolder, $"{llmId}.json");

    public LlmTokenUsage GetUsage(string llmId)
    {
        if (_usageByLlmId.TryGetValue(llmId, out var cached)) return cached;

        var path = UsagePath(llmId);
        LlmTokenUsage usage;
        try
        {
            usage = File.Exists(path)
                ? JsonSerializer.Deserialize<LlmTokenUsage>(File.ReadAllText(path), _jsonOptions) ?? new LlmTokenUsage { LlmId = llmId }
                : new LlmTokenUsage { LlmId = llmId };
        }
        catch
        {
            usage = new LlmTokenUsage { LlmId = llmId };
        }

        _usageByLlmId[llmId] = usage;
        return usage;
    }

    public void SaveUsage(LlmTokenUsage usage)
    {
        _usageByLlmId[usage.LlmId] = usage;
        Directory.CreateDirectory(UsageFolder);
        File.WriteAllText(UsagePath(usage.LlmId), JsonSerializer.Serialize(usage, _jsonOptions));
    }

    public void ResetUsage(string llmId)
    {
        var usage = GetUsage(llmId);
        usage.Reset();
        SaveUsage(usage);
    }

    /// <summary>
    /// Registruje potrošnju tokena za jedan poziv LLM-a: obračunava trošak na osnovu
    /// registrovanog cenovnika za taj LLM i ažurira/perzistira kumulativnu potrošnju.
    /// </summary>
    public LlmTokenUsage RegisterUsage(LLMConfig llm, TokenInfo info)
    {
        info.Model ??= llm.Model;
        var pricing = GetPricing(llm);
        var calculator = new TokenCostCalculator([pricing]);
        // Kalkulator traži tačan naziv modela; poravnaj sa cenovnikom ako se razlikuje.
        info.Model = pricing.Model;
        var breakdown = calculator.Calculate(info);

        var usage = GetUsage(llm.Id);
        usage.Add(info, breakdown);
        SaveUsage(usage);
        return usage;
    }
}
