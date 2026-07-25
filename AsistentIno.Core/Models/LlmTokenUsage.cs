namespace AsistentIno.Models;

/// <summary>
/// Kumulativna ("ukupna") potrošnja tokena za JEDAN LLM (svi pozivi zbirno).
/// Perzistira se u posebnom JSON fajlu po LLM-u (Usage\{LlmId}.json).
/// Polja odgovaraju dimenzijama iz <see cref="TokenInfo"/>, ali su ovde zbirne vrednosti.
/// </summary>
public class LlmTokenUsage
{
    public string LlmId { get; set; } = "";

    public long TotalInputTokens { get; set; }
    public long TotalOutputTokens { get; set; }
    public long TotalCachedInputTokens { get; set; }
    public long TotalCacheCreationTokens { get; set; }
    public long TotalReasoningTokens { get; set; }
    public long TotalToolUseTokens { get; set; }

    /// <summary>Zbir svih tokena obračunatih od strane TokenCostCalculator-a (kontrolna vrednost).</summary>
    public long TotalTokens { get; set; }

    public decimal TotalCost { get; set; }

    public int CallCount { get; set; }

    public void Add(TokenInfo info, CostBreakdown breakdown)
    {
        TotalInputTokens += info.InputTokens ?? 0;
        TotalOutputTokens += info.OutputTokens ?? 0;
        TotalCachedInputTokens += info.CachedInputTokens ?? 0;
        TotalCacheCreationTokens += info.CacheCreationTokens ?? 0;
        TotalReasoningTokens += info.ReasoningTokens ?? 0;
        TotalToolUseTokens += info.ToolUseTokens ?? 0;
        TotalTokens += breakdown.ComputedTotalTokens;
        TotalCost += breakdown.TotalCost;
        CallCount++;
    }

    public void Reset()
    {
        TotalInputTokens = 0;
        TotalOutputTokens = 0;
        TotalCachedInputTokens = 0;
        TotalCacheCreationTokens = 0;
        TotalReasoningTokens = 0;
        TotalToolUseTokens = 0;
        TotalTokens = 0;
        TotalCost = 0;
        CallCount = 0;
    }
}
