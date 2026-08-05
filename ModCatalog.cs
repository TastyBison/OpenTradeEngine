using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenTradeEngine;

public static class ModCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly List<LoadedMod> Loaded = [];
    private static readonly List<ModPlanetDefinition> LoadedPlanets = [];
    private static readonly List<ModEventDefinition> LoadedEvents = [];
    private static readonly List<string> LoadErrors = [];

    public static string ModsDirectory { get; private set; } = Path.Combine(AppContext.BaseDirectory, "Mods");
    public static bool Enabled { get; private set; } = true;
    public static IReadOnlyList<LoadedMod> Mods => Loaded;
    public static IReadOnlyList<ModPlanetDefinition> Planets => LoadedPlanets;
    public static IReadOnlyList<ModEventDefinition> Events => LoadedEvents;
    public static IReadOnlyList<string> Errors => LoadErrors;

    public static void SetEnabled(bool enabled, string? modsDirectory = null)
    {
        Enabled = enabled;
        Reload(modsDirectory);
    }

    public static void Reload(string? modsDirectory = null)
    {
        ModsDirectory = Path.GetFullPath(modsDirectory ?? Path.Combine(AppContext.BaseDirectory, "Mods"));
        Loaded.Clear();
        LoadedPlanets.Clear();
        LoadedEvents.Clear();
        LoadErrors.Clear();

        try
        {
            Directory.CreateDirectory(ModsDirectory);
        }
        catch (Exception exception)
        {
            LoadErrors.Add($"Could not create the Mods folder: {exception.Message}");
            return;
        }

        if (!Enabled) return;

        var candidates = new List<(string Directory, ModManifest Manifest)>();
        foreach (var directory in Directory.EnumerateDirectories(ModsDirectory)
                     .Where(path => !Path.GetFileName(path).StartsWith('.') &&
                                    !Path.GetFileName(path).StartsWith('_')))
        {
            try
            {
                var manifestPath = Path.Combine(directory, "mod.json");
                var manifest = File.Exists(manifestPath)
                    ? JsonSerializer.Deserialize<ModManifest>(File.ReadAllText(manifestPath), JsonOptions)
                    : new ModManifest { Name = Path.GetFileName(directory) };
                if (manifest is null || !manifest.Enabled) continue;
                manifest.Name = string.IsNullOrWhiteSpace(manifest.Name) ? Path.GetFileName(directory) : manifest.Name.Trim();
                candidates.Add((directory, manifest));
            }
            catch (Exception exception)
            {
                LoadErrors.Add($"{Path.GetFileName(directory)}: invalid mod.json ({exception.Message})");
            }
        }

        foreach (var candidate in candidates.OrderBy(item => item.Manifest.Priority)
                     .ThenBy(item => item.Manifest.Name, StringComparer.OrdinalIgnoreCase))
        {
            var planetCount = LoadFiles(candidate.Directory, "Planets", LoadedPlanets, candidate.Manifest.Name,
                ValidatePlanet);
            var eventCount = LoadFiles(candidate.Directory, "Events", LoadedEvents, candidate.Manifest.Name,
                ValidateEvent);
            Loaded.Add(new LoadedMod(candidate.Manifest.Name, candidate.Manifest.Version,
                candidate.Manifest.Author, candidate.Directory, planetCount, eventCount));
        }
    }

    public static ModPlanetDefinition? FindPlanet(string name) =>
        LoadedPlanets.LastOrDefault(planet => planet.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public static string? ResolvePlanetAsset(string planet, Func<ModPlanetDefinition, string?> selector)
    {
        var definition = FindPlanet(planet);
        return definition is null ? null : ResolveAsset(definition.SourceDirectory, selector(definition));
    }

    public static string? ResolveAsset(string rootDirectory, string? asset)
    {
        if (string.IsNullOrWhiteSpace(asset)) return null;
        try
        {
            var root = Path.GetFullPath(rootDirectory);
            var path = Path.GetFullPath(Path.Combine(root, asset));
            if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                !path.Equals(root, StringComparison.OrdinalIgnoreCase)) return null;
            return File.Exists(path) ? path : null;
        }
        catch
        {
            return null;
        }
    }

    private static int LoadFiles<T>(string modDirectory, string contentFolder, List<T> target,
        string modName, Func<T, string?> validate) where T : ModContentDefinition
    {
        var directory = Path.Combine(modDirectory, contentFolder);
        if (!Directory.Exists(directory)) return 0;
        var count = 0;
        foreach (var path in Directory.EnumerateFiles(directory, "*.json", SearchOption.AllDirectories)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var item = JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions);
                if (item is null) throw new InvalidDataException("The file contained no definition.");
                item.SourceDirectory = modDirectory;
                item.SourceMod = modName;
                var validationError = validate(item);
                if (validationError is not null) throw new InvalidDataException(validationError);
                target.Add(item);
                count++;
            }
            catch (Exception exception)
            {
                LoadErrors.Add($"{modName}/{contentFolder}/{Path.GetFileName(path)}: {exception.Message}");
            }
        }
        return count;
    }

    private static string? ValidatePlanet(ModPlanetDefinition planet)
    {
        if (string.IsNullOrWhiteSpace(planet.Name)) return "Planet name is required.";
        planet.Name = planet.Name.Trim();
        return null;
    }

    private static string? ValidateEvent(ModEventDefinition travelEvent)
    {
        if (string.IsNullOrWhiteSpace(travelEvent.Id)) return "Event id is required.";
        if (string.IsNullOrWhiteSpace(travelEvent.Heading)) return "Event heading is required.";
        if (string.IsNullOrWhiteSpace(travelEvent.Message)) return "Event message is required.";
        travelEvent.ChancePercent = Math.Clamp(travelEvent.ChancePercent, 0, 100);
        travelEvent.MinWeek = Math.Max(1, travelEvent.MinWeek);
        return null;
    }
}

