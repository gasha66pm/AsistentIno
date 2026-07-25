namespace AsistentIno.Models;

/// <summary>
/// Detaljan raspis troška za jedan TokenInfo zapis.
/// </summary>
public class CostBreakdown
{
    public required TokenInfo Source { get; init; }
    public required string Model { get; init; }

    public decimal InputCost { get; init; }
    public decimal CachedInputCost { get; init; }
    public decimal CacheCreationCost { get; init; }
    public decimal OutputCost { get; init; }

    /// <summary>
    /// Trošak reasoning/thought tokena SAMO kada ih provajder naplaćuje odvojeno
    /// od output_tokens (trenutno slučaj kod Google Gemini formata). Kod provajdera
    /// gde su reasoning tokeni već uključeni u output_tokens (OpenAI, Anthropic),
    /// ovo polje je 0 da se izbegne duplo naplaćivanje.
    /// </summary>
    public decimal AdditiveReasoningCost { get; init; }

    public decimal TotalCost => InputCost + CachedInputCost + CacheCreationCost + OutputCost + AdditiveReasoningCost;

    /// <summary>
    /// Zbir tokena koje smo mi koristili za obračun (input - keširani + keširani + creation + output).
    /// Služi za poređenje sa TokenInfo.TotalTokens (kontrolna vrednost iz JSON-a).
    /// </summary>
    public long ComputedTotalTokens { get; init; }

    /// <summary>
    /// Razlika između TokenInfo.TotalTokens (iz JSON-a) i ComputedTotalTokens.
    /// Null ako izvor nije vratio total_tokens (npr. Anthropic format) -> tada nema kontrole.
    /// 0 znači da se obračun tačno poklapa sa onim što je provajder prijavio.
    /// </summary>
    public long? DiscrepancyVsJsonTotal =>
        Source.TotalTokens.HasValue ? Source.TotalTokens.Value - ComputedTotalTokens : null;

    public bool HasDiscrepancy => DiscrepancyVsJsonTotal.HasValue && DiscrepancyVsJsonTotal.Value != 0;

    public override string ToString()
    {
        var ctrl = Source.TotalTokens.HasValue
            ? $"JSON total={Source.TotalTokens}, izračunato={ComputedTotalTokens}, razlika={DiscrepancyVsJsonTotal}"
            : "JSON total nije dostupan (nema kontrole)";

        return $"{Model}: input=${InputCost:F6}, cached=${CachedInputCost:F6}, " +
               $"cache_creation=${CacheCreationCost:F6}, output=${OutputCost:F6}, " +
               $"reasoning(dodatno)=${AdditiveReasoningCost:F6}, " +
               $"UKUPNO=${TotalCost:F6}  [{ctrl}]";
    }
}
