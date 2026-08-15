using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AsistentIno.Services
{

public class FileService
{
    private string _currentFolder = "";
    private readonly INotificationService _notificationService;

    public string CurrentFolder => _currentFolder;

    public FileService(INotificationService notificationService)
    {
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
    }

    public void SetCurrentFolder(string path)
    {
        if (Directory.Exists(path))
            _currentFolder = path;
    }


    public string ResolveWorkspacePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(_currentFolder))
            throw new InvalidOperationException("Workspace folder nije izabran.");

        var fullPath = Path.GetFullPath(Path.Combine(_currentFolder, relativePath ?? string.Empty));
        if (!IsPathSafe(fullPath))
            throw new UnauthorizedAccessException("Putanja izlazi iz workspace foldera.");
        return fullPath;
    }

    public List<string> ListWorkspaceEntries(string relativeFolder = "", bool recursive = false)
    {
        var folder = ResolveWorkspacePath(relativeFolder);
        if (!Directory.Exists(folder))
            return new List<string>();

        var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        return Directory.EnumerateFileSystemEntries(folder, "*", option)
            .Select(path =>
            {
                var relative = Path.GetRelativePath(_currentFolder, path).Replace('\\', '/');
                return Directory.Exists(path) ? relative + "/" : relative;
            })
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public List<string> GetCodeFiles()
    {
        if (string.IsNullOrEmpty(_currentFolder))
            return new List<string>();

        var extensions = new[] { ".cpp", ".h", ".ino", ".c", ".hpp", ".txt", ".md", ".svg", ".json" };
        var files = Directory.GetFiles(_currentFolder, "*.*", SearchOption.AllDirectories)
            .Where(f => extensions.Contains(Path.GetExtension(f).ToLower()))
            .ToList();

        return files;
    }

    public string ReadFile(string filePath)
    {
        try
        {
            if (!IsPathSafe(filePath))
                throw new UnauthorizedAccessException("Pristup fajlu je zabranjen");

            if (File.Exists(filePath))
                return File.ReadAllText(filePath);

            return "";
        }
        catch (Exception ex)
        {
            throw new Exception($"Greška pri čitanju fajla: {ex.Message}");
        }
    }

    public void WriteFile(string filePath, string content)
    {
        try
        {
            if (!IsPathSafe(filePath))
                throw new UnauthorizedAccessException("Pristup fajlu je zabranjen");

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(filePath, content);
        }
        catch (Exception ex)
        {
            throw new Exception($"Greška pri pisanju u fajl: {ex.Message}");
        }
    }

    public List<string> ListFiles(string folderPath = "")
    {
        try
        {
            var path = string.IsNullOrEmpty(folderPath) ? _currentFolder : folderPath;

            if (!IsPathSafe(path))
                throw new UnauthorizedAccessException("Pristup folderu je zabranjen");

            if (!Directory.Exists(path))
                return new List<string>();

            var files = Directory.GetFiles(path)
                .Select(f => Path.GetFileName(f))
                .ToList();

            var dirs = Directory.GetDirectories(path)
                .Select(d => Path.GetFileName(d) + "/")
                .ToList();

            return files.Concat(dirs).ToList();
        }
        catch (Exception ex)
        {
            throw new Exception($"Greška pri listanju fajlova: {ex.Message}");
        }
    }

    public void DeleteFile(string filePath)
    {
        try
        {
            if (!IsPathSafe(filePath))
                throw new UnauthorizedAccessException("Pristup fajlu je zabranjen");

            if (File.Exists(filePath))
                File.Delete(filePath);
        }
        catch (Exception ex)
        {
            throw new Exception($"Greška pri brisanju fajla: {ex.Message}");
        }
    }

    public bool FileExists(string filePath)
    {
        return File.Exists(filePath);
    }

    private bool IsPathSafe(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var currentFullPath = Path.GetFullPath(_currentFolder);

        // Proveri da li je path unutar trenutnog foldera
        if (string.IsNullOrEmpty(_currentFolder)) return false;
        var root = currentFullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return fullPath.Equals(currentFullPath, StringComparison.OrdinalIgnoreCase) || fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }
}
}
