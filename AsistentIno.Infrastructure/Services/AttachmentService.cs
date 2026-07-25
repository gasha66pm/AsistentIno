using System.IO;
using AsistentIno.Models;

namespace AsistentIno.Services;

public static class AttachmentService
{
    private const long MaxImageBytes = 20 * 1024 * 1024; // 20 MB
    private const int MaxTextChars = 200_000;

    private static readonly Dictionary<string, string> ImageMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".bmp"] = "image/bmp",
    };

    public static MessageAttachment CreateFromFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var extension = Path.GetExtension(filePath);

        if (ImageMimeTypes.TryGetValue(extension, out var mimeType))
        {
            var info = new FileInfo(filePath);
            if (info.Length > MaxImageBytes)
                throw new InvalidOperationException($"Slika '{fileName}' je prevelika ({info.Length / 1024 / 1024} MB). Maksimum je {MaxImageBytes / 1024 / 1024} MB.");

            var bytes = File.ReadAllBytes(filePath);
            return new MessageAttachment
            {
                FileName = fileName,
                MimeType = mimeType,
                IsImage = true,
                Base64Data = Convert.ToBase64String(bytes)
            };
        }

        var text = File.ReadAllText(filePath);
        var truncated = false;
        if (text.Length > MaxTextChars)
        {
            text = text[..MaxTextChars];
            truncated = true;
        }

        if (truncated)
            text += "\n\n[... sadržaj skraćen zbog veličine ...]";

        return new MessageAttachment
        {
            FileName = fileName,
            MimeType = "text/plain",
            IsImage = false,
            TextContent = text
        };
    }
}
