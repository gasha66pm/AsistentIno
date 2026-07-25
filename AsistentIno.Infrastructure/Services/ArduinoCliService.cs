using System.Diagnostics;

namespace AsistentIno.Services;

public class ArduinoCliService
{
    private readonly string _arduinoCliPath;

    public Task<string> GetVersionAsync(
    CancellationToken cancellationToken = default) =>
    RunCommandAsync(["version", "--json"], cancellationToken);
    public ArduinoCliService(string? customPath = null) => _arduinoCliPath = customPath ?? "arduino-cli.exe";

    public Task<string> ListAllBoardsAsync(CancellationToken cancellationToken = default) =>
        RunCommandAsync(["board", "listall"], cancellationToken);

    public Task<string> SearchBoardsAsync(string boardname,CancellationToken cancellationToken = default) =>
    RunCommandAsync(["board", "search", boardname], cancellationToken);

    public Task<string> ListAllLibrariesAsync(CancellationToken cancellationToken = default) =>
        RunCommandAsync(["lib", "list"], cancellationToken);

    public Task<string> CompileAsync(string sketchPath, string fqbn, CancellationToken cancellationToken) =>
        RunCommandAsync(["compile", "--fqbn", fqbn, sketchPath], cancellationToken);

    public async Task<List<string>> ListBoardsAsync() =>
        (await RunCommandAsync(["board", "list"], CancellationToken.None))
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Skip(1).ToList();

    public async Task<List<string>> ListLibrariesAsync() =>
        (await ListAllLibrariesAsync())
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Skip(1).ToList();

    public Task<string> CompileAsync(string sketchPath, string fqbn) => CompileAsync(sketchPath, fqbn, CancellationToken.None);

    private async Task<string> RunCommandAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
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

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(stderr) ? $"arduino-cli je završio kodom {process.ExitCode}." : stderr.Trim());

        return string.IsNullOrWhiteSpace(stderr) ? stdout.Trim() : $"{stdout.Trim()}\n{stderr.Trim()}".Trim();
    }
}
