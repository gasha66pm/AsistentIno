using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AsistentIno.Models;

namespace AsistentIno.Services;

    /// <summary>
    /// Računa troškove na osnovu TokenInfo objekata i registrovanog cenovnika po modelu.
    /// </summary>
    public class TokenCostCalculator
    {
        private readonly Dictionary<string, ModelPricing> _pricingByModel;

        public TokenCostCalculator(IEnumerable<ModelPricing> pricingList)
        {
            _pricingByModel = pricingList.ToDictionary(p => p.Model, StringComparer.OrdinalIgnoreCase);
        }

        public void AddOrUpdatePricing(ModelPricing pricing) => _pricingByModel[pricing.Model] = pricing;

        /// <summary>
        /// Obračunava trošak za jedan TokenInfo. Baca izuzetak ako za dati model
        /// nije registrovan cenovnik (svesno - da se ne bi tiho vratio pogrešan/nulti trošak).
        /// </summary>
        public CostBreakdown Calculate(TokenInfo info)
        {
            if (string.IsNullOrWhiteSpace(info.Model) || !_pricingByModel.TryGetValue(info.Model, out var pricing))
            {
                throw new InvalidOperationException(
                    $"Nema registrovanog cenovnika za model '{info.Model ?? "(nepoznat)"}'. " +
                    $"Dodaj ga preko AddOrUpdatePricing pre obračuna.");
            }

            long cachedInput = info.CachedInputTokens ?? 0;
            long cacheCreation = info.CacheCreationTokens ?? 0;
            long totalInput = info.InputTokens ?? 0;

            // Semantika "InputTokens" se razlikuje po provajderu:
            // - OpenAI i Gemini: InputTokens je UKUPAN input, a keširani deo je NJEGOV PODSKUP
            //   (potvrđeno primerom: prompt_tokens=842, cached=768, prompt_cache_miss_tokens=74=842-768).
            //   Zato ga oduzimamo da ne bismo platili duplo.
            // - Anthropic: InputTokens (input_tokens) predstavlja SAMO sveže/nekeširane tokene;
            //   cache_read_input_tokens i cache_creation_input_tokens su DODATNI tokeni, ne podskup.
            //   Zato se ovde ništa ne oduzima.
            long nonCachedInput = info.Format == LlmResponseFormat.AnthropicMessagesApi
                ? totalInput
                : Math.Max(0, totalInput - cachedInput);

            decimal cachedRate = pricing.CachedInputPricePerMillion ?? pricing.InputPricePerMillion;
            decimal cacheCreationRate = pricing.CacheCreationPricePerMillion ?? pricing.InputPricePerMillion;

            decimal inputCost = nonCachedInput / 1_000_000m * pricing.InputPricePerMillion;
            decimal cachedCost = cachedInput / 1_000_000m * cachedRate;
            decimal creationCost = cacheCreation / 1_000_000m * cacheCreationRate;
            decimal outputCost = (info.OutputTokens ?? 0) / 1_000_000m * pricing.OutputPricePerMillion;

            // Kod Google Gemini formata, "thought/reasoning" tokeni se NE ubrajaju u
            // OutputTokens (total_output_tokens) i naplaćuju se odvojeno - potvrđeno
            // matematikom iz primera: total_input(434) + total_output(10) + total_thought(72) = total_tokens(516).
            // Kod OpenAI/Anthropic formata reasoning tokeni su već podskup OutputTokens,
            // pa se ovde ne dodaju ponovo (izbegava se dupla naplata).
            long additiveReasoningTokens = 0;
            decimal additiveReasoningCost = 0m;
            if (pricing.ReasoningBilledAsOutput && info.Format == LlmResponseFormat.GoogleGeminiNative)
            {
                additiveReasoningTokens = info.ReasoningTokens ?? 0;
                additiveReasoningCost = additiveReasoningTokens / 1_000_000m * pricing.OutputPricePerMillion;
            }

            long computedTotal = nonCachedInput + cachedInput + cacheCreation
                                  + (info.OutputTokens ?? 0) + additiveReasoningTokens;

            return new CostBreakdown
            {
                Source = info,
                Model = info.Model!,
                InputCost = inputCost,
                CachedInputCost = cachedCost,
                CacheCreationCost = creationCost,
                OutputCost = outputCost,
                AdditiveReasoningCost = additiveReasoningCost,
                ComputedTotalTokens = computedTotal
            };
        }

        /// <summary>Obračunava troškove za listu TokenInfo objekata i vraća zbirni izveštaj.</summary>
        public List<CostBreakdown> CalculateMany(IEnumerable<TokenInfo> infos) =>
            infos.Select(Calculate).ToList();

        /// <summary>Ukupan trošak za listu obračuna (npr. za ceo dan/mesec/korisnika).</summary>
        public static decimal SumTotal(IEnumerable<CostBreakdown> breakdowns) =>
            breakdowns.Sum(b => b.TotalCost);
    }
 