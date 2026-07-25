using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using AsistentIno.Models;

namespace AsistentIno.Services;

    /// <summary>
    /// Parsira JSON odgovore raznih LLM provajdera i izvlači token podatke
    /// u jedinstven <see cref="TokenInfo"/> objekat.
    /// </summary>
    public static class TokenInfoProcessor
    {
        /// <summary>
        /// Parsira JSON string (bilo koji od podržanih formata) i vraća normalizovan TokenInfo.
        /// </summary>
        public static TokenInfo Parse(string json)
        {
        try
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("JSON sadržaj je prazan.", nameof(json));

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var format = DetectFormat(root);

            return format switch
            {
                LlmResponseFormat.GoogleGeminiNative => ParseGemini(root),
                LlmResponseFormat.OpenAiResponsesApi => ParseOpenAiResponses(root),
                LlmResponseFormat.AnthropicMessagesApi => ParseAnthropicMessages(root),
                LlmResponseFormat.OpenAiChatCompletion => ParseOpenAiChatCompletion(root),
                _ => throw new NotSupportedException(
                        "Nepoznat/nepodržan format JSON odgovora - nije moguće izvući token informacije.")
            };
        }
        catch
        {
            return new TokenInfo
            {
                Format = LlmResponseFormat.Unknown,
                RawUsageJson = json
            };
        }
        }

        /// <summary>Učitava fajl sa diska i parsira ga.</summary>
        public static TokenInfo ParseFile(string path)
        {
            var json = File.ReadAllText(path);
            return Parse(json);
        }

        /// <summary>Parsira više fajlova odjednom.</summary>
        public static List<TokenInfo> ParseFiles(IEnumerable<string> paths)
        {
            var result = new List<TokenInfo>();
            foreach (var path in paths)
                result.Add(ParseFile(path));
            return result;
        }

        // ------------------------------------------------------------------
        // Detekcija formata na osnovu karakterističnih polja
        // ------------------------------------------------------------------
        private static LlmResponseFormat DetectFormat(JsonElement root)
        {
            // OpenAI Responses API: top-level "object": "response"
            if (TryGetString(root, "object", out var obj))
            {
                if (obj == "response")
                    return LlmResponseFormat.OpenAiResponsesApi;

                if (obj == "chat.completion")
                    return LlmResponseFormat.OpenAiChatCompletion;
            }

            // Anthropic Messages API: top-level "type": "message" + "role": "assistant"
            if (TryGetString(root, "type", out var type) && type == "message"
                && TryGetString(root, "role", out var role) && role == "assistant")
            {
                return LlmResponseFormat.AnthropicMessagesApi;
            }

            // Google Gemini native: usage sadrži "total_input_tokens" i "total_output_tokens"
            if (root.TryGetProperty("usage", out var usage)
                && usage.TryGetProperty("total_input_tokens", out _)
                && usage.TryGetProperty("total_output_tokens", out _))
            {
                return LlmResponseFormat.GoogleGeminiNative;
            }

            return LlmResponseFormat.Unknown;
        }

        // ------------------------------------------------------------------
        // Google Gemini (native) format
        // ------------------------------------------------------------------
        private static TokenInfo ParseGemini(JsonElement root)
        {
            var usage = root.GetProperty("usage");

            return new TokenInfo
            {
                Id = GetStringOrNull(root, "id"),
                Model = GetStringOrNull(root, "model"),
                Format = LlmResponseFormat.GoogleGeminiNative,
                InputTokens = GetLongOrNull(usage, "total_input_tokens"),
                OutputTokens = GetLongOrNull(usage, "total_output_tokens"),
                TotalTokens = GetLongOrNull(usage, "total_tokens"),
                CachedInputTokens = GetLongOrNull(usage, "total_cached_tokens"),
                ReasoningTokens = GetLongOrNull(usage, "total_thought_tokens"),
                ToolUseTokens = GetLongOrNull(usage, "total_tool_use_tokens"),
                RawUsageJson = usage.GetRawText()
            };
        }

        // ------------------------------------------------------------------
        // OpenAI Responses API format ( /v1/responses )
        // ------------------------------------------------------------------
        private static TokenInfo ParseOpenAiResponses(JsonElement root)
        {
            var usage = root.GetProperty("usage");

            long? cached = null;
            long? reasoning = null;

            if (usage.TryGetProperty("input_tokens_details", out var inDetails))
                cached = GetLongOrNull(inDetails, "cached_tokens");

            if (usage.TryGetProperty("output_tokens_details", out var outDetails))
                reasoning = GetLongOrNull(outDetails, "reasoning_tokens");

            return new TokenInfo
            {
                Id = GetStringOrNull(root, "id"),
                Model = GetStringOrNull(root, "model"),
                Format = LlmResponseFormat.OpenAiResponsesApi,
                ServiceTier = GetStringOrNull(root, "service_tier"),
                InputTokens = GetLongOrNull(usage, "input_tokens"),
                OutputTokens = GetLongOrNull(usage, "output_tokens"),
                TotalTokens = GetLongOrNull(usage, "total_tokens"),
                CachedInputTokens = cached,
                ReasoningTokens = reasoning,
                RawUsageJson = usage.GetRawText()
            };
        }

        // ------------------------------------------------------------------
        // Anthropic Messages API format
        // ------------------------------------------------------------------
        private static TokenInfo ParseAnthropicMessages(JsonElement root)
        {
            var usage = root.GetProperty("usage");

            return new TokenInfo
            {
                Id = GetStringOrNull(root, "id"),
                Model = GetStringOrNull(root, "model"),
                Format = LlmResponseFormat.AnthropicMessagesApi,
                ServiceTier = GetStringOrNull(usage, "service_tier"),
                InputTokens = GetLongOrNull(usage, "input_tokens"),
                OutputTokens = GetLongOrNull(usage, "output_tokens"),
                // Anthropic ne vraća eksplicitno "total_tokens" polje -> namerno ostaje null.
                TotalTokens = GetLongOrNull(usage, "total_tokens"),
                CachedInputTokens = GetLongOrNull(usage, "cache_read_input_tokens"),
                CacheCreationTokens = GetLongOrNull(usage, "cache_creation_input_tokens"),
                RawUsageJson = usage.GetRawText()
            };
        }

        // ------------------------------------------------------------------
        // OpenAI Chat Completions format (legacy /v1/chat/completions,
        // koriste ga npr. DeepSeek preko OpenAI-kompatibilnog API-ja)
        // ------------------------------------------------------------------
        private static TokenInfo ParseOpenAiChatCompletion(JsonElement root)
        {
            var usage = root.GetProperty("usage");

            long? cached = null;
            long? reasoning = null;

            if (usage.TryGetProperty("prompt_tokens_details", out var promptDetails))
                cached = GetLongOrNull(promptDetails, "cached_tokens");

            if (usage.TryGetProperty("completion_tokens_details", out var completionDetails))
                reasoning = GetLongOrNull(completionDetails, "reasoning_tokens");

            return new TokenInfo
            {
                Id = GetStringOrNull(root, "id"),
                Model = GetStringOrNull(root, "model"),
                Format = LlmResponseFormat.OpenAiChatCompletion,
                InputTokens = GetLongOrNull(usage, "prompt_tokens"),
                OutputTokens = GetLongOrNull(usage, "completion_tokens"),
                TotalTokens = GetLongOrNull(usage, "total_tokens"),
                CachedInputTokens = cached,
                ReasoningTokens = reasoning,
                RawUsageJson = usage.GetRawText()
            };
        }

        // ------------------------------------------------------------------
        // Pomoćne (helper) metode za bezbedno čitanje polja
        // ------------------------------------------------------------------
        private static bool TryGetString(JsonElement el, string propertyName, out string? value)
        {
            value = null;
            if (el.ValueKind == JsonValueKind.Object
                && el.TryGetProperty(propertyName, out var prop)
                && prop.ValueKind == JsonValueKind.String)
            {
                value = prop.GetString();
                return true;
            }
            return false;
        }

        private static string? GetStringOrNull(JsonElement el, string propertyName)
        {
            if (el.ValueKind == JsonValueKind.Object
                && el.TryGetProperty(propertyName, out var prop)
                && prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString();
            }
            return null;
        }

        private static long? GetLongOrNull(JsonElement el, string propertyName)
        {
            if (el.ValueKind == JsonValueKind.Object
                && el.TryGetProperty(propertyName, out var prop)
                && prop.ValueKind == JsonValueKind.Number
                && prop.TryGetInt64(out var value))
            {
                return value;
            }
            return null;
        }
    }
