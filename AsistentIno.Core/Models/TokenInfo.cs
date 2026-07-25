namespace AsistentIno.Models
{
    /// <summary>
    /// Format (provajder) iz kog je JSON odgovor originalno došao.
    /// Koristi se samo informativno / za debug, ne utiče na obračun.
    /// </summary>
    public enum LlmResponseFormat
    {
        Unknown,
        GoogleGeminiNative,     // "usage.total_input_tokens", "usage.total_output_tokens", ...
        OpenAiResponsesApi,     // top-level "object": "response"
        AnthropicMessagesApi,   // top-level "type": "message", "role": "assistant"
        OpenAiChatCompletion    // top-level "object": "chat.completion"
    }

    /// <summary>
    /// Jedinstven, normalizovan zapis o potrošnji tokena za JEDAN LLM poziv/odgovor,
    /// nezavisno od toga koji je provajder/format u pitanju.
    ///
    /// VAŽNO: TotalTokens se popunjava ISKLJUČIVO ako ga sam JSON sadrži kao eksplicitno
    /// polje (npr. "total_tokens"). Ako izvorni format nema takvo polje (npr. Anthropic
    /// Messages API), TotalTokens ostaje null — namerno se NE računa kao zbir Input+Output,
    /// jer je svrha ovog polja da služi kao nezavisna kontrola ispravnosti obračuna.
    /// </summary>
    public class TokenInfo
    {
        // --- Metapodaci ---
        public string? Id { get; set; }
        public string? Model { get; set; }
        public LlmResponseFormat Format { get; set; } = LlmResponseFormat.Unknown;
        public string? ServiceTier { get; set; }

        // --- Osnovni tokeni ---
        public long? InputTokens { get; set; }
        public long? OutputTokens { get; set; }

        /// <summary>
        /// Total tokens ONLY ako postoji direktno u JSON-u (kontrolna vrednost).
        /// Null ako izvor ne vraća takvo polje.
        /// </summary>
        public long? TotalTokens { get; set; }

        // --- Detaljniji / dodatni tokeni (nisu uvek prisutni) ---

        /// <summary>Tokeni pročitani iz keša (cache hit) - Gemini: total_cached_tokens,
        /// OpenAI: *_details.cached_tokens, Anthropic: cache_read_input_tokens.</summary>
        public long? CachedInputTokens { get; set; }

        /// <summary>Tokeni upisani u keš (samo Anthropic: cache_creation_input_tokens).</summary>
        public long? CacheCreationTokens { get; set; }

        /// <summary>"Thinking"/reasoning tokeni (Gemini: total_thought_tokens,
        /// OpenAI: output_tokens_details.reasoning_tokens, DeepSeek/OpenAI: reasoning_tokens).</summary>
        public long? ReasoningTokens { get; set; }

        /// <summary>Tokeni utrošeni na tool-use/function-calling (Gemini: total_tool_use_tokens).</summary>
        public long? ToolUseTokens { get; set; }

        /// <summary>Sirov "usage" JSON blok, za slučaj da zatreba nešto neuobičajeno / audit.</summary>
        public string? RawUsageJson { get; set; }

        public override string ToString()
        {
            return $"[{Format}] Model={Model}, Input={InputTokens}, Output={OutputTokens}, " +
                   $"Total(json)={(TotalTokens?.ToString() ?? "N/A")}, Cached={CachedInputTokens}, " +
                   $"CacheCreation={CacheCreationTokens}, Reasoning={ReasoningTokens}, ToolUse={ToolUseTokens}";
        }
    }
}
