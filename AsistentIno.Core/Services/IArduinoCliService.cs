namespace AsistentIno.Services;

public interface IArduinoCliService
{
    string ArduinoCliPath { get; }

    void SetArduinoCliPath(string? path);

    Task<string> GetVersionAsync(CancellationToken cancellationToken = default);

    Task<string> ListAllBoardsAsync(CancellationToken cancellationToken = default);

    Task<string> SearchBoardsAsync(string boardname, CancellationToken cancellationToken = default);

    Task<string> ListAllLibrariesAsync(CancellationToken cancellationToken = default);

    Task<string> CompileAsync(string sketchPath, string fqbn, CancellationToken cancellationToken = default);

    Task<List<string>> ListBoardsAsync();

    Task<List<string>> ListLibrariesAsync();

    Task<string> SearchLibrariesAsync(string libstring, CancellationToken cancellationToken = default);
    Task<string> ListInstalledLibrariesAsync(CancellationToken cancellationToken = default);
}
