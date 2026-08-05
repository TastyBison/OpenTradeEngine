using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenTradeEngine;

public static class PlanetCatalog
{
    private static readonly Dictionary<string, string> Descriptions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Vexx"] = "the capital of Kukubia and the seat of the Imperial Magistrate.",
        ["Pyke"] = "a major industrial center and the home of L-Tech engines.",
        ["Mira"] = "the religious heart of Kukubia and sacred resting place of the Quaso Mutta.",
        ["Stye"] = "a financial center and home of the Traders' Union.",
        ["Loro"] = "a celebrated vacation world known throughout Kukubia.",
        ["Zile"] = "a wealthy merchant world and the home of Mr. Zinn.",
        ["Frac"] = "the headquarters of Voyager's Insurance Company.",
        ["Tilo"] = "the gambler's planet, crowded with casinos.",
        ["Queg"] = "Kukubia's infamous smuggler haven.",
        ["Xeen"] = "a giant junkyard filled with ship parts and ingenious mechanics.",
        ["Ooom"] = "the mysterious fortune-teller's planet.",
        ["Hork"] = "the media capital of Kukubia.",
        ["Bass"] = "a gathering place for stock-market analysts and brokers.",
        ["Nosh"] = "the fuel depot of the Kukubian system."
    };

    public static IReadOnlyList<PlanetDefinition> All
    {
        get
        {
            var planets = Descriptions.Select(pair => new PlanetDefinition(pair.Key, pair.Value)).ToList();
            foreach (var modPlanet in ModCatalog.Planets)
            {
                var existing = planets.FindIndex(planet =>
                    planet.Name.Equals(modPlanet.Name, StringComparison.OrdinalIgnoreCase));
                var definition = new PlanetDefinition(modPlanet.Name,
                    string.IsNullOrWhiteSpace(modPlanet.Description)
                        ? "one of the worlds of the Kukubian system."
                        : modPlanet.Description);
                if (existing >= 0) planets[existing] = definition;
                else planets.Add(definition);
            }
            return planets;
        }
    }

    public static string Describe(string planet)
    {
        var modDescription = ModCatalog.FindPlanet(planet)?.Description;
        return !string.IsNullOrWhiteSpace(modDescription)
            ? $"{planet.ToUpperInvariant()} is {modDescription}"
            : Descriptions.TryGetValue(planet, out var text)
        ? $"{planet.ToUpperInvariant()} is {text}"
        : $"{planet} is one of the worlds of the Kukubian system.";
    }
}

public sealed record PlanetDefinition(string Name, string Description);
