using System.Text.Json;
using System.Text.Json.Nodes;

namespace AsistentIno.Services;

public sealed class ToolRegistry
{
    private readonly FileService _files;
    private readonly IArduinoCliService _arduinoCli;
    private readonly AsistentIno.Services.INotificationService? _notification;
    private readonly Dictionary<string, RegisteredTool> _tools;

    public ToolRegistry(FileService files, IArduinoCliService arduinoCli, AsistentIno.Services.INotificationService? notification = null)
    {
        _files = files;
        _arduinoCli = arduinoCli;
        _notification = notification;
        _tools = new(StringComparer.OrdinalIgnoreCase)
        {
            ["interactive.ask"] = Register(
                Descriptor(
                    "interactive.ask", "interactive_ask",
                    "Postavlja korisniku pitanje ili traži eksplicitnu dozvolu za nastavak. Koristi kada je odluka korisnika neophodna.",
                    ObjectSchema(
                        ("question", StringSchema("Jasno pitanje korisniku", true)),
                        ("details", StringSchema("Dodatni kontekst")),
                        ("options", ArraySchema("Ponuđeni odgovori")),
                        ("allowFreeText", BooleanSchema("Dozvoli slobodan unos")),
                        ("requiresApproval", BooleanSchema("Da li se traži dozvola za nastavak")))),
                ExecuteInteractiveAskAsync),

            ["file.writetext"] = Register(
                Descriptor(
                    "file.writetext", "file_writetext",
                    "Upisuje kompletan tekst u fajl unutar trenutno otvorenog workspace foldera.",
                    ObjectSchema(
                        ("path", StringSchema("Relativna putanja fajla", true)),
                        ("content", StringSchema("Kompletan sadržaj fajla", true)))),
                (args, _) =>
                {
                    var path = RequireString(args, "path");
                    var fullPath = _files.ResolveWorkspacePath(path);
                    _files.WriteFile(fullPath, RequireString(args, "content"));
                    return Task.FromResult(ToolExecutionResult.Ok($"Sačuvan fajl: {path}"));
                }),

            ["file.readtext"] = Register(
                Descriptor(
                    "file.readtext", "file_readtext",
                    "Čita tekstualni fajl unutar trenutno otvorenog workspace foldera.",
                    ObjectSchema(("path", StringSchema("Relativna putanja fajla", true)))),
                (args, _) =>
                {
                    var path = RequireString(args, "path");
                    var content = _files.ReadFile(_files.ResolveWorkspacePath(path));
                    return Task.FromResult(ToolExecutionResult.Ok(content));
                }),

            ["file.list"] = Register(
                Descriptor(
                    "file.list", "file_list",
                    "Lista fajlove i podfoldere unutar trenutno otvorenog workspace foldera.",
                    ObjectSchema(
                        ("path", StringSchema("Relativni podfolder; prazno za root")),
                        ("recursive", BooleanSchema("Rekurzivno listanje")))),
                (args, _) =>
                {
                    var path = GetString(args, "path") ?? string.Empty;
                    var recursive = GetBool(args, "recursive");
                    var items = _files.ListWorkspaceEntries(path, recursive);
                    return Task.FromResult(ToolExecutionResult.Ok(items.Count == 0 ? "(nema fajlova)" : string.Join('\n', items)));
                }),

            //["arduinocli.listallboards"] = Register(
            //    Descriptor(
            //        "arduinocli.listallboards", "arduinocli_listallboards",
            //        "Lista sve board platforme poznate Arduino CLI-ju.",
            //        ObjectSchema()),
            //    async (_, ct) => ToolExecutionResult.Ok(await _arduinoCli.ListAllBoardsAsync(ct))),
            ["arduinocli.searchboards"] = Register(
                Descriptor(
                    "arduinocli.searchboards", "arduinocli_searchboards",
                    "Pretražuje board platforme poznate Arduino CLI-ju.",
                    ObjectSchema(("boardname", StringSchema("Naziv boarda", true)))),
                async (args, ct) => {
                    var boardname =RequireString(args, "boardname");
                    return ToolExecutionResult.Ok(await _arduinoCli.SearchBoardsAsync(boardname, ct)); 
                }),
            ["arduinocli.searchlibs"] = Register(
                Descriptor(
                    "arduinocli.searchlibs", "arduinocli_searchlibs",
                    "Pretražuje biblioteke poznate Arduino CLI-ju. libstring moze da bude deo imena ili ako se trazi lib po tacnom nazivu libstring je name=<naziv_biblioteke>",
                    ObjectSchema(("libstring", StringSchema("Naziv biblioteke", true)))),
                async (args, ct) => {
                    var libstring = RequireString(args, "libstring");
                    return ToolExecutionResult.Ok(await _arduinoCli.SearchLibrariesAsync(libstring, ct));
                }),
            //["arduinocli.listalllibs"] = Register(
            //    Descriptor(
            //        "arduinocli.listalllibs", "arduinocli_listalllibs",
            //        "Lista svih Arduino biblioteka poznatih Arduino CLI-ju.",
            //        ObjectSchema()),
            //    async (_, ct) => ToolExecutionResult.Ok(await _arduinoCli.ListAllLibrariesAsync(ct))),
            ["arduinocli.installedlibs"] = Register(
                Descriptor(
                    "arduinocli.installedlibs", "arduinocli_installedlibs",
                    "Lista instaliranih Arduino biblioteka.",
                    ObjectSchema()),
                async (_, ct) => ToolExecutionResult.Ok(await _arduinoCli.ListInstalledLibrariesAsync(ct))),

            ["arduinocli.compile"] = Register(
                Descriptor(
                    "arduinocli.compile", "arduinocli_compile",
                    "Kompajlira Arduino sketch za zadati FQBN.",
                    ObjectSchema(
                        ("sketchPath", StringSchema("Relativna putanja sketch foldera ili .ino fajla", true)),
                        ("fqbn", StringSchema("Fully Qualified Board Name", true)))),
                async (args, ct) =>
                {
                    var path = _files.ResolveWorkspacePath(RequireString(args, "sketchPath"));
                    var output = await _arduinoCli.CompileAsync(path, RequireString(args, "fqbn"), ct);
                    return ToolExecutionResult.Ok(output);
                })
        };
    }