public sealed class ModManifest
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public string Author { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public int Priority { get; set; }
}

public sealed record LoadedMod(string Name, string Version, string Author, string Directory,
    int PlanetCount, int EventCount);

public abstract class ModContentDefinition
{
    [JsonIgnore] public string SourceDirectory { get; set; } = string.Empty;
    [JsonIgnore] public string SourceMod { get; set; } = string.Empty;
}

public sealed class ModPlanetDefinition : ModContentDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? ArrivalImage { get; set; }
    public string? CityImage { get; set; }
}

public enum ModEventKind
{
    Either,
    Good,
    Bad
}

public sealed class ModEventDefinition : ModContentDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Heading { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public ModEventKind Kind { get; set; } = ModEventKind.Either;
    public int ChancePercent { get; set; } = 5;
    public int MinWeek { get; set; } = 2;
    public string? Planet { get; set; }
    public decimal CashChange { get; set; }
    public int LuckChange { get; set; }
    public string? Image { get; set; }
    public string? Audio { get; set; }
    public string Effect { get; set; } = string.Empty;
    public decimal FeePerShipTon { get; set; } = 5m;

    public bool IsEligible(GameSession session, CompanyState company, int week, bool goodChain)
    {
        if (week < MinWeek ||
            (!string.IsNullOrWhiteSpace(Planet) && !Planet.Equals(company.Planet, StringComparison.OrdinalIgnoreCase)) ||
            (Kind != ModEventKind.Either && (goodChain ? Kind != ModEventKind.Good : Kind != ModEventKind.Bad)))
            return false;

        if (!Effect.Equals("ChaosMonkSabotage", StringComparison.OrdinalIgnoreCase)) return true;
        var active = session.Companies.Where(candidate => !candidate.IsBankrupt).ToArray();
        if (active.Length <= 1) return false;
        var highestNetWorth = active.Max(session.NetWorthOf);
        return session.NetWorthOf(company) < highestNetWorth;
    }

