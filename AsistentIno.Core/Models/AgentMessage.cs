using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AsistentIno.Models
{

public class AgentMessage
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("role")]
    public string Role { get; set; } = "user"; // user, assistant

    [JsonPropertyName("content")]
    public string Content { get; set; } = "";

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.Now;

    [JsonPropertyName("toolCalls")]
    public List<ToolCall> ToolCalls { get; set; } = new();

    [JsonPropertyName("inputTokens")]
    public long InputTokens { get; set; } = 0;

    [JsonPropertyName("outputTokens")]
    public long OutputTokens { get; set; } = 0;

    [JsonPropertyName("cacheTokens")]
    public long CacheTokens { get; set; } = 0;

    [JsonPropertyName("attachments")]
    public List<MessageAttachment> Attachments { get; set; } = new();
}

public class MessageAttachment
{
    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = "";

    [JsonPropertyName("mimeType")]
    public string MimeType { get; set; } = "";

    [JsonPropertyName("isImage")]
    public bool IsImage { get; set; }

    // Za slike: base64-enkodirani sadržaj fajla.
    [JsonPropertyName("base64Data")]
    public string? Base64Data { get; set; }

    // Za ne-slike (tekst/kod fajlovi): tekstualni sadržaj fajla.
    [JsonPropertyName("textContent")]
    public string? TextContent { get; set; }
}

public class ToolCall
{
    [JsonPropertyName("toolName")]
    public string ToolName { get; set; } = "";

    [JsonPropertyName("arguments")]
    public Dictionary<string, object> Arguments { get; set; } = new();

    [JsonPropertyName("result")]
    public string Result { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "pending"; // pending, success, error
}

public class FileOperation
{
    public string FilePath { get; set; } = "";
    public string Operation { get; set; } = ""; // create, modify, read, delete
    public string Content { get; set; } = "";
}
}
