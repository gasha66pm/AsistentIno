using System;
using System.Collections.Generic;
using System.Text;

namespace AsistentIno.Models;

/// <summary>
/// Cenovnik za jedan model. Sve cene su izražene po 1.000.000 (milion) tokena,
/// jer tako većina provajdera objavljuje cene (npr. "$3 / 1M input tokens").
/// Prilagodi vrednosti stvarnom cenovniku provajdera koji koristiš.
/// </summary>
public class ModelPricing
{
    public required string Model { get; init; }

    /// <summary>Cena za "obične" (nekeširane) input tokene, po 1M tokena.</summary>
    public decimal InputPricePerMillion { get; init; }

    /// <summary>Cena za output tokene, po 1M tokena.</summary>
    public decimal OutputPricePerMillion { get; init; }

    /// <summary>Cena za input tokene koji su pogodak u kešu (obično znatno jeftinije).
    /// Ako nije zadato, koristi se ista cena kao za obične input tokene.</summary>
    public decimal? CachedInputPricePerMillion { get; init; }

    /// <summary>Cena za upis u keš (Anthropic "cache_creation_input_tokens").
    /// Obično malo skuplje od običnog input tokena. Ako nije zadato, koristi se
    /// ista cena kao za obične input tokene.</summary>
    public decimal? CacheCreationPricePerMillion { get; init; }

    /// <summary>Da li se "reasoning"/"thought" tokeni naplaćuju po ceni output tokena
    /// (to je uobičajena praksa kod svih provajdera - reasoning tokeni su podskup
    /// output tokena, ne dodaju se odvojeno na total).</summary>
    public bool ReasoningBilledAsOutput { get; init; } = true;
}