    public TravelEventResult Apply(GameSession session, CompanyState company, int seed)
    {
        if (Effect.Equals("ChaosMonkSabotage", StringComparison.OrdinalIgnoreCase))
            return ApplyChaosMonkSabotage(session, company, seed);

        var cashChange = GameMath.WholeKubars(CashChange);
        if (cashChange < 0m) company.PayMandatoryExpense(-cashChange);
        else company.Cash += cashChange;
        if (LuckChange != 0)
            company.Luck = Math.Clamp(company.Luck + LuckChange, CompanyState.MinimumLuck, CompanyState.MaximumLuck);

        var message = Message.Replace("{company}", company.Name, StringComparison.OrdinalIgnoreCase)
            .Replace("{planet}", company.Planet, StringComparison.OrdinalIgnoreCase)
            .Replace("{cashChange}", cashChange.ToString("N0"), StringComparison.OrdinalIgnoreCase);
        var image = ModCatalog.ResolveAsset(SourceDirectory, Image) ?? string.Empty;
        var audio = ModCatalog.ResolveAsset(SourceDirectory, Audio) ?? string.Empty;
        var isGood = Kind != ModEventKind.Bad;
        return new TravelEventResult(Heading, message, isGood, image, audio);
    }

    private TravelEventResult ApplyChaosMonkSabotage(GameSession session, CompanyState company, int seed)
    {
        var fee = Math.Max(500m, GameMath.WholeKubars(company.ShipTons * Math.Max(0m, FeePerShipTon)));
        var image = ModCatalog.ResolveAsset(SourceDirectory, Image) ?? string.Empty;
        var opponents = session.Companies.Where(target => !target.IsBankrupt &&
            !ReferenceEquals(target, company) &&
            !target.Name.Equals(company.Name, StringComparison.OrdinalIgnoreCase)).ToArray();
        var aiAccepts = opponents.Length > 0 && company.Cash + company.Bank >= fee;

        return new TravelEventResult(Heading,
            Message.Replace("{company}", company.Name, StringComparison.OrdinalIgnoreCase)
                .Replace("{planet}", company.Planet, StringComparison.OrdinalIgnoreCase)
                .Replace("{fee}", fee.ToString("N0"), StringComparison.OrdinalIgnoreCase),
            true, image, "SOOTH.MP3",
            new TravelEventChoice("Hire Chaos Monk", "Decline", aiAccepts, accepted =>
            {
                if (!accepted)
                    return new TravelEventResult("Dark Offer Declined",
                        "You refuse to unleash the Chaos Monk's bad spirits.", true, image, "SOOTH.MP3",
                        SkipOutcomeScreen: true, SuppressAiEventNotice: true);

                company.PayMandatoryExpense(fee);
                var random = new Random(GameMath.StableHash(seed, company.Name, "Chaos Monk sabotage"));
                var outcomeRoll = random.Next(1, 13);
                if (outcomeRoll == 1)
                {
                    company.Luck = CompanyState.MinimumLuck;
                    return new TravelEventResult("The Curse Backfires!",
                        "The Chaos Monk loses control of the ritual. The curse turns upon your own company, " +
                        "leaving it under the darkest possible cloud of misfortune.", false, image, "BAD.MP3",
                        SuppressAiEventNotice: true);
                }

                CompanyState[] targets;
                var hitsEveryone = outcomeRoll is 2 or 3;
                if (hitsEveryone)
                {
                    targets = session.Companies.Where(target => !target.IsBankrupt).ToArray();
                }
                else
                {
                    targets = opponents.OrderBy(_ => random.Next()).Take(random.Next(1, opponents.Length + 1)).ToArray();
                }

                foreach (var target in targets)
                {
                    var reduction = hitsEveryone ? random.Next(15, 51) : random.Next(25, 51);
                    target.Luck -= reduction;
                    if (target.IsHuman)
                    {
                        target.PendingTurnNotices.Add(new TurnNotice(
                            "A Warning From Ooom",
                            "The Soothsayers of Ooom sense a disturbance around your company. Bad spirits have " +
                            "been sent your way, and a shadow of misfortune now follows your ship.",
                            "SOOTH_N.SWF", "SOOTH.MP3"));
                    }
                }

                return new TravelEventResult("Bad Spirits Dispatched",
                    $"The Chaos Monk completes the ritual. Bad spirits descend upon " +
                    $"{(hitsEveryone ? "every company—including yours" : targets.Length == 1 ? "one rival company" : $"{targets.Length} rival companies")}.",
                    true, image, "SOOTH.MP3", SuppressAiEventNotice: true);
            }), SuppressAiEventNotice: true);
    }
}
