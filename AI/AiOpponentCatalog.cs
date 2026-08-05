
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenTradeEngine;

public sealed record AiOpponentProfile(
    int Number,
    string Name,
    string Personality,
    decimal ReserveMultiplier,
    decimal InsuranceAppetite,
    decimal PassengerFocus,
    decimal CommodityFocus,
    decimal RiskTolerance);

public static class AiOpponentCatalog
{
    public static IReadOnlyList<AiOpponentProfile> All { get; } =
    [
        new(1, "Gizzy Shipping", "Freewheeling & Chaotic", 0.90m, 0.90m, 1.00m, 1.05m, 1.20m),
        new(2, "Trading Corp. IV", "By The Book & Neutral", 1.00m, 1.00m, 1.00m, 1.00m, 1.00m),
        new(3, "Vandergriff Ltd.", "Cautious Oldguard", 1.30m, 1.45m, 0.95m, 0.90m, 0.70m),
        new(4, "Puffer Inc.", "Naive Start-up", 0.85m, 0.80m, 1.30m, 0.85m, 1.10m),
        new(5, "Roke Transport", "Risk Taker & Innovative", 0.80m, 0.70m, 0.90m, 1.15m, 1.35m),
        new(6, "Hoff Meister", "Ruthless & Aggressive", 0.70m, 0.55m, 0.75m, 1.35m, 1.50m)
    ];

    public static IReadOnlyList<AiOpponentProfile> Select(int count, int seed)
    {
        var pool = All.ToList();
        var random = new Random(GameMath.StableHash(seed, "AI opponent selection"));
        for (var index = pool.Count - 1; index > 0; index--)
        {
            var swap = random.Next(index + 1);
            (pool[index], pool[swap]) = (pool[swap], pool[index]);
        }
        return pool.Take(Math.Clamp(count, 0, pool.Count)).ToArray();
    }

    public static AiOpponentProfile ForCompany(string name) =>
        All.FirstOrDefault(profile => profile.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) ??
        All[1];
}