    public Func<InteractiveAskRequest, CancellationToken, Task<InteractiveAskResponse>>? InteractiveAskHandler { get; set; }

    public event Action<string>? StatusChanged;

    public IReadOnlyList<object> GetOpenAiDefinitions(IEnumerable<string> enabledToolIds) =>
        Enabled(enabledToolIds).Select(x => (object)new
        {
            type = "function",
            function = new { name = x.Descriptor.ApiName, description = x.Descriptor.Description, parameters = x.Descriptor.InputSchema }
        }).ToList();

    public IReadOnlyList<object> GetAnthropicDefinitions(IEnumerable<string> enabledToolIds) =>
        Enabled(enabledToolIds).Select(x => (object)new
        {
            name = x.Descriptor.ApiName,
            description = x.Descriptor.Description,
            input_schema = x.Descriptor.InputSchema
        }).ToList();

    public IReadOnlyList<object> GetResponsesDefinitions(IEnumerable<string> enabledToolIds) =>
    Enabled(enabledToolIds).Select(x => (object)new
    {
        type = "function",
        name = x.Descriptor.ApiName,
        description = x.Descriptor.Description,
        parameters = x.Descriptor.InputSchema
    }).ToList();

    public IReadOnlyList<object> GetGeminiDefinitions(IEnumerable<string> enabledToolIds) =>
        Enabled(enabledToolIds).Select(x => (object)new
        {
            type = "function",
            name = x.Descriptor.ApiName,
            description = x.Descriptor.Description,
            parameters = x.Descriptor.InputSchema
        }).ToList();

    public async Task<ToolExecutionResult> ExecuteAsync(string apiName, JsonElement arguments, CancellationToken cancellationToken)
    {
        var tool = _tools.Values.FirstOrDefault(x => x.Descriptor.ApiName.Equals(apiName, StringComparison.OrdinalIgnoreCase));
        if (tool is null)
        {
            var msg = $"Nepoznat tool: {apiName}";
            StatusChanged?.Invoke(msg);
            _notification?.Notify(msg);
            return ToolExecutionResult.Fail($"Nepoznat tool: {apiName}");
        }
        var startMsg = $"Pozivam alat: {tool.Descriptor.Id}...";
        var path = string.Empty;
        if (tool.Descriptor.Id == "file.readtext" || tool.Descriptor.Id == "file.writetext")
        {
             path = arguments.TryGetProperty("path", out var pathProp) && pathProp.ValueKind == JsonValueKind.String
                ? pathProp.GetString() ?? string.Empty
                : string.Empty;
            if (!string.IsNullOrEmpty(path))
            {
                path = " fajl: " + path;
                startMsg += $" ({path})";
            }
        }
        
        StatusChanged?.Invoke(startMsg);
        _notification?.Notify(startMsg);
        try
        {
            var result = await tool.Handler(arguments, cancellationToken);
            var finished = result.Success
                ? $"Alat {tool.Descriptor.Id} uspešno završen. {path}"
                : $"Greška u alatu {tool.Descriptor.Id}: {result.Content}";
            StatusChanged?.Invoke(finished);
            _notification?.Notify(finished);
            return result;
        }
        catch (OperationCanceledException)
        {
            var canceled = $"Alat {tool.Descriptor.Id} je otkazan.";
            StatusChanged?.Invoke(canceled);
            _notification?.Notify(canceled);
            return ToolExecutionResult.Fail("Operacija je otkazana.");
        }
        catch (Exception ex)
        {
            var err = $"Greška u alatu {tool.Descriptor.Id}: {ex.Message}";
            StatusChanged?.Invoke(err);
            _notification?.Notify(err);
            return ToolExecutionResult.Fail(ex.Message);
        }
    }

