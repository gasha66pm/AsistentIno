using AsistentIno.Models;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AsistentIno.Services;

public class ArduinoCliService : IArduinoCliService
{
    private string _arduinoCliPath;

    public string ArduinoCliPath => _arduinoCliPath;

    public ArduinoCliService(string? customPath = null)
    {
        _arduinoCliPath = GetEffectivePath(customPath);
    }

    public void SetArduinoCliPath(string? path) => _arduinoCliPath = GetEffectivePath(path);

    public Task<string> GetVersionAsync(
    CancellationToken cancellationToken = default) =>
    RunCommandAsync(["version", "--json"], cancellationToken);
    public Task<string> ListAllBoardsAsync(CancellationToken cancellationToken = default) =>
        RunCommandAsync(["board", "listall"], cancellationToken);
    private Task<string> ListInstalledBoardsAsync(CancellationToken cancellationToken = default) =>
        RunCommandAsync(["board", "listall", "--format", "json"], cancellationToken);
    public async Task<IReadOnlyList<BoardProfile>> GetBoardProfilesAsync(
        CancellationToken cancellationToken = default)
    {
        string result = await ListInstalledBoardsAsync(cancellationToken);
        if ( result is null)
        {
            return [];
        }
        var bpJson = JsonNode.Parse(result);
        var boards = new Dictionary<string, BoardProfile>(StringComparer.OrdinalIgnoreCase);
        CollectBoards(bpJson, boards);
        return boards.Values.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }
    private static JsonNode? TryParseJson(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(value);
        }
        catch (JsonException ex)
        {
            return null;
        }
        catch (Exception ex)
        { return null; }
    }
    private static void CollectBoards(JsonNode node, IDictionary<string, BoardProfile> result)
    {
        if (node is JsonObject obj)
        {
            string? fqbn = obj["fqbn"]?.GetValue<string>();
            string? name = obj["name"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(fqbn) && !string.IsNullOrWhiteSpace(name))
            {
                result[fqbn] = new BoardProfile { Name = name, Fqbn = fqbn };
            }
            foreach ((_, JsonNode? child) in obj) if (child is not null) CollectBoards(child, result);
        }
        else if (node is JsonArray array)
        {
            foreach (JsonNode? child in array) if (child is not null) CollectBoards(child, result);
        }
    }
    public Task<string> SearchBoardsAsync(string boardname,CancellationToken cancellationToken = default) =>
    RunCommandAsync(["board", "search", boardname], cancellationToken);

    public Task<string> SearchLibrariesAsync(string libstring, CancellationToken cancellationToken = default) =>
        RunCommandAsync(["lib", "search", libstring], cancellationToken);

    public Task<string> ListAllLibrariesAsync(CancellationToken cancellationToken = default) =>
        RunCommandAsync(["lib", "list","--all"], cancellationToken);

    public Task<string> ListInstalledLibrariesAsync(CancellationToken cancellationToken = default) =>
        RunCommandAsync(["lib", "list"], cancellationToken);

    public Task<string> CompileAsync(string sketchPath, string fqbn, CancellationToken cancellationToken, IProgress<string>? progress = null) =>
        RunCommandAsync(["compile", "--fqbn", fqbn, sketchPath], cancellationToken, progress);

    public Task<string> UploadAsync(string sketchPath, string port, string fqbn, CancellationToken cancellationToken, IProgress<string>? progress = null) =>
    RunCommandAsync(["upload", sketchPath, "-p", port,"-b", fqbn], cancellationToken, progress);

    public async Task<List<string>> ListBoardsAsync() =>
        (await RunCommandAsync(["board", "list"], CancellationToken.None))
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Skip(1).ToList();

    public async Task<List<string>> ListLibrariesAsync() =>
        (await ListAllLibrariesAsync())
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Skip(1).ToList();

    public Task<string> CompileAsync(string sketchPath, string fqbn) => CompileAsync(sketchPath, fqbn, CancellationToken.None);

    private async Task<string> RunCommandAsync(IReadOnlyList<string> args, CancellationToken cancellationToken, IProgress<string>? progress = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _arduinoCliPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var arg in args) startInfo.ArgumentList.Add(arg);

        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Arduino CLI nije moguće pokrenuti: {ex.Message}", ex);
        }

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        var stdoutTask = ReadStreamAsync(process.StandardOutput, stdout, progress, cancellationToken);
        var stderrTask = ReadStreamAsync(process.StandardError, stderr, progress, cancellationToken);
        await Task.WhenAll(process.WaitForExitAsync(cancellationToken), stdoutTask, stderrTask);

        if (process.ExitCode != 0)
            throw new InvalidOperationException(stderr.Length == 0 ? $"arduino-cli je završio kodom {process.ExitCode}." : stderr.ToString().Trim());

        var standardOutput = stdout.ToString().Trim();
        var standardError = stderr.ToString().Trim();
        return string.IsNullOrWhiteSpace(standardError) ? standardOutput : $"{standardOutput}\n{standardError}".Trim();
    }

    private static async Task ReadStreamAsync(
        StreamReader reader,
        StringBuilder output,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            output.AppendLine(line);
            progress?.Report(line);
        }
    }

    private static string GetEffectivePath(string? path) =>
        string.IsNullOrWhiteSpace(path) ? "arduino-cli.exe" : path.Trim();
}