    private async Task<ToolExecutionResult> ExecuteInteractiveAskAsync(JsonElement args, CancellationToken ct)
    {
        if (InteractiveAskHandler is null)
            return ToolExecutionResult.Fail("Interaktivni handler nije konfigurisan.");

        var request = new InteractiveAskRequest
        {
            Question = RequireString(args, "question"),
            Details = GetString(args, "details") ?? string.Empty,
            Options = GetStringArray(args, "options"),
            AllowFreeText = !args.TryGetProperty("allowFreeText", out var free) || free.ValueKind != JsonValueKind.False,
            RequiresApproval = GetBool(args, "requiresApproval")
        };
        var response = await InteractiveAskHandler(request, ct);
        return ToolExecutionResult.Ok(JsonSerializer.Serialize(new
        {
            approved = response.Approved,
            answer = response.Answer,
            cancelled = response.Cancelled
        }));
    }

    private IEnumerable<RegisteredTool> Enabled(IEnumerable<string> ids)
    {
        var enabled = new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);
        return _tools.Values.Where(x => enabled.Contains(x.Descriptor.Id));
    }

    private static RegisteredTool Register(ToolDescriptor descriptor, Func<JsonElement, CancellationToken, Task<ToolExecutionResult>> handler) => new(descriptor, handler);

    private static ToolDescriptor Descriptor(string id, string apiName, string description, JsonObject schema) => new(id, apiName, description, schema);

    private static JsonObject ObjectSchema(params (string Name, JsonObject Schema)[] properties)
    {
        var props = new JsonObject();
        var required = new JsonArray();
        foreach (var (name, schema) in properties)
        {
            var isRequired = schema["x-required"]?.GetValue<bool>() ?? false;
            schema.Remove("x-required");
            props[name] = schema;
            if (isRequired) required.Add(name);
        }
        var result = new JsonObject { ["type"] = "object", ["properties"] = props, ["additionalProperties"] = false };
        if (required.Count > 0) result["required"] = required;
        return result;
    }

    private static JsonObject StringSchema(string description, bool required = false) => new() { ["type"] = "string", ["description"] = description, ["x-required"] = required };
    private static JsonObject BooleanSchema(string description) => new() { ["type"] = "boolean", ["description"] = description };
    private static JsonObject ArraySchema(string description) => new() { ["type"] = "array", ["description"] = description, ["items"] = new JsonObject { ["type"] = "string" } };

    private static string RequireString(JsonElement args, string name) => GetString(args, name) is { Length: > 0 } value ? value : throw new ArgumentException($"Nedostaje obavezan argument '{name}'.");
    private static string? GetString(JsonElement args, string name) => args.ValueKind == JsonValueKind.Object && args.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static bool GetBool(JsonElement args, string name) => args.ValueKind == JsonValueKind.Object && args.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
    private static List<string> GetStringArray(JsonElement args, string name) => args.ValueKind == JsonValueKind.Object && args.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToList() : [];

    private sealed record RegisteredTool(ToolDescriptor Descriptor, Func<JsonElement, CancellationToken, Task<ToolExecutionResult>> Handler);
}

public sealed record ToolDescriptor(string Id, string ApiName, string Description, JsonObject InputSchema);

public sealed class ToolExecutionResult
{
    public bool Success { get; init; }
    public string Content { get; init; } = string.Empty;
    public static ToolExecutionResult Ok(string content) => new() { Success = true, Content = content };
    public static ToolExecutionResult Fail(string error) => new() { Success = false, Content = error };
}

public sealed class InteractiveAskRequest
{
    public string Question { get; init; } = string.Empty;
    public string Details { get; init; } = string.Empty;
    public List<string> Options { get; init; } = [];
    public bool AllowFreeText { get; init; } = true;
    public bool RequiresApproval { get; init; }
}

public sealed class InteractiveAskResponse
{
    public bool Approved { get; init; }
    public string Answer { get; init; } = string.Empty;
    public bool Cancelled { get; init; }
}
