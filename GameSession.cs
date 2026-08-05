using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenTradeEngine;

public enum AiEventVisibility
{
    Default,
    Full,
    None
}

public sealed partial class GameSession
{
    private static readonly int[] OriginalBadEventSlots = [2, 3, 4, 5, 6, 7, 8, 10, 11, 12, 13, 14, 15, 16, 17, 18];
    private readonly Dictionary<string, ArrivalReport> _arrivalReports =
        new(StringComparer.OrdinalIgnoreCase);

    public const decimal StandardWinTarget = 5_000_000m;
    // Quaso, the original 46-entry good-event table (events 2..47), and
    // the original bad-event table plus its travel-delay follow-up (2..19).
    public const int DebugTravelEventCount = 64;
    public GameSession(int level, IReadOnlyList<string> planets, int seed)
    {
        Level = Math.Clamp(level, 1, 6);
        // Decompiled initializeGame: Tutorial and Novice both use economy
        // difficulty 0; Beginner through Master use 1 through 4.
        Difficulty = Math.Clamp(Level - 2, 0, 4);
        Planets = planets.ToArray();
        Seed = seed;
        var random = new Random(seed);

        foreach (var planet in Planets)
            Markets[planet] = PlanetMarket.Create(planet, random, Difficulty);
        RefreshWeather();
    }

    public int Level { get; }
    public int Difficulty { get; }
    public bool IsTutorial => Level == 1;
    public int TutorialStage { get; internal set; } = 1;
    public bool TutorialCompleted { get; internal set; }
    public int Seed { get; }
    public int Week { get; internal set; } = 1;
    public AiEventVisibility AiEventVisibility { get; set; } = AiEventVisibility.Default;
    public bool SabotageEventsUnlocked => Week > 10;
    public int BrowSabotageRollMaximum => Math.Clamp(Week, 10, 50);
    public int HapaJilloSabotageRollMaximum => Math.Clamp(Week, 20, 70);
    public bool AuctionsUnlocked => Week > 10;
    public bool FacilityAuctionsUnlocked => AuctionsUnlocked;
    public int ActiveHumanIndex { get; set; }
    public int ActiveTurnIndex { get; set; }
    public List<string> TurnOrder { get; } = [];
    private decimal _winTarget = StandardWinTarget;
    public decimal WinTarget { get => _winTarget; set => _winTarget = GameMath.WholeKubars(value); }
    public IReadOnlyList<string> Planets { get; }
    public List<CompanyState> Companies { get; } = [];
    public Dictionary<string, PlanetMarket> Markets { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, decimal> SharePrices { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, List<decimal>> SharePriceHistory { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> StockTrends { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> CrashedExchanges { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> ExchangeClosedThroughWeek { get; } = new(StringComparer.OrdinalIgnoreCase);
    public int WeeklyStockNewsCode { get; internal set; }
    public string WeeklyStockNewsPlanet { get; internal set; } = string.Empty;
    public decimal WeeklyStockNewsPoints { get; internal set; }
    public List<string> LastTurnNews { get; } = [];
    public HashSet<string> WeeklyTravelEventClaims { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, TravelEventResult> LastTravelEvents { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, List<TravelEventResult>> LastJourneyEvents { get; } = new(StringComparer.OrdinalIgnoreCase);
    public AuctionResultNotice? PendingAuctionResult { get; internal set; }
    public HashSet<string> AuctionResultAcknowledgedBy { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<FacilityHolding> Facilities { get; } = [];
    public AuctionOffer? CurrentAuction { get; internal set; }
    public int LastAuctionWeek { get; internal set; }
    public CompanyState? Winner => Companies.Where(company => !company.IsBankrupt)
        .OrderByDescending(NetWorthOf)
        .FirstOrDefault(company => NetWorthOf(company) >= WinTarget);
    public string WeatherPlanet { get; internal set; } = string.Empty;
    public int WeatherCode { get; internal set; }

    private static string WeeklyGoodEventKey(int eventSlot) => eventSlot switch
    {
        22 or 42 => "sabotage",
        // The emergency tax break and the ceremonial visit both use Emperor
        // Dred, so either also blocks his donation request for the week.
        5 or 26 => "emperor-dred",
        _ => $"good:{eventSlot}"
    };

    private static string WeeklyBadEventKey(int eventSlot) => eventSlot == 2
        ? "emperor-dred"
        : $"bad:{eventSlot}";

    private static string WeeklyModEventKey(ModEventDefinition definition) =>
        definition.Effect.Equals("ChaosMonkSabotage", StringComparison.OrdinalIgnoreCase)
            ? "sabotage"
            : $"mod:{definition.Id}";

    private bool IsWeeklyGoodEventAvailable(int eventSlot) =>
        // The Traders' Union can offer ships and warehouse space to multiple
        // companies. Every other event can be encountered only once per week.
        eventSlot is 2 or 3 || !WeeklyTravelEventClaims.Contains(WeeklyGoodEventKey(eventSlot));

    public static string PickStartingPlanet(
        IReadOnlyList<string> planets,
        IReadOnlyDictionary<string, int> occupancy,
        Random random)
    {
        if (planets.Count == 0) return string.Empty;

        // Keep shared starts possible, but strongly prefer spreading ships
        // across the selected planets. Once every planet is occupied, the
        // lighter penalty still favors the least crowded locations.
        var weights = planets.Select(planet => occupancy.GetValueOrDefault(planet) switch
        {
            <= 0 => 8,
            1 => 2,
            _ => 1
        }).ToArray();
        var roll = random.Next(weights.Sum());
        for (var index = 0; index < planets.Count; index++)
        {
            if (roll < weights[index]) return planets[index];
            roll -= weights[index];
        }
        return planets[^1];
    }

    public string WeatherForecast()
        => WeatherCatalog.Forecast(WeatherCode, WeatherPlanet, Seed, Week);

    public decimal KukubianYear => 139m + Math.Max(0, Week - 1) / 50m;

    public string KukubianDate => $"{KukubianYear:0.00} A.B.";

    private void RefreshWeather()
    {
        if (Planets.Count == 0) return;
        var random = new Random(GameMath.StableHash(Seed, Week.ToString(), "weather"));
        WeatherPlanet = Planets[random.Next(Planets.Count)];
        WeatherCode = random.Next(1, 71);
    }

    public void InitializeTurnOrder()
    {
        foreach (var company in Companies)
        {
            foreach (var planet in Planets)
                company.Warehouses.TryAdd(planet, []);
            if (company.NetWorthHistory.Count == 0)
                company.NetWorthHistory.Add(NetWorthOf(company));
        }
        if (TurnOrder.Count > 0) return;
        TurnOrder.AddRange(Companies.Where(company => !company.IsBankrupt).Select(company => company.Name));
        ActiveTurnIndex = Math.Clamp(ActiveTurnIndex, 0, Math.Max(0, TurnOrder.Count - 1));
        SyncActiveHumanIndex();
    }

    public CompanyState? CurrentTurnCompany
    {
        get
        {
            InitializeTurnOrder();
            if (ActiveTurnIndex < 0 || ActiveTurnIndex >= TurnOrder.Count) return null;
            return Companies.FirstOrDefault(company =>
                company.Name.Equals(TurnOrder[ActiveTurnIndex], StringComparison.OrdinalIgnoreCase));
        }
    }

    public void RecordTravelTime(CompanyState company)
    {
        // Passengers disembark as soon as this journey is recorded. Resetting
        // them only when the global week advanced left them aboard during a
        // legitimate second turn in the same week.
        company.Passengers = 0;
        company.PassengersPickedUp = false;
        company.TicketPrice = company.NextTicketPrice;
        // Advertising was purchased on the planet just departed. Its values
        // remain available to calculate demand at the destination, but the
        // menu lamp represents a campaign placed at the current stop and must
        // therefore switch off as soon as the ship arrives.
        company.AdvertisingLightOn = false;
        if (!string.IsNullOrWhiteSpace(company.LastPlanet) &&
            !company.LastPlanet.Equals(company.Planet, StringComparison.OrdinalIgnoreCase))
            company.RecordPlanetVisit(company.Planet);
        if (string.IsNullOrWhiteSpace(company.LastPlanet) ||
            company.LastPlanet.Equals(company.Planet, StringComparison.OrdinalIgnoreCase))
        {
            company.TravelTime = 0d;
            company.TravelDelay = 1;
            return;
        }
        var baseTime = TravelRules.TravelTime(company.LastPlanet, company.Planet, company, Planets);
        company.TravelTime = Math.Floor(baseTime * Math.Max(1, company.TravelDelay) *
                                        Math.Clamp(company.TravelTimeMultiplier, 0.1d, 10d));
        company.TravelDelay = 1;
        company.TravelTimeMultiplier = 1d;
    }

    /// <summary>
    /// Completes the current human slot, runs any AI companies that arrive
    /// before the next human, and starts a new week when every company has acted.
    /// Returns true when the weekly economy advanced.
    /// </summary>
    public bool AdvanceScheduledTurnsAfterHuman(CompanyState? expectedDepartingHuman = null)
    {
        InitializeTurnOrder();
        if (expectedDepartingHuman is not null &&
            !ReferenceEquals(CurrentTurnCompany, expectedDepartingHuman))
            return false;
        if (CurrentTurnCompany is { IsHuman: true } departingHuman)
            RecordArrivalReport(departingHuman, departingHuman.LastPlanet, []);
        ActiveTurnIndex++;
        var advancedWeek = false;
        while (Companies.Any(company => company.IsHuman && !company.IsBankrupt))
        {
            if (ActiveTurnIndex >= TurnOrder.Count)
            {
                AdvanceWeek();
                BuildArrivalOrder();
                advancedWeek = true;
            }

            var company = CurrentTurnCompany;
            if (company is null || company.IsBankrupt)
            {
                ActiveTurnIndex++;
                continue;
            }
            if (company.IsHuman)
            {
                SyncActiveHumanIndex();
                QueueArrivalBoastsFor(company);
                return advancedWeek;
            }

            RunAiTurn(company);
            ResolveJourneyEvents(company);
            RecordTravelTime(company);
            ActiveTurnIndex++;
        }
        return advancedWeek;
    }

    public void BuildArrivalOrder()
    {
        _arrivalReports.Clear();
        TurnOrder.Clear();
        TurnOrder.AddRange(Companies.Where(company => !company.IsBankrupt)
            .OrderBy(company => company.TravelTime)
            .ThenBy(company => Math.Abs((long)GameMath.StableHash(Seed, Week.ToString(), company.Name,
                "travel tie")))
            .Select(company => company.Name));
        ActiveTurnIndex = 0;
        SyncActiveHumanIndex();
    }

    private void RecordArrivalReport(CompanyState company, string planet,
        IReadOnlyList<ArrivalPurchase> purchases)
    {
        if (string.IsNullOrWhiteSpace(planet)) return;
        var featuredPurchase = purchases
            .OrderByDescending(purchase => purchase.Quantity)
            .ThenByDescending(purchase => purchase.CommodityIndex)
            .Cast<ArrivalPurchase?>()
            .FirstOrDefault();
        _arrivalReports[company.Name] = new ArrivalReport(planet, featuredPurchase);
    }

    private void QueueArrivalBoastsFor(CompanyState recipient)
    {
        if (ActiveTurnIndex <= 0) return;
        foreach (var earlierName in TurnOrder.Take(ActiveTurnIndex))
        {
            var speaker = Companies.FirstOrDefault(company =>
                company.Name.Equals(earlierName, StringComparison.OrdinalIgnoreCase));
            if (speaker is null || speaker.IsBankrupt || ReferenceEquals(speaker, recipient) ||
                !_arrivalReports.TryGetValue(speaker.Name, out var report) ||
                !report.Planet.Equals(recipient.Planet, StringComparison.OrdinalIgnoreCase)) continue;
            recipient.PendingTurnNotices.Add(ArrivalDialogueCatalog.Create(
                this, speaker, recipient, report.Planet, report.Purchase));
        }
    }

    private void SyncActiveHumanIndex()
    {
        var current = ActiveTurnIndex >= 0 && ActiveTurnIndex < TurnOrder.Count ? TurnOrder[ActiveTurnIndex] : null;
        var humans = Companies.Where(company => company.IsHuman).ToArray();
        var index = Array.FindIndex(humans, company =>
            current is not null && company.Name.Equals(current, StringComparison.OrdinalIgnoreCase));
        if (index >= 0) ActiveHumanIndex = index;
    }

    public void ResolveTravelEvents()
    {
        LastTravelEvents.Clear();
        LastJourneyEvents.Clear();
        foreach (var company in Companies) ResolveJourneyEvents(company);
    }

    public TravelEventResult ResolveTravelEvent(CompanyState company)
    {
        var results = ResolveJourneyEvents(company);
        return results.FirstOrDefault() ?? QuietJourney(company);
    }

    internal PrimaryTravelEventState BeginPrimaryTravelEvents(CompanyState company) =>
        new(GameMath.StableHash(Seed, Week.ToString(), company.Name, company.Planet));

    internal TravelEventResult? ResolveNextPrimaryTravelEvent(
        CompanyState company, PrimaryTravelEventState state)
    {
        if (state.Finished) return null;
        if (company.IsBankrupt)
        {
            state.Finished = true;
            return new TravelEventResult("Bankrupt Company", $"{company.Name} can no longer travel.", false);
        }

        // Decompiled frm_Travel2_exit: ordinary player events are not activated
        // during the opening three global turns. Week one covers that opening
        // circuit in OpenTradeEngine's scheduler.
        if (Week == 1)
        {
            state.Finished = true;
            return QuietJourney(company);
        }

        // frm_Travel2_playerEvents checks Quaso first. If Quaso appears, OK
        // re-enters Travel2 and proceeds into the ordinary event chain.
        if (!state.QuasoChecked)
        {
            state.QuasoChecked = true;
            var suggestedPlanet = Planets.Count == 0
                ? company.Planet
                : Planets[state.Random.Next(Planets.Count)];
            if (!WeeklyTravelEventClaims.Contains("quaso") && Planets.Count > 1 &&
                state.Random.Next(1, 41) == 1 &&
                !suggestedPlanet.Equals(company.Planet, StringComparison.OrdinalIgnoreCase))
            {
                WeeklyTravelEventClaims.Add("quaso");
                return TravelEncounterCatalog.QuasoRedirect(company, suggestedPlanet);
            }
        }

        if (!state.ChainChosen)
        {
            state.ChainChosen = true;
            var goodChance = Math.Clamp(
                company.Luck, CompanyState.MinimumLuck, CompanyState.MaximumLuck);
            var weatherHazard = company.Planet.Equals(WeatherPlanet, StringComparison.OrdinalIgnoreCase) &&
                                (WeatherCode <= 10 || WeatherCode is >= 61 and <= 70);
            if (weatherHazard)
                goodChance = Math.Max(CompanyState.MinimumLuck, goodChance - 25);
            var chainRoll = state.Random.Next(1, 101);
            state.GoodChain = chainRoll <= goodChance;
            GameplayLogger.Log("EVENT ROLL", company.Name,
                $"chainRoll={chainRoll}; goodChance={goodChance}; goodChain={state.GoodChain}; " +
                $"weatherHazard={weatherHazard}; planet={company.Planet}");
        }

        if (state.GoodChain)
        {
            while (state.GoodEventSlot <= 47)
            {
                var eventSlot = state.GoodEventSlot++;
                // The first four original good checks use 1-in-10 odds. The
                // remaining forty-two use 1-in-40 odds. A successful but
                // ineligible slot is skipped and checking continues.
                if (!IsWeeklyGoodEventAvailable(eventSlot)) continue;
                var odds = eventSlot <= 5 ? 10 : 40;
                var eventRoll = state.Random.Next(1, odds + 1);
                GameplayLogger.Log("EVENT ROLL", company.Name,
                    $"goodSlot={eventSlot}; roll={eventRoll}; required=1; odds=1/{odds}");
                if (eventRoll != 1) continue;
                var result = ResolveGoodTravelEvent(company, state.Random, eventSlot);
                if (!result.Heading.Equals("A Quiet Journey", StringComparison.OrdinalIgnoreCase))
                {
                    if (eventSlot is not (2 or 3))
                        WeeklyTravelEventClaims.Add(WeeklyGoodEventKey(eventSlot));
                    return result;
                }
            }
        }
        else
        {
            while (state.BadEventIndex < OriginalBadEventSlots.Length)
            {
                var eventSlot = OriginalBadEventSlots[state.BadEventIndex++];
                // Original bad event -4 (our positive slot 4) is the common
                // 1-in-8 insurance event; the other ordinary checks are 1-in-35.
                var eventKey = WeeklyBadEventKey(eventSlot);
                if (WeeklyTravelEventClaims.Contains(eventKey)) continue;
                var odds = eventSlot == 4 ? 8 : 35;
                var eventRoll = state.Random.Next(1, odds + 1);
                GameplayLogger.Log("EVENT ROLL", company.Name,
                    $"badSlot={eventSlot}; roll={eventRoll}; required=1; odds=1/{odds}");
                if (eventRoll != 1) continue;
                var result = ResolveBadTravelEvent(company, state.Random, weatherHazard: false, eventSlot);
                if (!result.Heading.Equals("A Quiet Journey", StringComparison.OrdinalIgnoreCase))
                {
                    WeeklyTravelEventClaims.Add(eventKey);
                    return result;
                }
            }
        }

        while (state.ModEventIndex < ModCatalog.Events.Count)
        {
            var definition = ModCatalog.Events[state.ModEventIndex++];
            var eventKey = WeeklyModEventKey(definition);
            if (WeeklyTravelEventClaims.Contains(eventKey) ||
                !definition.IsEligible(this, company, Week, state.GoodChain)) continue;
            var modRoll = state.Random.Next(1, 101);
            GameplayLogger.Log("EVENT ROLL", company.Name,
                $"mod={definition.Id}; roll={modRoll}; chance={definition.ChancePercent}");
            if (modRoll > definition.ChancePercent) continue;
            var result = definition.Apply(this, company, state.Random.Next());
            WeeklyTravelEventClaims.Add(eventKey);
            return result;
        }

        // The original adjusts eventGood only after the selected chain has
        // exhausted its remaining checks, not once for every displayed card.
        UpdateEventOdds(company, new TravelEventResult(string.Empty, string.Empty, state.GoodChain));
        state.Finished = true;
        return null;
    }

    public JourneyEventSequence BeginJourneyEvents(CompanyState company) => new(this, company);

    public IReadOnlyList<TravelEventResult> ResolveJourneyEvents(CompanyState company)
    {
        var sequence = BeginJourneyEvents(company);
        while (sequence.Next() is { } result)
        {
            var acceptedAiOffer = false;
            var presentedEvent = result;
            var offerHeading = result.Heading;
            if (result.Choice is not null)
            {
                acceptedAiOffer = !company.IsHuman && result.Choice.AiAccepts;
                result = result.Choice.Resolve(result.Choice.AiAccepts);
            }
            sequence.Complete(result);
            if (company.IsHuman) continue;
            if (result.SuppressAiEventNotice) continue;
            if (AiEventVisibility == AiEventVisibility.Full)
                QueueFullAiEventNotice(company, presentedEvent, result);
            else if (AiEventVisibility == AiEventVisibility.Default && acceptedAiOffer)
                QueueAcceptedAiOfferNotice(company, offerHeading, result);
        }
        return LastJourneyEvents.GetValueOrDefault(company.Name) ?? [];
    }

    internal TravelEventResult? ResolveCrewStrike(CompanyState company)
    {
        var threshold = company.CrewCount * company.CrewSalary * 5m;
        if (company.CrewWagesOwed < threshold) return null;
        var random = new Random(GameMath.StableHash(Seed, Week.ToString(), company.Name, "crew strike"));
        if (random.Next(1, 4) != 1) return null;

        var backWages = company.CrewWagesOwed;
        company.PayMandatoryExpense(backWages);
        company.CrewWagesOwed = 0m;
        company.CrewSalary += 500m;
        var result = new TravelEventResult("Union Boss",
            $"Unwilling to be treated as slave labor, your crew goes on strike! To settle the strike, " +
            $"{company.Name} must pay {backWages:N0} kubars in back wages and raise the weekly salary " +
            "by 500 kubars per crew member. Voyager's Insurance does not cover crew wages.",
            false, "CREW_N.SWF", "BAD3.MP3");
        UpdateEventOdds(company, result);
        return result;
    }

    internal TravelEventResult? ResolveFuelFailure(CompanyState company, out bool travelWasDelayed)
    {
        travelWasDelayed = false;
        if (string.IsNullOrWhiteSpace(company.PendingTravelNotice)) return null;
        var notice = company.PendingTravelNotice;
        company.PendingTravelNotice = string.Empty;
        travelWasDelayed = notice.Contains("were added to the Traders' Union loan", StringComparison.OrdinalIgnoreCase);
        var result = new TravelEventResult("Out of Fuel", notice, false,
            $"SHIP{Math.Clamp(company.ShipNumber, 1, 6)}.SWF", "BAD3.MP3");
        UpdateEventOdds(company, result);
        return result;
    }

    internal TravelEventResult FuelDelayResult(CompanyState company)
    {
        company.TravelDelay++;
        return new TravelEventResult("Travel Delayed",
            "To make matters worse, it will take you considerably longer than expected to reach your destination planet. " +
            "Voyager's Insurance does not cover emergency fuel service.",
            false, $"SHIP{Math.Clamp(company.ShipNumber, 1, 6)}.SWF", "BAD4.MP3");
    }

    internal TravelEventResult? ResolveTaxAudit(CompanyState company)
    {
        var backTaxes = company.TaxesOwed + company.TariffsOwed;
        if (!company.IsTaxAuditRisk) return null;
        var random = new Random(GameMath.StableHash(Seed, Week.ToString(), company.Name, "tax audit"));
        if (random.Next(1, 4) != 1) return null;

        var fine = backTaxes * 2m;
        company.PayMandatoryExpense(backTaxes + fine);
        company.TaxesOwed = 0m;
        company.TariffsOwed = 0m;
        company.TaxUnpaidWeeks = 0;
        var result = new TravelEventResult("Tax Audit",
            $"The Imperial Revenue Service audits {company.Name}. You must pay {backTaxes:N0} kubars in back taxes " +
            $"plus a fine of {fine:N0} kubars for not paying on time. Voyager's Insurance does not cover this.",
            false, "TAX1_N.SWF", "TAX.MP3");
        UpdateEventOdds(company, result);
        return result;
    }

    internal void RecordJourneyEvent(CompanyState company, TravelEventResult result)
    {
        if (!LastJourneyEvents.TryGetValue(company.Name, out var events))
            LastJourneyEvents[company.Name] = events = [];
        events.Add(result);
        LastTravelEvents[company.Name] = result;
        if (!company.IsHuman && !result.Heading.Equals("A Quiet Journey", StringComparison.OrdinalIgnoreCase))
            LastTurnNews.Add($"{company.Name}: {result.Heading} — {result.Message}");
    }

    internal void FinalizeJourneyEvents(CompanyState company)
    {
        company.InsuranceLevel = 0;
        company.InsuranceCost = GenerateInsuranceQuote(company);
        if (!company.IsHuman && company.WouldExceedAnyCreditLimit)
        {
            // Human companies receive a start-of-turn opportunity to rescue
            // their credit after an event chain. AI companies need to exercise
            // that same opportunity automatically before weekly interest can
            // turn an otherwise payable event bill into bankruptcy.
            if (Markets.TryGetValue(company.Planet, out var market))
                TryRescueAiCredit(company, market);
        }
    }

    public TravelEventResult ResolveDebugTravelEvent(CompanyState company, int eventIndex)
    {
        eventIndex = Math.Clamp(eventIndex, 1, DebugTravelEventCount);
        var random = new Random(GameMath.StableHash(Seed, "event debugger", eventIndex.ToString()));
        if (eventIndex == 1)
        {
            var destination = Planets.FirstOrDefault(planet =>
                !planet.Equals(company.Planet, StringComparison.OrdinalIgnoreCase)) ?? company.Planet;
            return TravelEncounterCatalog.QuasoRedirect(company, destination);
        }
        if (eventIndex <= 47) return ResolveGoodTravelEvent(company, random, eventIndex);
        var badKind = eventIndex - 46;
        if (badKind >= 9) badKind++; // The invented half-Zinn-loan event was removed.
        return ResolveBadTravelEvent(company, random, false, badKind);
    }

    private TravelEventResult ResolveGoodTravelEvent(CompanyState company, Random random, int? forcedEvent = null)
    {
        switch (forcedEvent ?? random.Next(2, 48))
        {
            case 2:
                return TravelEncounterCatalog.NewShipOffer(company, random.Next(25_000, 100_001));
            case 3:
                return TravelEncounterCatalog.WarehouseExpansionOffer(company, random.Next(15_000, 50_001));
            case 4 when company.CrewWagesOwed > 0m:
            {
                var forgiven = company.CrewWagesOwed;
                company.CrewWagesOwed = 0m;
                return new TravelEventResult("No Need To Pay!",
                    $"The crew forgives {forgiven:N0} kubars in unpaid wages.", true, "CREW_N.SWF", "GOOD2.MP3");
            }
            case 5 when company.TaxesOwed + company.TariffsOwed > 0m:
            {
                var forgiven = company.TaxesOwed + company.TariffsOwed;
                company.TaxesOwed = 0m;
                company.TariffsOwed = 0m;
                company.TaxUnpaidWeeks = 0;
                return new TravelEventResult("Emergency Tax Break",
                    $"Emperor Dred Nicolson forgives {forgiven:N0} kubars in taxes and tariffs.", true, "DRED.SWF", "DRED.MP3");
            }
            case 6:
            {
                var oldSpeed = company.EngineSpeed;
                company.BaseEngineSpeed++;
                return new TravelEventResult("Super Deal",
                    $"L-Tech installs a free engine upgrade, raising speed from {oldSpeed} to {company.EngineSpeed} kuarp.", true,
                    "MECH_N.SWF", "MECH.MP3");
            }
            case 7 when company.InsurancePriceRange >= 6:
            {
                var oldRange = company.InsurancePriceRange;
                company.InsurancePriceRange -= 5;
                var reduction = decimal.Floor((1m - company.InsurancePriceRange / (decimal)oldRange) * 100m);
                return new TravelEventResult("Lower Insurance Premiums",
                    $"Voyagers' Insurance lowers {company.Name}'s premium range by {reduction:N0}%.", true,
                    "INSURE_N.SWF", "GOOD5.MP3");
            }
            case 8 when company.ZinnRate >= 2m:
            {
                var oldRate = company.ZinnRate--;
                return new TravelEventResult("Lower Interest Rate",
                    $"Mr. Zinn lowers the weekly rate from {oldRate:0.#}% to {company.ZinnRate:0.#}%.", true,
                    "ZINN_N.SWF", "ZINN.MP3");
            }
            case 9 when company.StandardLoanRate >= 4m ||
                             company.StandardLoanRate >= 2m &&
                             company.Cash + company.Bank - company.Loan - company.ZinnLoan > 5_000_000m:
            {
                var oldRate = company.StandardLoanRate--;
                return new TravelEventResult("Lower Interest Rate",
                    $"The Traders' Union lowers the weekly loan rate from {oldRate:0.#}% to {company.StandardLoanRate:0.#}%.", true,
                    "LOAN_N.SWF", "GOOD5.MP3");
            }
            case 10 when company.SavingsRate <= 2m:
            {
                var oldRate = company.SavingsRate++;
                return new TravelEventResult("Higher Savings Rate",
                    $"The Traders' Union raises the weekly savings rate from {oldRate:0.#}% to {company.SavingsRate:0.#}%.", true,
                    "BANK1_N.SWF", "GOOD5.MP3");
            }
            case 11:
                return TravelEncounterCatalog.ZinnLoanExtension(company);
            case 12 when company.ZinnLoan > 0m:
            {
                var oldLimit = company.ZinnCreditLimit;
                company.ZinnCreditLimit += 50_000m;
                return new TravelEventResult("Higher Credit Limit",
                    $"Mr. Zinn raises the credit limit from {oldLimit:N0} to {company.ZinnCreditLimit:N0} kubars.", true,
                    "ZINN_N.SWF", "ZINN.MP3");
            }
            case 13 when company.Loan > 0m:
            {
                var oldLimit = company.StandardCreditLimit;
                company.StandardCreditLimit += 50_000m;
                return new TravelEventResult("Higher Credit Limit",
                    $"The Traders' Union raises the credit limit from {oldLimit:N0} to {company.StandardCreditLimit:N0} kubars.", true,
                    "LOAN_N.SWF", "GOOD3.MP3");
            }
            case 14:
            {
                // Original inheritance/lottery awards are 25..75 times ship mass.
                var reward = random.Next(25, 76) * company.ShipTons;
                company.Cash += reward;
                return new TravelEventResult("Money, Money, Money!",
                    $"A rich relative leaves {company.Name} {reward:N0} kubars.", true,
                    "MONEY_N.SWF", "GOOD2.MP3");
            }
            case 15:
            {
                var reward = random.Next(25, 76) * company.ShipTons;
                company.Cash += reward;
                return new TravelEventResult("You're In The Money!",
                    $"{company.Name} wins the lottery and receives {reward:N0} kubars.", true,
                    "MONEY_N.SWF", "GOOD2.MP3");
            }
            case 16:
            {
                var oldSpace = company.WarehouseCapacity;
                company.WarehouseCapacity += 25;
                company.InsurancePriceRange += 5;
                return new TravelEventResult("Free Warehouse Space",
                    $"The Traders' Union increases warehouse space from {oldSpace} to {company.WarehouseCapacity} tons on every planet free of charge.", true,
                    "WAREHOUS.SWF", "GOOD3.MP3");
            }
            case 17 when company.Cargo.Count > 0:
                return TravelEncounterCatalog.ScooterJayOffer(company, random.Next(1, 6) == 1);
            case 18 when company.Cargo.Count > 0:
                return TravelEncounterCatalog.HandsCargoOffer(company, random.Next(1, 6) == 1);
            case 19:
                return TravelEncounterCatalog.CurtonianLoan(company, random.Next(15, 36), random.Next(1, 5) == 1);
            case 20:
                return TravelEncounterCatalog.QuistInvestment(company, random.Next(25, 76));
            case 21:
                return TravelEncounterCatalog.WobblerSponsorship(company, random.Next(15, 36), random.Next(1, 4) != 1);
            case 22 when forcedEvent is not null || SabotageEventsUnlocked:
                return TravelEncounterCatalog.BrowSabotage(company, Companies,
                    random.Next(10, BrowSabotageRollMaximum + 1),
                    random.Next(1, 6) == 1, random.Next());
            case 23:
                return TravelEncounterCatalog.YoyoCoinFlip(company, random.Next(1, 36));
            case 24 when company.Cargo.Count > 0:
                return TravelEncounterCatalog.LimpusCharity(company);
            case 25 when company.BaseEngineSpeed >= 5:
                return TravelEncounterCatalog.SlegEngineTrade(company);
            case 26:
                return TravelEncounterCatalog.RoyalVisitor(company);
            case 27:
                return TravelEncounterCatalog.IsoGift(company, random.Next(25, 76));
            case 28:
            {
                var owned = company.Shares.Where(pair => pair.Value > 0 && SharePrices.ContainsKey(pair.Key))
                    .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).ToArray();
                if (owned.Length == 0) break;
                var exchange = owned[random.Next(owned.Length)].Key;
                return TravelEncounterCatalog.RaffetyShareOffer(company, exchange, SharePrices[exchange]);
            }
            case 29 when Planets.Count > 0:
            {
                var exchanges = Planets.Where(SharePrices.ContainsKey).ToArray();
                if (exchanges.Length == 0) break;
                var exchange = exchanges[random.Next(exchanges.Length)];
                var offerPrice = Math.Max(1m, GameMath.WholeKubars(SharePrices[exchange] * 0.8m));
                // The original event is eligible only when liquid funds are
                // strictly greater than the cost of two discounted shares.
                if (company.Cash + company.Bank <= offerPrice * 2m) break;
                return TravelEncounterCatalog.NebbitShareOffer(company, exchange, SharePrices[exchange]);
            }
            case 30 when company.Passengers < company.PassengerCapacity:
                return TravelEncounterCatalog.TatilusPassengerOffer(company);
            case 31 when company.Fuel > 1m:
                return TravelEncounterCatalog.GurttleFuelOffer(company);
            case 32 when company.Cargo.Count > 0:
                return TravelEncounterCatalog.Lord104CargoOffer(company);
            case 33 when company.CargoFree > 0 && company.Cash >= 100m:
                return TravelEncounterCatalog.ExoticForSale(company);
            case 34 when company.Cargo.Count > 0:
            {
                var alternatives = Enumerable.Range(0, CommodityCatalog.All.Length)
                    .Where(index => !company.Cargo.ContainsKey(index)).ToArray();
                if (alternatives.Length == 0) break;
                return TravelEncounterCatalog.SquowkCargoSwap(company,
                    alternatives[random.Next(alternatives.Length)]);
            }
            case 35 when company.Cargo.Count > 0:
                return TravelEncounterCatalog.CaptainLeahyOffer(company);
            case 36:
                return TravelEncounterCatalog.FreeAdvice(random.Next(TravelEncounterCatalog.AdviceCount));
            case 37:
            {
                // Original Travel2 event 37 uses a fixed per-company 1..100 roll
                // and the current week's global 1..100 newsData roll. The same
                // company roll also decides which of Teeter's four upgrades appears.
                var companyRandom = (int)(Math.Abs((long)GameMath.StableHash(Seed, company.Name,
                    "original company random")) % 100L) + 1;
                var weeklyNewsData = (int)(Math.Abs((long)GameMath.StableHash(Seed, Week.ToString(),
                    "original newsData")) % 100L) + 1;
                return TravelEncounterCatalog.TeeterOffer(company, companyRandom, weeklyNewsData);
            }
            case 38 when company.CrewCount > 2:
                return TravelEncounterCatalog.MeegOffer(company, random.Next(5, 26) * 1_000m,
                    random.Next(1, 3) == 2);
            case 39:
                return TravelEncounterCatalog.SpikeAdoption(company, random.Next(15, 51));
            case 40 when company.CrewSalary >= 1_500m:
                return TravelEncounterCatalog.NibbleOffer(company, random.Next(15, 51), random.Next(1, 6) != 1);
            case 41:
                return TravelEncounterCatalog.SpeevakOffer(company, random.Next(25, 126), random.Next(1, 5) == 1);
            case 42 when forcedEvent is not null || SabotageEventsUnlocked:
                return TravelEncounterCatalog.HapaJilloSabotage(company, Companies,
                    random.Next(20, HapaJilloSabotageRollMaximum + 1),
                    random.Next(1, 6) == 1, random.Next());
            case 43:
                return TravelEncounterCatalog.PilotShortcut(company);
            case 44:
                return TravelEncounterCatalog.SnozTransport(company, PickTravelRedirect(company, random),
                    random.Next(25, 126));
            case 45:
                return TravelEncounterCatalog.ShimmerTransport(company, PickTravelRedirect(company, random),
                    random.Next(100, 151), random.Next(1, 5) == 1);
            case 46:
                return TravelEncounterCatalog.TealTransport(company, PickTravelRedirect(company, random),
                    random.Next(25, 126));
            case 47:
                return TravelEncounterCatalog.StubbsWaterOffer(company, PickTravelRedirect(company, random),
                    random.Next(25, 76), random.Next(1, 5) == 1);
            default:
                break;
        }
        return QuietJourney(company);
    }

    private static TravelEventResult QuietJourney(CompanyState company) =>
        new("A Quiet Journey", $"{company.Name} reaches {company.Planet} without incident.", true);

    private string PickTravelRedirect(CompanyState company, Random random)
    {
        var choices = Planets.Where(planet =>
            !planet.Equals(company.Planet, StringComparison.OrdinalIgnoreCase) &&
            !planet.Equals(company.LastPlanet, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (choices.Length == 0)
            choices = Planets.Where(planet =>
                !planet.Equals(company.Planet, StringComparison.OrdinalIgnoreCase)).ToArray();
        return choices.Length == 0 ? company.Planet : choices[random.Next(choices.Length)];
    }

    private static void UpdateEventOdds(CompanyState company, TravelEventResult result)
    {
        if (result.LuckOverride is int luckOverride)
        {
            company.Luck = luckOverride;
            company.LastTravelEventGood = result.IsGood;
            return;
        }
        var wasGood = result.IsGood;
        // The original alternates around a 50% center: repeating the same kind
        // of outcome moves its probability by five points, while a switch resets it.
        if (wasGood)
        {
            company.Luck = company.LastTravelEventGood
                ? Math.Min(CompanyState.MaximumLuck, company.Luck + 5)
                : 50;
        }
        else
        {
            company.Luck = company.LastTravelEventGood
                ? 50
                : Math.Max(CompanyState.MinimumLuck, company.Luck - 5);
        }
        company.LastTravelEventGood = wasGood;
    }

    private TravelEventResult ResolveBadTravelEvent(
        CompanyState company, Random random, bool weatherHazard, int? forcedKind = null)
    {
        // The original Travel2 table is numbered 2..19. Event 19 is the
        // travel-delay follow-up card, but is exposed independently in the
        // debugger so its presentation can be checked too.
        var kind = forcedKind ?? random.Next(2, 18);
        // Slot 9 was an invented Zinn repayment demand, not an original
        // travel event. Keep the original 2..18 table while skipping that gap.
        if (forcedKind is null && kind >= 9) kind++;
        if (kind == 2)
        {
            // Decompiled frm_Travel2_badEvent uses f_rnd(15, 50) * shipTons.
            var donation = random.Next(15, 51) * company.ShipTons;
            var loanWarning = donation > company.Cash + company.Bank
                ? "\n\nYou don't have enough to cover the cost, so the Traders' Union steps in and loans you the difference."
                : string.Empty;
            return new TravelEventResult("Donation Request",
                "Supreme Commander Dred Nicolson, Emperor of the New Realm, pays a visit to your company. " +
                "He praises your hard work and loyal support of the New Realm.\n\n" +
                $"At the end of his speech, he asks you to make a small donation of {donation:N0} kubars " +
                $"to the Imperial Trust Fund.\n\nDo you donate the money?{loanWarning}", false,
                "DRED.SWF", "DRED.MP3", new TravelEventChoice("Yes", "No", true, accepted =>
                {
                    if (accepted)
                    {
                        company.PayMandatoryExpense(donation);
                        return new TravelEventResult("Donation Made",
                            $"You donate {donation:N0} kubars to the Imperial Trust Fund.", false,
                            "DRED.SWF", "DRED.MP3", SkipOutcomeScreen: true);
                    }
                    company.TaxesOwed += donation;
                    return new TravelEventResult("Special Tax",
                        $"Without explanation, the Government adds a one-time fee of {donation:N0} kubars to your taxes.",
                        false, "TAX1_N.SWF", "TAX.MP3");
                }));
        }

        if (kind == 3)
        {
            var warehouse = company.Warehouses.FirstOrDefault(pair => pair.Value.Values.Any(lot => lot.Quantity > 0));
            if (string.IsNullOrWhiteSpace(warehouse.Key))
                return new TravelEventResult("Warehouse Fire",
                    "A fire breaks out in one of your warehouses. Luckily, no goods were stored there.", false,
                    "FIRE.SWF", "FIRE.MP3");
            var cargo = warehouse.Value.First(pair => pair.Value.Quantity > 0);
            var lost = Math.Min(cargo.Value.Quantity, random.Next(1, 6));
            var insured = company.InsuranceCoverage > 0m;
            if (!insured)
            {
                cargo.Value.Quantity -= lost;
                if (cargo.Value.Quantity == 0) warehouse.Value.Remove(cargo.Key);
            }
            company.RecordWarehouseFire(warehouse.Key,
                insured ? 0m : lost * cargo.Value.AverageCost, Week, insured);
            return new TravelEventResult("Warehouse Fire",
                $"A fire breaks out in your warehouse on {warehouse.Key}. You lose {lost} tons of " +
                $"{CommodityCatalog.All[cargo.Key].Name}." + ReplacementText(insured), false,
                "FIRE.SWF", "FIRE.MP3");
        }

        if (kind == 4)
        {
            var oldRange = Math.Max(1, company.InsurancePriceRange);
            company.InsurancePriceRange += 5;
            var increase = decimal.Floor((company.InsurancePriceRange / (decimal)oldRange - 1m) * 100m);
            return new TravelEventResult("Insurance Rate Increase",
                $"Voyagers' Insurance raises {company.Name}'s premium range by {increase:N0}%.", false,
                "INSURE_N.SWF", "BAD5.MP3");
        }

        if (kind == 5)
        {
            var oldRate = company.ZinnRate++;
            return new TravelEventResult("Rate Increase",
                $"Out of pure greed, Mr. Zinn raises your weekly interest rate from {oldRate:0.#}% to " +
                $"{company.ZinnRate:0.#}%. Insurance does not cover this problem.", false,
                "ZINN_N.SWF", "ZINN.MP3");
        }

        if (kind == 6)
        {
            var oldRate = company.StandardLoanRate++;
            return new TravelEventResult("Rate Increase",
                $"The Traders' Union labels your company a high credit risk and raises the weekly loan rate " +
                $"from {oldRate:0.#}% to {company.StandardLoanRate:0.#}%. Insurance does not cover it.", false,
                "LOAN_N.SWF", "BAD5.MP3");
        }

        if (kind == 7)
        {
            var award = random.Next(25, 76) * company.ShipTons;
            var insured = company.InsuranceCoverage > 0m;
            if (!insured) company.PayMandatoryExpense(award);
            return new TravelEventResult("Lawsuit",
                $"An injured crew member successfully sues your company for {award:N0} kubars." +
                InsuranceText(insured ? award : 0m), false, "CREW_N.SWF", "BAD6.MP3");
        }

        if (kind == 8)
        {
            return new TravelEventResult("Demanding Union",
                "The United Space Workers' Union demands a 100-kubar increase in each employee's weekly salary. " +
                "Refusing may cause the crew to strike.\n\nDo you agree?", false,
                "CREW_N.SWF", "BAD6.MP3", new TravelEventChoice("Agree", "Refuse", true, accepted =>
                {
                    if (accepted)
                    {
                        company.CrewSalary += 100m;
                        return new TravelEventResult("Wages Raised",
                            $"Weekly salary rises to {company.CrewSalary:N0} kubars per crew member.", false,
                            "CREW_N.SWF", "BAD6.MP3");
                    }
                    if (random.Next(1, 4) == 1)
                    {
                        company.TravelDelay++;
                        company.CrewSalary += 100m;
                        return new TravelEventResult("Crew Strike",
                            "The crew goes on strike. The journey is delayed and the wage increase is imposed.", false,
                            "CREW_N.SWF", "BAD6.MP3");
                    }
                    return new TravelEventResult("Demand Refused",
                        "The crew reluctantly returns to work without a raise.", false,
                        "CREW_N.SWF", "BAD6.MP3");
                }));
        }

        if (kind == 10)
        {
            var damage = random.Next(25, 76) * company.ShipTons;
            var insured = company.InsuranceCoverage > 0m;
            if (!insured) company.PayMandatoryExpense(damage);
            return new TravelEventResult("Rebels' Demand",
                $"The Chichi Bobo Rebels seize ship parts worth {damage:N0} kubars as a revolutionary tax." +
                InsuranceText(insured ? damage : 0m), false, "REBELS.SWF", "REBELS.MP3");
        }

        if (kind == 11)
        {
            var cost = random.Next(25, 76) * company.ShipTons;
            var insured = company.InsuranceCoverage > 0m;
            if (!insured) company.PayMandatoryExpense(cost);
            return new TravelEventResult("Close Escape",
                $"A meteor storm pummels your ship. Hull repairs cost {cost:N0} kubars." +
                InsuranceText(insured ? cost : 0m), false, "METEOR.SWF", "METEOR.MP3");
        }

        if (kind == 12 && company.Cargo.Count > 0)
        {
            // Decompiled bad event 12 scans the 3x6 commodity table in order
            // and retains the last occupied slot. It then discards half of
            // that commodity, with .5 rounded upward. It is not a 1..5-ton
            // loss as the earlier recreation assumed.
            var cargo = company.Cargo
                .Where(pair => pair.Value.Quantity > 0)
                .OrderBy(pair => pair.Key)
                .Last();
            var lost = (int)decimal.Ceiling(cargo.Value.Quantity / 2m);
            var insured = company.InsuranceCoverage > 0m;
            if (!insured)
            {
                cargo.Value.Quantity -= lost;
                if (cargo.Value.Quantity == 0) company.Cargo.Remove(cargo.Key);
            }
            var quality = (cargo.Key / 6) switch
            {
                0 => "Rotten",
                1 => "Defective",
                _ => "Poor Quality"
            };
            return new TravelEventResult($"{quality} {CommodityCatalog.All[cargo.Key].Name}",
                $"One of your crew members discovers that you purchased {quality.ToLowerInvariant()} goods. " +
                $"You have no choice but to throw out {lost:N0} tons of " +
                $"{CommodityCatalog.All[cargo.Key].Name}." + ReplacementText(insured), false,
                $"SHIP{Math.Clamp(company.ShipNumber, 1, 12)}.SWF", "BAD3.MP3");
        }

        if (kind == 13)
        {
            var cost = random.Next(25, 76) * company.ShipTons;
            var insured = company.InsuranceCoverage > 0m;
            if (!insured) company.PayMandatoryExpense(cost);
            return new TravelEventResult("Your Ship Breaks Down!",
                $"A repair-droid must come out to fix your ship for {cost:N0} kubars." +
                InsuranceText(insured ? cost : 0m), false, "REPAIR.SWF", "REPAIR.MP3");
        }

        if (kind == 14)
        {
            var remainingFuel = decimal.Floor(company.Fuel / 2m);
            var lost = company.Fuel - remainingFuel;
            var insured = company.InsuranceCoverage > 0m;
            if (!insured) company.Fuel = remainingFuel;
            return new TravelEventResult("Fuel Tank Trouble",
                $"Lumbor stops a leak, but {lost:0.#} tons—half your fuel—has drained away." +
                ReplacementText(insured), false, "LUMBOR.SWF", "LEAK.MP3");
        }

        if (kind == 15)
        {
            var cost = random.Next(25, 76) * company.ShipTons;
            var insured = company.InsuranceCoverage > 0m;
            if (!insured) company.PayMandatoryExpense(cost);
            return new TravelEventResult("Asteroid Strikes",
                $"An asteroid hits your ship. Repairs cost {cost:N0} kubars." + InsuranceText(insured ? cost : 0m),
                false, "METEOR.SWF", "METEOR.MP3");
        }

        if (kind == 16)
        {
            var tons = company.Cargo.Values.Sum(cargo => cargo.Quantity);
            var insured = company.InsuranceCoverage > 0m;
            if (!insured) company.Cargo.Clear();
            return new TravelEventResult("Cargo Hold Overheats",
                $"Faulty wiring destroys all {tons} tons of cargo." + ReplacementText(insured), false,
                $"SHIP{Math.Clamp(company.ShipNumber, 1, 12)}.SWF", "BAD5.MP3");
        }

        if (kind == 17)
        {
            var original = company.Planet;
            company.Planet = PickTravelRedirect(company, random);
            return new TravelEventResult("Wrong Destination",
                $"A navigation error sends your ship to {company.Planet} instead of {original}. Insurance does not cover pilot error.",
                false, "PILOT.SWF", "PILOT.MP3");
        }

        if (kind == 18)
        {
            company.TravelDelay++;
            return new TravelEventResult("Lost Time",
                "Because of a serious miscalculation by your pilot, you get lost and lose several hours of valuable time.",
                false, "PILOT.SWF", "PILOT.MP3");
        }

        company.TravelDelay += company.TravelDelay > 1 ? 2 : 1;
        return new TravelEventResult("Travel Delayed",
            company.TravelDelay > 2
                ? "Because of all the trouble you had, it will take much, much longer than expected to reach your destination."
                : "Because of the mishap, it will take considerably longer than expected to reach your destination.",
            false, $"SHIP{Math.Clamp(company.ShipNumber, 1, 12)}.SWF", "BAD4.MP3");
    }

    private static string InsuranceText(decimal reimbursement) => reimbursement > 0
        ? $" Insurance reimburses {reimbursement:N0} kubars."
        : string.Empty;

    private static string ReplacementText(bool insured) => insured
        ? " Voyagers' Insurance replaces the loss in full."
        : string.Empty;

    public decimal GenerateInsuranceQuote(CompanyState company)
    {
        var random = new Random(GameMath.StableHash(Seed, Week.ToString(), company.Name, "insurance"));
        return random.Next(company.InsurancePriceRange, company.InsurancePriceRange * 1_000 + 1);
    }

    public void InitializeStocks()
    {
        for (var index = 0; index < Planets.Count; index++)
        {
            var planet = Planets[index];
            if (!SharePrices.ContainsKey(planet))
                SharePrices[planet] = Math.Max(100m, 1_700m - index * 100m);
            if (!StockTrends.ContainsKey(planet))
                StockTrends[planet] = 50;
            if (!SharePriceHistory.TryGetValue(planet, out var history))
                SharePriceHistory[planet] = Enumerable.Repeat(SharePrices[planet], 16).ToList();
            else if (history.Count == 0)
                history.AddRange(Enumerable.Repeat(SharePrices[planet], 16));
        }
    }

    private void RecordStockHistory()
    {
        foreach (var planet in Planets)
        {
            if (!SharePrices.TryGetValue(planet, out var price)) continue;
            if (!SharePriceHistory.TryGetValue(planet, out var history))
                SharePriceHistory[planet] = history = [];
            history.Add(price);
            while (history.Count > 16) history.RemoveAt(0);
        }
    }

    private void AdvanceStockMarket(Random random)
    {
        InitializeStocks();
        foreach (var planet in Planets)
        {
            if (CrashedExchanges.Remove(planet))
            {
                SharePrices[planet] = 1_000m;
                StockTrends[planet] = 50;
                ExchangeClosedThroughWeek.Remove(planet);
                continue;
            }

            var price = SharePrices[planet];
            // Ordinary momentum settles between 15 and 85, but original
            // market-news events may temporarily force this value to 0 or
            // 100. Preserve those extremes for the following weekly roll.
            var trend = Math.Clamp(StockTrends.GetValueOrDefault(planet, 50), 0, 100);
            int change;
            if (price < 250m)
            {
                // Original frm_Travel3_stockMarket crash trigger: once an
                // exchange falls below 250, its trend is forced to 1 and its
                // weekly movement becomes 50..75. It therefore has only a
                // 1-in-95 chance to climb instead of continuing toward zero.
                change = random.Next(50, 76);
                trend = 1;
            }
            else
            {
                change = random.Next(Math.Max(1, (int)decimal.Floor(price * 0.01m)),
                    Math.Max(2, (int)decimal.Floor(price * 0.10m) + 1));
            }

            if (random.Next(1, 96) <= trend)
            {
                price += change;
                trend = trend > 50 ? Math.Min(85, trend + 5) : 51;
            }
            else
            {
                price = Math.Max(0m, price - change);
                trend = trend <= 50 ? Math.Max(15, trend - 5) : 50;
            }

            SharePrices[planet] = GameMath.WholeKubars(price);
            StockTrends[planet] = trend;
            if (price > 0m) continue;

            CrashedExchanges.Add(planet);
            ExchangeClosedThroughWeek[planet] = Week;
            foreach (var company in Companies)
            {
                company.Shares.Remove(planet);
                company.ShareAverageCosts.Remove(planet);
            }
            var crashMessage = $"This is an emergency update!  The stock market on planet {planet} crashed today!!!  " +
                               $"Everyone who owned shares in the {planet} Exchange lost their entire investment.\n\n" +
                               $"Supreme Commander Dred Nicolson has intervened and shut down the {planet} Exchange for one week.  " +
                               "Investors are protesting, but there is no way to recover the lost money.";
            LastTurnNews.Add($"Emergency Broadcast: {crashMessage}");
            QueueStockNotice("Emergency Broadcast", crashMessage, planet);
        }
        foreach (var planet in Planets)
        {
            if (CrashedExchanges.Contains(planet)) continue;
            var current = SharePrices[planet];
            if (Week <= 1 || current >= 250m) continue;

            // frm_PlayerTurn2 cycles through eleven falling-market reports using
            // (turn + planet offset) % 11 whenever that exchange remains below
            // the original 250-kubar crash-warning threshold.
            var planetOffset = Planets.TakeWhile(candidate =>
                !candidate.Equals(planet, StringComparison.OrdinalIgnoreCase)).Count();
            var report = Math.Abs(Week + planetOffset) % StockFallReports.Length;
            QueueStockNotice("Financial Update!", StockFallReports[report](planet), planet);
        }
        RecordStockHistory();
    }

    private static readonly Func<string, string>[] StockFallReports =
    [
        planet => $"Financial analysts across Kukubia are warning that the {planet} Exchange is headed for a crash.",
        planet => $"It is rumored that Mr. Zinn is selling off a substantial portion of his investments in the {planet} Exchange.  Experts predict this rumor may push that Exchange down further.",
        planet => $"Corporate investors throughout the galaxy have been unloading their shares of the {planet} Exchange.",
        planet => $"Small investors are flooding the {planet} Exchange with sell orders.",
        planet => $"Everyone seems to feel a stock market meltdown on planet {planet} is imminent.",
        planet => $"We have reports that the majority of {planet} Exchange shareholders have lost all confidence in the market.",
        planet => $"The {planet} Exchange Board of Trustees has called for calm and requested that investors hold onto their shares.\n\nHowever, this has not stemmed the tide of sell orders.",
        planet => $"The {planet} Exchange is in a state of utter confusion as investors pack the floor, scrambling to sell their shares.\n\nSome investors have been hiring spaceships at exorbitant prices just to make it there in time to sell off their investments.",
        planet => $"A broad-based loss of confidence in the {planet} Exchange has lead to a major crash.\n\nIts Board of Trustees is begging investors to hold onto their shares, but no one seems to be willing to take that chance.",
        planet => $"A panic selling spree has brought the {planet} Exchange to its knees.  It's only a matter of time before it completely crashes.",
        planet => $"It appears that the {planet} Exchange is in a free-fall.\n\nAnalysts across Kukubia are advising their clients to sell their shares before it's too late."
    ];

    private void QueueStockNotice(string heading, string message, string planet)
    {
        // The original frm_PlayerTurn2 card uses NEWS_L and STOKCRSH for both
        // falling-market updates and the final emergency crash broadcast.
        var notice = new TurnNotice(heading, message, "NEWS_L.SWF", "STOKCRSH.MP3");
        foreach (var human in Companies.Where(company => company.IsHuman && !company.IsBankrupt &&
                     company.Planet.Equals(planet, StringComparison.OrdinalIgnoreCase)))
            human.PendingTurnNotices.Add(notice);
    }

    public decimal NetWorthOf(CompanyState company) => company.NetWorth + company.Shares.Sum(holding =>
        SharePrices.TryGetValue(holding.Key, out var price) ? holding.Value * price : 0m);

    public bool IsExchangeOpen(string planet) =>
        !ExchangeClosedThroughWeek.TryGetValue(planet, out var throughWeek) || throughWeek < Week;

    public string StockRecommendation(string exchange)
    {
        var trend = Math.Clamp(StockTrends.GetValueOrDefault(exchange, 50), 0, 100);
        return trend switch
        {
            <= 20 => "very strong sell",
            <= 30 => "strong sell",
            <= 40 => "sell",
            <= 60 => "hold",
            <= 70 => "buy",
            <= 80 => "strong buy",
            _ => "very strong buy"
        };
    }



    /// <summary>
    /// Resolves Vexx's Imperial Magistrate special. The original draws 1..30:
    /// 7/22 affect passenger tax, 8/23 import tariff, 9/24 export tariff, and
    /// 10/25 may forgive the petitioner's accrued tax and tariff balance.
    /// All other rolls are flavour-only audiences. Rate changes are system-wide.
    /// </summary>
    public TradeResult ResolveVexxPetition(CompanyState petitioner, int roll)
    {
        using var action = GameplayLogger.BeginCompanyAction(petitioner, "VEXX PETITION", $"roll={roll}");
        roll = Math.Clamp(roll, 1, 30);
        switch (roll)
        {
            case 7:
            case 22:
            {
                var oldRate = petitioner.PassengerTaxRate;
                var newRate = oldRate >= 2 ? oldRate - 1 : oldRate + 1;
                foreach (var company in Companies) company.PassengerTaxRate = newRate;
                var message = newRate < oldRate
                    ? $"The Imperial Magistrate lowers the passenger tax from {oldRate}% to {newRate}%."
                    : $"The petition backfires. The Imperial Magistrate raises the passenger tax from {oldRate}% to {newRate}%.";
                return TradeResult.Success(message, newRate < oldRate
                    ? OutcomeHighlight.Positive($"lowers the passenger tax from {oldRate}% to {newRate}%")
                    : OutcomeHighlight.Negative($"raises the passenger tax from {oldRate}% to {newRate}%"));
            }
            case 8:
            case 23:
            {
                var oldRate = petitioner.ImportTariffRate;
                var newRate = oldRate >= 2 ? oldRate - 1 : oldRate + 1;
                foreach (var company in Companies) company.ImportTariffRate = newRate;
                var message = newRate < oldRate
                    ? $"The Imperial Magistrate lowers import tariffs from {oldRate}% to {newRate}%."
                    : $"The petition backfires. The Imperial Magistrate raises import tariffs from {oldRate}% to {newRate}%.";
                return TradeResult.Success(message, newRate < oldRate
                    ? OutcomeHighlight.Positive($"lowers import tariffs from {oldRate}% to {newRate}%")
                    : OutcomeHighlight.Negative($"raises import tariffs from {oldRate}% to {newRate}%"));
            }
            case 9:
            case 24:
            {
                var oldRate = petitioner.ExportTariffRate;
                var newRate = oldRate >= 2 ? oldRate - 1 : oldRate + 1;
                foreach (var company in Companies) company.ExportTariffRate = newRate;
                var message = newRate < oldRate
                    ? $"The Imperial Magistrate lowers export tariffs from {oldRate}% to {newRate}%."
                    : $"The petition backfires. The Imperial Magistrate raises export tariffs from {oldRate}% to {newRate}%.";
                return TradeResult.Success(message, newRate < oldRate
                    ? OutcomeHighlight.Positive($"lowers export tariffs from {oldRate}% to {newRate}%")
                    : OutcomeHighlight.Negative($"raises export tariffs from {oldRate}% to {newRate}%"));
            }
            case 10 when petitioner.TaxesOwed + petitioner.TariffsOwed >= 2m:
            case 25 when petitioner.TaxesOwed + petitioner.TariffsOwed >= 2m:
            {
                var forgiven = petitioner.TaxesOwed + petitioner.TariffsOwed;
                petitioner.TaxesOwed = 0m;
                petitioner.TariffsOwed = 0m;
                var message = $"The Imperial Magistrate grants an emergency tax break of {forgiven:N0} kubars. You now owe nothing in taxes or tariffs.";
                return TradeResult.Success(message,
                    OutcomeHighlight.Positive($"tax break of {forgiven:N0} kubars"),
                    OutcomeHighlight.Positive("owe nothing"));
            }
            default:
                return TradeResult.Success(VexxAudienceText(roll));
        }
    }

    private static string VexxAudienceText(int roll) => roll switch
    {
        1 => "The Imperial Magistrate apologizes, but says his hands are tied and taxes cannot be changed now.",
        2 => "The Imperial Magistrate does not even bother to respond to your petition.",
        3 => "The Magistrate promises to raise the issue with the Supreme Commander on his next visit.",
        4 => "The Magistrate says he is working on the problem and hopes to make headway soon.",
        5 => "The Magistrate says he is too busy to cater to every special-interest group.",
        6 => "The Magistrate's assistant informs you that he is away on vacation.",
        >= 11 and <= 15 => "The Magistrate's secretary says he is in a meeting and asks you to try again next time.",
        16 => "The Magistrate criticizes you for asking to lower taxes and questions your loyalty to the government.",
        17 => "The Magistrate says he is too busy for petty requests, but would discuss a donation to the Imperial Treasury.",
        18 => "The Magistrate may raise the tax issue with Emperor Dred Nicolson next week if he is in a good mood.",
        19 => "The Magistrate spends two hours complaining that government bureaucrats are underpaid.",
        20 => "The Magistrate tells you to submit the request in writing to the Department of Petitions.",
        21 => "The Imperial Magistrate has an ulcer and refuses to speak with you.",
        _ => "The Imperial Magistrate is away on official business. Please try again another week."
    };

    public decimal PykeEngineCost(CompanyState company)
    {
        var companyRoll = PlanetSpecialRoll(company, "original company random", 1, 100);
        return companyRoll * OriginalPlanetSpecialNewsData * 6m;
    }

    public bool IsPykeEngineAvailable() => OriginalPlanetSpecialNewsData % 4 != 0;

    /// <summary>
    /// Pyke follows the original special: the quoted price is player-random ×
    /// weekly-news-random × 6, one quarter of weeks cannot supply an engine,
    /// and a successful installation is paid from cash, then savings, then the
    /// Traders' Union loan through the normal mandatory-expense ordering.
    /// </summary>
    public TradeResult ResolvePykeEnginePurchase(CompanyState company)
    {
        using var action = GameplayLogger.BeginCompanyAction(company, "PYKE ENGINE PURCHASE");
        if (company.BaseEngineSpeed >= 10)
            return TradeResult.Success("L-Tech cannot offer an engine faster than the one already installed.");
        if (!IsPykeEngineAvailable())
            return TradeResult.Success(PykeUnavailableText(company));

        var cost = PykeEngineCost(company);
        company.PayMandatoryExpense(cost);
        company.BaseEngineSpeed++;
        var slogan = PlanetSpecialRoll(company, "Pyke success wording", 1, 15) switch
        {
            1 => "This should help speed things up!",
            2 => "Faster is better!",
            3 => "It pays to be first!",
            4 => "L-Tech is the best!",
            5 => "It's worth every kubar!",
            6 => "The early bird gets the worm!",
            7 => "There's a need for speed!",
            8 => "Got to keep up with the Joneses!",
            9 => "Time is money!",
            10 => "You can never be too fast!",
            11 => "The race is on!",
            12 => "Okay, now you're moving!",
            13 => "A kuarp a day keeps the competition at bay!",
            14 => "Now you're cooking!",
            _ => "Full speed ahead!"
        };
        var message = $"You purchased L-Tech's super {company.BaseEngineSpeed}-kuarp engine for {cost:N0} kubars.\n\n{slogan}";
        return TradeResult.Success(message,
            OutcomeHighlight.Positive($"new {company.BaseEngineSpeed}-kuarp engine"),
            OutcomeHighlight.Negative($"{cost:N0} kubars"));
    }

    private string PykeUnavailableText(CompanyState company)
    {
        var rating = Math.Min(10, company.BaseEngineSpeed + 1);
        return (Week % 10) switch
        {
            0 => $"L-Tech is sold out of {rating}-kuarp engines. You will have to come back next week.",
            1 => "L-Tech's mechanic is ill, so the company cannot install a new engine this week.",
            2 => "The L-Tech engine plant is closed for no apparent reason. After waiting, you leave empty-handed.",
            3 => "Emperor Dred Nicolson ordered 500 engines for the Imperial Fleet, leaving L-Tech too busy to serve you.",
            4 => "A strike has shut down the L-Tech factory, so no engines are being sold or serviced.",
            5 => "The L-Tech plant explodes in a ball of flames just as you arrive. So much for an engine this week.",
            6 => $"L-Tech found a serious defect in its latest {rating}-kuarp engines and stopped selling them.",
            7 => $"Mr. Zinn ordered fifty {rating}-kuarp engines, leaving nothing for L-Tech to sell you.",
            8 => "Reports of bombs in three recently installed engines convince you to cancel your order.",
            _ => "L-Tech Engines is closed for a week-long national holiday. You must wait until next time."
        };
    }

    /// <summary>
    /// Reproduces Mira's 1..30 Grand Sage table. Most audiences are advice
    /// only; two are curses, while eleven grant one of the original luck floors.
    /// </summary>
    public TradeResult ResolveMiraBlessing(CompanyState company, int roll)
    {
        using var action = GameplayLogger.BeginCompanyAction(company, "MIRA BLESSING", $"roll={roll}");
        roll = Math.Clamp(roll, 1, 30);
        if (roll is 6 or 21)
        {
            company.Luck = 15;
            company.LastTravelEventGood = false;
            const string curseMessage = "The Grand Sages place an evil curse on you in the hope that overcoming hardship will strengthen your spirit.";
            return TradeResult.Success(curseMessage, OutcomeHighlight.Negative("place an evil curse on you"));
        }

        var target = roll switch
        {
            7 or 22 => 70,
            8 or 23 => 80,
            9 or 10 or 24 or 25 => 60,
            11 or 26 => 85,
            12 or 27 => 75,
            _ => 0
        };
        if (target == 0)
            return TradeResult.Success(MiraAudienceText(roll));

        company.Luck = target == 85
            ? 85
            : company.Luck < target ? target : Math.Min(85, company.Luck + 5);
        company.LastTravelEventGood = true;
        var blessing = target switch
        {
            >= 85 => "grant you their highest blessing",
            >= 80 => "grant you a high blessing",
            >= 70 => "bless you and your crew",
            _ => "grant you a small blessing"
        };
        var message = $"The Grand Sages {blessing} and turn your fortunes in a more favorable direction.";
        return TradeResult.Success(message, OutcomeHighlight.Positive(blessing));
    }

    private static string MiraAudienceText(int roll) => roll switch
    {
        1 => "The Grand Sages tell you to give to others before asking for yourself.",
        2 => "The Grand Sages tell you to examine your own actions before asking others to act.",
        3 => "The Grand Sages warn that pursuing money drains the soul and obscures true fulfillment.",
        4 => "The Grand Sages tell you to consider your neighbor's needs before your own.",
        5 => "The Grand Sages say you must understand yourself before asking others for help.",
        >= 13 and <= 15 => "An apprentice says the Grand Sages are meditating and asks you to return next week.",
        16 => "The Grand Sages advise you not to depend on others for happiness.",
        17 => "The Grand Sages remind you that pursuing material wealth leads only to discontent.",
        18 => "The Grand Sages remind you that the path to enlightenment leads inward.",
        19 => "The Grand Sages urge you to embrace the transitory nature of existence.",
        20 => "The Grand Sages instruct you to submit to reality instead of struggling to alter it.",
        >= 28 => "The Grand Sages offer spiritual advice but do not alter your fortune this week.",
        _ => "The Grand Sages offer spiritual advice but do not alter your fortune this week."
    };

    /// <summary>
    /// The original Ooom service reports the company's existing eventGood
    /// value; it never rolls or changes luck. Its quote uses the company's
    /// current 1..100 journey roll multiplied by ship mass and divided by ten.
    /// </summary>
    public decimal OoomFortuneCost(CompanyState company)
    {
        var companyRoll = Math.Abs((long)GameMath.StableHash(
            Seed, Week.ToString(), company.Name, "original company random")) % 100L + 1L;
        return GameMath.WholeKubars(companyRoll * company.ShipTons / 10m);
    }

    public TradeResult ResolveOoomFortune(CompanyState company, bool awardWindfall)
    {
        using var action = GameplayLogger.BeginCompanyAction(company, "OOOM FORTUNE",
            $"awardWindfall={awardWindfall}");
        // turn % 5 + 1 == 1 in the original: every fifth week produces one
        // of ten flavour-only unavailable results and charges nothing.
        if (Week % 5 == 0)
        {
            var unavailable = new[]
            {
                "The Soothsayers are having a party and invite you to join them. No aura reading is available this week.",
                "The Soothsayers are observing a religious festival and refuse to perform aura readings this week.",
                "The Soothsayers are restoring their own auras and cannot be disturbed this week.",
                "After waiting for several hours, no Soothsayer becomes available to help you.",
                "The Soothsayer assigned to you keeps hopping with her eyes closed and refuses to speak.",
                "An energy shift around Ooom has distorted every reading, so the Soothsayers cannot help this week.",
                "This is a religious holiday on Ooom, and the Soothsayers ask you to return another week.",
                "The Soothsayer takes a reading, mumbles something, and leaves without explaining it.",
                "Your aura is supposedly too hot to read accurately this week.",
                "The Soothsayer squeezes your hand twice and falls fast asleep before giving a reading."
            };
            var index = (int)(Math.Abs((long)GameMath.StableHash(
                Seed, Week.ToString(), "original newsData")) % unavailable.Length);
            return TradeResult.Success($"No aura reading is available this week.\n\n{unavailable[index]}");
        }

        var cost = OoomFortuneCost(company);
        if (!company.TryPayFromCashAndBank(cost))
            return TradeResult.Fail("You cannot afford the Soothsayers' fee with your cash and savings.");

        var luck = company.Luck;
        var reading = luck switch
        {
            <= 20 => ("The Soothsayer recoils from your aura and warns that you are cursed. You are headed for death and destruction unless you seek a blessing.", "you are cursed", false),
            <= 25 => ("The Soothsayer reports that you have an extremely negative aura and may suffer some great calamity.", "extremely negative aura", false),
            <= 30 => ("The Soothsayer detects extremely negative energy around your company and warns that trouble lies ahead.", "extremely negative energy", false),
            <= 35 => ("The Soothsayer declares that your aura is not good—not good at all—and advises great caution.", "not good—not good at all", false),
            <= 40 => ("The Soothsayer says your aura is rather negative and advises you to be careful over the next few weeks.", "rather negative", false),
            <= 45 => ("The reading finds your aura slightly on the negative side, although the Soothsayer says it is nothing to worry about.", "slightly on the negative side", false),
            <= 50 => ("The Soothsayer finds that your aura is average. Your luck could go either way in the near future.", "average", false),
            <= 55 => ("The Soothsayer says your aura is neither especially good nor especially bad.", "neither especially good nor especially bad", false),
            <= 60 => ("The Soothsayer detects slightly positive energy. This is good, but nothing extraordinary.", "slightly positive energy", true),
            <= 65 => ("The Soothsayer finds that your aura is positive and well above average. The odds are in your favor.", "positive and well above average", true),
            <= 70 => ("The Soothsayer says your energy levels are on the rise and that good fortune may come your way.", "energy levels are on the rise", true),
            <= 75 => ("The Soothsayer congratulates you on a very positive aura. The odds are strongly in your favor.", "very positive aura", true),
            <= 80 => ("The Soothsayer says your energy levels are fantastic and that you should expect good things soon.", "energy levels are fantastic", true),
            _ => ("The Soothsayer is astonished by the most positive energy seen in months and declares that you are truly blessed.", "truly blessed", true)
        };

        var multiplier = luck switch
        {
            > 80 => 25,
            > 75 => 20,
            > 70 => 15,
            > 65 => 10,
            > 60 => 5,
            _ => 0
        };
        if (multiplier > 0 && awardWindfall)
        {
            var windfall = cost * multiplier;
            company.Cash += windfall;
            var message = $"{reading.Item1}\n\nSoon afterward, your good fortune produces an unexpected gift of {windfall:N0} kubars.";
            return TradeResult.Success(message,
                OutcomeHighlight.Positive(reading.Item2),
                OutcomeHighlight.Positive($"{windfall:N0} kubars"));
        }

        if (luck <= 40)
            return TradeResult.Success(reading.Item1, OutcomeHighlight.Negative(reading.Item2));
        if (luck > 60)
            return TradeResult.Success(reading.Item1, OutcomeHighlight.Positive(reading.Item2));
        return TradeResult.Success(reading.Item1);
    }

    /// <summary>
    /// Reproduces Loro's crew-leave result table. Most visits are flavour, but
    /// the original can create an expense, lower salary, forgive back wages,
    /// or fill the ship's fuel tank.
    /// </summary>
    public TradeResult ResolveLoroCrewLeave(CompanyState company, int roll, decimal incidentCost)
    {
        using var action = GameplayLogger.BeginCompanyAction(company, "LORO CREW LEAVE",
            $"roll={roll}; incidentCost={incidentCost:0}");
        roll = Math.Clamp(roll, 1, 30);
        if (roll is 5 or 20)
        {
            incidentCost = Math.Max(1m, GameMath.WholeKubars(incidentCost));
            company.PayMandatoryExpense(incidentCost);
            var message = roll == 5
                ? $"Your crew gets into a fight and winds up in jail. It costs you {incidentCost:N0} kubars to bail them out."
                : $"Two crew members sink a resort's sun-bathing raft. It costs you {incidentCost:N0} kubars in damages.";
            return TradeResult.Success(message, OutcomeHighlight.Negative($"{incidentCost:N0} kubars"));
        }

        if (roll is 6 or 7 or 21 or 22 && company.CrewSalary > 1_000m)
        {
            var oldSalary = company.CrewSalary;
            company.CrewSalary = Math.Max(500m, company.CrewSalary - 100m);
            var message = $"After their vacation, your crew offers to reduce their salary from {oldSalary:N0} to {company.CrewSalary:N0} kubars per person.";
            return TradeResult.Success(message,
                OutcomeHighlight.Positive("reduce their salary"),
                OutcomeHighlight.Positive($"{oldSalary:N0} to {company.CrewSalary:N0} kubars"));
        }

        if (roll is 8 or 9 or 23 or 24 && company.CrewWagesOwed > 0m)
        {
            var forgiven = company.CrewWagesOwed;
            company.CrewWagesOwed = 0m;
            var message = $"Your grateful crew agrees to forget the {forgiven:N0} kubars you owe them in back wages.";
            return TradeResult.Success(message,
                OutcomeHighlight.Positive("agrees to forget"),
                OutcomeHighlight.Positive($"{forgiven:N0} kubars"));
        }

        if (roll is 21 or 22)
        {
            company.Fuel = company.FuelCapacity;
            const string message = "Your grateful crew chips in to fill up the ship's fuel tank.";
            return TradeResult.Success(message, OutcomeHighlight.Positive("fill up the ship's fuel tank"));
        }

        var flavour = roll switch
        {
            1 => "Your crew has a wonderful time surfing the Vorpal Pools, eating Lorinian grapes and dancing the night away.",
            2 => "Your crew returns so exhausted after a night of partying that they can barely see straight.",
            3 => "Your crew thanks you for the much-needed vacation and promises to work harder than ever.",
            4 => "Your crew brings you a giant Bigaloo Pie to show their gratitude.",
            >= 10 and <= 15 => "Your crew enjoys their short vacation and returns refreshed and ready to work.",
            16 => "Loro is bliss. Your crew explores the planet and pays court to Peelia Veelia.",
            17 => "After three nights discussing astral projection, your crew returns too tired to keep its eyes open.",
            18 => "After a few days on Loro, your crew seems ten years younger, happier, healthier and more alert.",
            19 => "Your crew has such a marvelous time that it cannot stop thanking you.",
            >= 25 => "Your crew cannot get enough of Loro and must be dragged back to the ship.",
            _ => "Your crew has a splendid vacation on Loro and returns in good spirits."
        };
        return TradeResult.Success(flavour);
    }

    /// <summary>
    /// Reproduces the Traders' Union's 1..31 Stye outcome table, including
    /// neutral audiences, credit extensions, rate changes that may backfire,
    /// and the one-third/one-quarter debt-forgiveness outcomes.
    /// </summary>
    public TradeResult ResolveStyeAssistance(CompanyState company, int roll)
    {
        using var action = GameplayLogger.BeginCompanyAction(company, "STYE ASSISTANCE", $"roll={roll}");
        roll = Math.Clamp(roll, 1, 31);
        if (roll is 7 or 22)
        {
            var increase = roll == 7 ? 50_000m : 75_000m;
            company.StandardCreditLimit += increase;
            var message = $"The Traders' Union increases your credit limit by {increase:N0} kubars to {company.StandardCreditLimit:N0}.";
            return TradeResult.Success(message, OutcomeHighlight.Positive($"increases your credit limit by {increase:N0} kubars to {company.StandardCreditLimit:N0}"));
        }

        if (roll is 8 or 23 or 24)
        {
            var oldRate = company.StandardLoanRate;
            var liquidWorth = company.Cash + company.Bank - company.Loan - company.ZinnLoan;
            company.StandardLoanRate += oldRate >= 4m || oldRate >= 2m && liquidWorth > 5_000_000m ? -1m : 1m;
            var message = company.StandardLoanRate < oldRate
                ? $"The Traders' Union lowers your loan rate from {oldRate:0.#}% to {company.StandardLoanRate:0.#}%."
                : $"The request backfires. Your loan rate rises from {oldRate:0.#}% to {company.StandardLoanRate:0.#}%.";
            return TradeResult.Success(message, company.StandardLoanRate < oldRate
                ? OutcomeHighlight.Positive($"lowers your loan rate from {oldRate:0.#}% to {company.StandardLoanRate:0.#}%")
                : OutcomeHighlight.Negative($"loan rate rises from {oldRate:0.#}% to {company.StandardLoanRate:0.#}%"));
        }

        if (roll is 9 or 25)
            return AdjustStyeSavingsRate(company);

        if ((roll is 10 or 26) && company.Loan > 20m)
        {
            var oldLoan = company.Loan;
            var divisor = roll == 10 ? 3m : 4m;
            company.Loan = GameMath.WholeKubars(oldLoan - oldLoan / divisor);
            var message = $"The Traders' Union forgives part of your debt. You now owe {company.Loan:N0} instead of {oldLoan:N0} kubars.";
            return TradeResult.Success(message,
                OutcomeHighlight.Positive("forgives part of your debt"),
                OutcomeHighlight.Positive($"{company.Loan:N0} instead of {oldLoan:N0} kubars"));
        }

        if (roll is 10 or 26)
            return AdjustStyeSavingsRate(company);

        if (roll is 11 or 27)
        {
            if (company.SavingsRate <= 1m)
            {
                var oldSavingsRate = company.SavingsRate;
                company.SavingsRate--;
                var savingsMessage = $"The request backfires. Your savings rate falls from {oldSavingsRate:0.#}% to {company.SavingsRate:0.#}%.";
                return TradeResult.Success(savingsMessage, OutcomeHighlight.Negative($"savings rate falls from {oldSavingsRate:0.#}% to {company.SavingsRate:0.#}%"));
            }

            var oldLoanRate = company.StandardLoanRate;
            company.StandardLoanRate++;
            var loanMessage = $"The request backfires. Your loan rate rises from {oldLoanRate:0.#}% to {company.StandardLoanRate:0.#}%.";
            return TradeResult.Success(loanMessage, OutcomeHighlight.Negative($"loan rate rises from {oldLoanRate:0.#}% to {company.StandardLoanRate:0.#}%"));
        }

        return TradeResult.Success(StyeAudienceText(roll));
    }

    private static string StyeAudienceText(int roll) => roll switch
    {
        1 => "The Traders' Union urges you to improve your business practices instead of relying on a bailout.",
        2 => "The Union will work on reducing your loan rate, but promises nothing.",
        3 => "The Union asks you to be patient and keep up the hard work.",
        4 => "The Union's budget is tight and it cannot help at present.",
        5 => "The Union says changing only your terms would be unfair to its other members.",
        6 => "The Union says you must first prove that your company is sound.",
        >= 12 and <= 15 => "The Traders' Union is unable to provide financial assistance at this time.",
        16 => "The Union urges you to improve profit margins and avoid unnecessary borrowing.",
        17 => "Government regulations and Imperial agreements prevent any change to your rates.",
        18 => "The Union Official is wary of promises after getting in trouble for granting a previous favor.",
        19 => "The Union Official complains that his salary was cut and the Union is losing money.",
        20 => "The Union Official does not return your calls, and his assistant refuses to arrange a meeting.",
        21 => "The Union Official doubts your company is a good credit risk and refuses to lend more.",
        >= 28 => "After waiting six hours in a huge line at Union Headquarters, you give up.",
        _ => "The Traders' Union hears your request but changes none of your financial terms this week."
    };

    private static TradeResult AdjustStyeSavingsRate(CompanyState company)
    {
        var oldRate = company.SavingsRate;
        company.SavingsRate += oldRate <= 2m ? 1m : -1m;
        var message = company.SavingsRate > oldRate
            ? $"The Traders' Union raises your savings rate from {oldRate:0.#}% to {company.SavingsRate:0.#}%."
            : $"The request backfires. Your savings rate falls from {oldRate:0.#}% to {company.SavingsRate:0.#}%.";
        return TradeResult.Success(message, company.SavingsRate > oldRate
            ? OutcomeHighlight.Positive($"raises your savings rate from {oldRate:0.#}% to {company.SavingsRate:0.#}%")
            : OutcomeHighlight.Negative($"savings rate falls from {oldRate:0.#}% to {company.SavingsRate:0.#}%"));
    }











    public void AdvanceWeek()
    {
        GameplayLogger.Log("WEEK", "SYSTEM", $"Ending week {Week}; beginning weekly processing");
        GameplayLogger.LogAllCompanyStates("BEFORE WEEKLY PROCESSING");
        ResolveAuction();
        Week++;
        WeeklyTravelEventClaims.Clear();
        AdvanceTutorialForNewWeek();
        RefreshWeather();
        var random = new Random(GameMath.StableHash(Seed, Week.ToString(), "weekly markets"));
        var previousPrices = Markets.ToDictionary(
            market => market.Key,
            market => market.Value.Listings.Select(listing => listing.Price).ToArray(),
            StringComparer.OrdinalIgnoreCase);
        var availabilityPools = Companies
            .Where(company => !company.IsBankrupt)
            .GroupBy(company => company.Planet, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(company => company.CommodityAdvertisingSupply +
                                               Math.Max(0, company.CargoCapacity - 100)),
                StringComparer.OrdinalIgnoreCase);
        foreach (var company in Companies.Where(company => !company.IsBankrupt))
        {
            company.MarketAccessPlanet = company.Planet;
            company.MarketCommodityAccessUnits = company.CommodityAdvertisingSupply +
                                                 Math.Max(0, company.CargoCapacity - 100);
            company.CommodityPurchasesThisWeek.Clear();
        }
        foreach (var market in Markets.Values)
            market.AdvanceWeek(random, Week, availabilityPools.GetValueOrDefault(market.Planet));
        foreach (var market in Markets.Values)
            GameplayLogger.Log("MARKET WEEK", "SYSTEM",
                $"planet={market.Planet}; fuel={market.FuelPrice:0}; availabilityPool={availabilityPools.GetValueOrDefault(market.Planet)}; " +
                $"listings=[{string.Join(',', market.Listings.Select((listing, commodity) =>
                    $"{commodity}:s{listing.Supply}:q{listing.Quantity}:p{listing.Price:0}"))}]");
        var movement = Markets.SelectMany(market => market.Value.Listings.Select((listing, commodity) => new
            {
                market.Key,
                Commodity = commodity,
                Change = previousPrices[market.Key][commodity] <= 0 ? 0m :
                    (listing.Price - previousPrices[market.Key][commodity]) / previousPrices[market.Key][commodity]
            }))
            .OrderByDescending(item => Math.Abs(item.Change))
            .FirstOrDefault();
        if (movement is not null && movement.Change != 0)
            LastTurnNews.Add($"Market report: {CommodityCatalog.All[movement.Commodity].Name} on {movement.Key} " +
                             $"{(movement.Change > 0 ? "rose" : "fell")} {Math.Abs(movement.Change):P0} this week.");
        LastTurnNews.Add(WeatherForecast());
        // Original frm_Travel3_gameEvents rolls 1..20 every week. Values
        // 1..16 are the sixteen economic events; 17..20 mean no event.
        var globalEventRoll = random.Next(1, 21);
        if (globalEventRoll <= 16) ApplyGlobalEconomicEvent(globalEventRoll);
        foreach (var company in Companies)
        {
            if (company.IsBankrupt) continue;
            company.CrewWagesOwed += company.CrewCount * company.CrewSalary;
            if (company.TaxesOwed + company.TariffsOwed > 0) company.TaxUnpaidWeeks++;
            else company.TaxUnpaidWeeks = 0;
            if (company.TaxUnpaidWeeks >= 4)
            {
                company.TaxesOwed = GameMath.WholeKubars(company.TaxesOwed * 1.02m);
                company.TariffsOwed = GameMath.WholeKubars(company.TariffsOwed * 1.02m);
            }
            // Kukubian rates are per journey/week. The original p-code applies
            // rate / 100 directly; these are not annual rates divided by 52.
            company.LoanInterest = GameMath.WholeKubars(company.Loan * company.StandardLoanRate / 100m);
            company.ZinnInterest = GameMath.WholeKubars(company.ZinnLoan * company.ZinnRate / 100m);
            company.SavingsInterest = Math.Min(100_000m,
                GameMath.WholeKubars(company.Bank * company.SavingsRate / 100m));
            company.Bank += company.SavingsInterest;
            company.Loan += company.LoanInterest;
            company.ZinnLoan += company.ZinnInterest;
            if (!company.IsHuman)
            {
                // Intermediate AI carries these liabilities just as a player
                // can. It pays only once the displayed amount turns red at the
                // original strike/audit danger threshold.
                if (company.CrewWagesOwed >= company.CrewCount * company.CrewSalary * 4m)
                {
                    EnsureAiCash(company, company.CrewWagesOwed);
                    company.PayCrew();
                }
                if (company.IsTaxAuditRisk)
                {
                    EnsureAiCash(company, company.TaxesOwed + company.TariffsOwed);
                    company.PayTaxes();
                }
            }
            company.PassengersPickedUp = false;
            company.Passengers = 0;
            company.StockSpentThisWeek = 0;
            company.GamblingSpentThisWeek = 0;
            company.CommodityProfitThisWeek = 0m;
            company.CommodityAdvertising = 0;
            // The original does not bankrupt a company when interest is
            // posted. It checks the current balances only when that company
            // next selects a destination, giving the player the entire turn
            // to withdraw savings, sell assets, and repay the excess.
            company.CreditCrisisNoticePending = false;
            company.BankruptcyAccepted = false;
            company.CreditCrisisWeeks = 0;
        }
        AdvanceStockMarket(random);
        // Original frm_Travel3 calls stockMarket before newsEvent. Immediate
        // price news therefore changes the completed weekly price, while a
        // forced 0/100 trend controls the following week's movement.
        ExploreContentCatalog.ApplyWeeklyNewsSignal(this);
        foreach (var company in Companies)
        {
            company.StartOfWeekNetWorth = NetWorthOf(company);
            company.NetWorthHistory.Add(company.StartOfWeekNetWorth);
            if (company.NetWorthHistory.Count > 104) company.NetWorthHistory.RemoveAt(0);
        }
        TryCreateAuction();
        GameplayLogger.Log("WEEK", "SYSTEM",
            $"Weekly processing complete; weatherPlanet={WeatherPlanet}; weatherCode={WeatherCode}; " +
            $"auction={(CurrentAuction is null ? "none" : CurrentAuction.Name)}");
        GameplayLogger.LogAllCompanyStates("AFTER WEEKLY PROCESSING");
    }

    public void PrepareTutorialStage()
    {
        if (!IsTutorial || TutorialCompleted) return;
        var automaticStage = Week switch
        {
            >= 9 => 7,
            8 => 6,
            7 => 5,
            6 => 4,
            >= 4 => 3,
            >= 3 => 2,
            _ => 1
        };
        TutorialStage = Math.Max(TutorialStage, automaticStage);
    }

    public bool ShouldShowTutorial
    {
        get
        {
            PrepareTutorialStage();
            return IsTutorial && !TutorialCompleted && Week is not (2 or 5);
        }
    }

    public bool CanAddTutorialFeature
    {
        get
        {
            PrepareTutorialStage();
            return IsTutorial && !TutorialCompleted &&
                   Companies.Count(company => company.IsHuman && !company.IsBankrupt) == 1 &&
                   Week > 9 && TutorialStage is >= 7 and < 17;
        }
    }

    public bool AddTutorialFeature()
    {
        if (!CanAddTutorialFeature) return false;
        TutorialStage++;
        return true;
    }

    private void AdvanceTutorialForNewWeek()
    {
        if (!IsTutorial || TutorialCompleted) return;
        if (TutorialStage >= 17)
        {
            TutorialCompleted = true;
            return;
        }
        var humanPlayers = Companies.Count(company => company.IsHuman && !company.IsBankrupt);
        if (TutorialStage >= 7 && humanPlayers >= 2) TutorialStage++;
    }

    private void ApplyGlobalEconomicEvent(int eventRoll)
    {
        if (Planets.Count == 0) return;
        var random = new Random(GameMath.StableHash(Seed, Week.ToString(), "global event"));
        // The original selects the affected planet with turn % 7 rather than
        // an independent random draw.
        var planet = Planets[Math.Abs(Week % Planets.Count)];
        var market = Markets[planet];
        var kind = Math.Clamp(eventRoll, 1, 16) - 1;
        var averageHumanShipTons = Companies.Where(company => company.IsHuman && !company.IsBankrupt)
            .Select(company => company.ShipTons).DefaultIfEmpty(400).Average();
        string heading;
        string message;
        if (kind <= 5)
        {
            var group = kind % 3;
            var plentiful = kind < 3;
            var first = group * 6;
            for (var commodity = first; commodity < Math.Min(first + 6, market.Listings.Count); commodity++)
            {
                var listing = market.Listings[commodity];
                if (plentiful)
                {
                    listing.Supply = random.Next(97, 101);
                    listing.Quantity += (int)averageHumanShipTons + random.Next(10, 1_001);
                    listing.PublicQuantity = Math.Max(listing.PublicQuantity, listing.Quantity);
                    // A plentiful-supply event is a hard sale-price override.
                    // Its displayed supply roll controls availability only;
                    // every affected commodity sells at its difficulty floor.
                    market.SetListingPrice(commodity,
                        PlanetMarket.MinimumPrice(commodity, Level));
                }
                else
                {
                    // Original shortage tables leave successively larger
                    // absolute lots for the six commodities in each group and
                    // force their market ratings down to 1..4.
                    listing.Supply = random.Next(1, 5);
                    listing.Quantity = Math.Max(0, 40 * (commodity - first + 1) - random.Next(0, 4));
                    listing.PublicQuantity = listing.Quantity;
                    listing.AdvertisedQuantity = 0;
                    listing.AccessPool = 0;
                    market.SetListingPrice(commodity,
                        PlanetMarket.PriceForSupply(commodity, listing.Supply, Level));
                }
            }
            (heading, message) = (group, plentiful) switch
            {
                (0, true) => ($"Great Harvest On {planet}",
                    $"Due to an unexpected record harvest, the supply of agricultural goods on {planet} temporarily soars.  Prices plummet as Cantaloupe, Jelly Beans, Moon Ferns, Frog Legs, Whip Cream and Babel Seeds flood the market."),
                (1, true) => ($"Manufactured Goods Mega Sale On {planet}",
                    $"Due to an unexpected surge in production, the supply of manufactured goods on {planet} temporarily soars.  Prices plummet as Diapers, Umbrellas, Toasters, Polyester, Hair Tonic and Lava Lamps flood the market."),
                (2, true) => ($"Raw Materials Dirt Cheap On {planet}",
                    $"Due to an unexpected rash of foreign dumping, the supply of raw materials on {planet} temporarily soars.  Prices plummet as Oxygen, Oggle Sand, Kryptoons, X Fuels, Gems and Exotic flood the market."),
                (0, false) => ($"Poor Harvest On {planet}",
                    $"Due to an unexpectedly poor harvest, the supply of agricultural goods on {planet} temporarily runs short.  Prices soar as Cantaloupe, Jelly Beans, Moon Ferns, Frog Legs, Whip Cream and Babel Seeds become hard to find."),
                (1, false) => ($"Manufactured Goods Rare On {planet}",
                    $"Due to labor unrest and strikes across {planet}, the supply of manufactured goods temporarily runs short.  Prices soar as Diapers, Umbrellas, Toasters, Polyester, Hair Tonic and Lava Lamps become hard to find."),
                _ => ($"Raw Materials Short On {planet}",
                    $"Due to an unexpected streak of natural disasters on {planet}, the supply of raw materials temporarily runs short.  Prices soar as Oxygen, Oggle Sand, Kryptoons, X Fuels, Gems and Exotic become difficult to find.")
            };
            QueueGlobalEconomicNotice(planet, kind, heading, message);
            return;
        }

        if (kind is 6 or 7)
        {
            var rising = kind == 6;
            foreach (var affected in Markets.Values)
                affected.FuelPrice = GameMath.WholeKubars(
                    Math.Max(15m, affected.FuelPrice * (rising ? 1.25m : 0.75m)));
            heading = rising ? "Fuel Prices Sky Rocket" : "Fuel Prices Plummet";
            message = rising
                ? "Due to a galactic fuel shortage, fuel prices are on the rise.\n\nYou can expect to pay higher prices at the refueling docks in the coming weeks."
                : "Due to a glut of fuel on the market, fuel prices are falling.\n\nYou can expect to see lower prices at the refueling docks in the coming weeks.";
            QueueGlobalEconomicNotice(planet, kind, heading, message);
            return;
        }

        if (kind is >= 8 and <= 13)
        {
            var rateType = (kind - 8) % 3;
            var rising = kind < 11;
            var oldRate = Companies.Count == 0 ? 0 : rateType switch
            {
                0 => Companies[0].PassengerTaxRate,
                1 => Companies[0].ExportTariffRate,
                _ => Companies[0].ImportTariffRate
            };
            foreach (var company in Companies)
            {
                if (rateType == 0) company.PassengerTaxRate = Math.Clamp(company.PassengerTaxRate + (rising ? 1 : -1), 1, 40);
                else if (rateType == 1) company.ExportTariffRate = Math.Clamp(company.ExportTariffRate + (rising ? 1 : -1), 0, 20);
                else company.ImportTariffRate = Math.Clamp(company.ImportTariffRate + (rising ? 1 : -1), 0, 20);
            }
            var rateName = rateType switch { 0 => "passenger tax", 1 => "export tariff", _ => "import tariff" };
            var newRate = Companies.Count == 0 ? 0 : rateType switch
            {
                0 => Companies[0].PassengerTaxRate,
                1 => Companies[0].ExportTariffRate,
                _ => Companies[0].ImportTariffRate
            };
            heading = (rateType, rising) switch
            {
                (0, true) => "Higher Passenger Tax Rate",
                (1, true) => "Higher Export Tariff Rate",
                (2, true) => "Higher Import Tariff Rate",
                (0, false) => "Lower Passenger Tax Rate",
                (1, false) => "Lower Export Tariff Rate",
                _ => "Lower Import Tariff Rate"
            };
            var reason = (rateType, rising) switch
            {
                (0, true) => "In order to bring in more revenue",
                (1, true) => "Because Supreme Commander Dred Nicolson has run up a big deficit",
                (2, true) => "Because politicians love to increase taxes",
                (0, false) => "In order to stimulate the sluggish economy",
                _ => "In order to stimulate trade"
            };
            message = $"{reason}, the Imperial Government {(rising ? "raises" : "lowers")} the {rateName} rate from {oldRate}% to {newRate}%.";
            QueueGlobalEconomicNotice(planet, kind, heading, message);
            return;
        }

        var boom = kind == 14;
        for (var commodity = 0; commodity < market.Listings.Count; commodity++)
        {
            var listing = market.Listings[commodity];
            if (boom)
            {
                listing.Supply = random.Next(97, 101);
                listing.Quantity += (int)averageHumanShipTons + random.Next(1, 1_001);
                listing.PublicQuantity = Math.Max(listing.PublicQuantity, listing.Quantity);
                market.SetListingPrice(commodity,
                    PlanetMarket.MinimumPrice(commodity, Level));
            }
            else
            {
                listing.Supply = random.Next(1, 5);
                listing.Quantity = Math.Max(0, 40 * (commodity % 6 + 1) - random.Next(0, 4));
                listing.PublicQuantity = listing.Quantity;
                listing.AdvertisedQuantity = 0;
                listing.AccessPool = 0;
                market.SetListingPrice(commodity,
                    PlanetMarket.PriceForSupply(commodity, listing.Supply, Level));
            }
        }
        heading = boom ? $"Prices Plummet On {planet}" : $"Prices Shoots Up On {planet}";
        message = boom
            ? $"The economy on {planet} over heats!  Productivity quadruples and prices plummet.  This economic shift has temporarily flooded {planet} with an oversupply of goods."
            : $"Due to political turmoil and hyper-inflation, there is an economic crash on {planet}!  Production comes to a halt, the supply of goods crashses, and prices shoot up.  Trading ships arriving on {planet} won't find much to buy, but anything they have on hand will sell well.";
        QueueGlobalEconomicNotice(planet, kind, heading, message);
    }

    private void QueueGlobalEconomicNotice(string planet, int kind, string heading, string message)
    {
        LastTurnNews.Add($"{heading}: {message}");
        var governmentRateChange = kind is >= 8 and <= 13;
        var notice = new TurnNotice(
            heading,
            message,
            governmentRateChange ? "TAX1_N.SWF" : $"PLANET:{planet}",
            governmentRateChange ? "TAX.MP3" : "EVENT.MP3",
            !governmentRateChange);
        // Harvests, shortages, booms and crashes are local planet notices.
        // Fuel and government-rate changes are system-wide and are therefore
        // still shown to every active human company.
        var planetLocal = kind is <= 5 or >= 14;
        foreach (var human in Companies.Where(company =>
                     company.IsHuman && !company.IsBankrupt &&
                     (!planetLocal || company.Planet.Equals(planet, StringComparison.OrdinalIgnoreCase))))
            human.PendingTurnNotices.Add(notice);
        GameplayLogger.Log("GLOBAL ECONOMIC EVENT", "SYSTEM",
            $"event={kind + 1}; planet={planet}; heading={heading}");
    }

    public TradeResult PlaceAuctionBid(CompanyState company, decimal amount)
    {
        using var action = GameplayLogger.BeginCompanyAction(company, "PLACE AUCTION BID", $"amount={amount:0}");
        if (CurrentAuction is null) return TradeResult.Fail("There is no auction this week.");
        if (!AuctionsUnlocked)
            return TradeResult.Fail("Auctions do not begin until week 11.");
        InitializeTurnOrder();
        if (company.IsHuman && !ReferenceEquals(CurrentTurnCompany, company))
            return TradeResult.Fail($"This auction screen belongs to {company.Name}, but it is no longer that company's turn.");
        amount = GameMath.WholeKubars(Math.Max(0m, amount));
        var availableFunds = company.Cash + company.Bank +
                             Math.Max(0m, company.StandardCreditLimit - company.Loan);
        if (amount > availableFunds)
            return TradeResult.Fail("Your secret bid cannot exceed your cash, savings and available credit.");
        CurrentAuction.Bids[company.Name] = amount;
        return TradeResult.Success(amount > 0
            ? $"Your secret bid of {amount:N0} kubars has been recorded."
            : "You declined to bid in this auction.");
    }

    public bool HasPendingAuctionResult(CompanyState company) =>
        PendingAuctionResult is not null && company.IsHuman && !company.IsBankrupt &&
        !AuctionResultAcknowledgedBy.Contains(company.Name);

    public void AcknowledgeAuctionResult(CompanyState company)
    {
        if (PendingAuctionResult is null) return;
        AuctionResultAcknowledgedBy.Add(company.Name);
        if (Companies.Where(candidate => candidate.IsHuman && !candidate.IsBankrupt)
            .All(candidate => AuctionResultAcknowledgedBy.Contains(candidate.Name)))
        {
            PendingAuctionResult = null;
            AuctionResultAcknowledgedBy.Clear();
        }
    }

    private void PublishAuctionResult(
        string heading,
        string message,
        string imageAsset = "DRED.SWF",
        string audioAsset = "AUCTION.MP3")
    {
        PendingAuctionResult = new AuctionResultNotice(heading, message, imageAsset, audioAsset);
        AuctionResultAcknowledgedBy.Clear();
    }

    public decimal ApplyFacilityFees(CompanyState visitor)
    {
        var collected = 0m;
        foreach (var facility in Facilities.Where(facility =>
                     facility.Planet.Equals(visitor.Planet, StringComparison.OrdinalIgnoreCase) &&
                     facility.OwnerName.Equals(visitor.Name, StringComparison.OrdinalIgnoreCase) &&
                     facility.Revenue > 0m))
        {
            collected += facility.Revenue;
            visitor.Cash += facility.Revenue;
            facility.Revenue = 0m;
        }
        if (collected > 0m)
        {
            LastTurnNews.Add($"{visitor.Name} collected {collected:N0} kubars in facility revenue on {visitor.Planet}.");
            if (visitor.IsHuman) visitor.PendingFacilityRevenue += collected;
        }

        var total = 0m;
        foreach (var facility in Facilities.Where(facility =>
                     facility.Planet.Equals(visitor.Planet, StringComparison.OrdinalIgnoreCase) &&
                     !facility.OwnerName.Equals(visitor.Name, StringComparison.OrdinalIgnoreCase)))
        {
            // Original frm_Travel5_payFacilities treats landing fees as a
            // mandatory bill: cash and savings are used first and any
            // shortfall becomes Traders' Union debt. Owners receive the full
            // fee rather than silently losing revenue when a visitor is short.
            visitor.PayMandatoryExpense(facility.Fee);
            facility.Revenue += facility.Fee;
            total += facility.Fee;
        }
        if (total > 0)
        {
            LastTurnNews.Add($"{visitor.Name} paid {total:N0} kubars in facility landing fees on {visitor.Planet}.");
            if (visitor.IsHuman) visitor.PendingFacilityFees += total;
        }
        return total;
    }

    private void TryCreateAuction()
    {
        if (Planets.Count == 0 || CurrentAuction is not null || !AuctionsUnlocked) return;
        var random = new Random(GameMath.StableHash(Seed, Week.ToString(), "auction schedule"));

        // Decompiled frm_Travel3_auction: ship auctions become available after
        // tutorial Explore (turn 14), and facilities after the Facilities lesson (turn 16).
        PrepareTutorialStage();
        var shipEligible = AuctionsUnlocked && (!IsTutorial || TutorialStage > 14);
        var facilityEligible = FacilityAuctionsUnlocked &&
                               (!IsTutorial || TutorialStage > 16);

        // OpenTradeEngine balance change: use one player-count-independent 25%
        // roll for any auction. Once both types are available, preserve the
        // original game's approximate one-ship-to-three-facilities mix.
        if ((!shipEligible && !facilityEligible) || random.Next(4) != 0) return;
        var createShipAuction = shipEligible && (!facilityEligible || random.Next(4) == 0);
        if (createShipAuction)
            CurrentAuction = new AuctionOffer("200-Ton Ship Upgrade", "Traders' Union", 0m, Week, true);
        else if (facilityEligible)
            CurrentAuction = new AuctionOffer(
                FacilityCatalog.All[random.Next(FacilityCatalog.All.Length)],
                Planets[random.Next(Planets.Count)], random.Next(500, 5_001), Week, false);

    }


    private void ResolveAuction()
    {
        if (CurrentAuction is null) return;
        var offer = CurrentAuction;
        // Discard every early offer from saves made by builds which allowed
        // ship auctions during the opening ten-week grace period.
        if (!AuctionsUnlocked)
        {
            CurrentAuction = null;
            return;
        }
        // The original waits until every human has travelled before computers
        // calculate their secret bids. Generating them when the offer opened
        // used stale cash values and made the AI appear to bid before players.
        var aiBidRandom = new Random(GameMath.StableHash(
            Seed, offer.Week.ToString(), offer.Name, offer.Planet, "auction AI bids"));
        PopulateAiAuctionBids(offer, aiBidRandom);
        var highest = offer.Bids.Values.DefaultIfEmpty(0m).Max();
        var winners = offer.Bids.Where(pair => pair.Value == highest && highest > 0).ToArray();
        if (winners.Length == 1)
        {
            var winner = Companies.FirstOrDefault(company =>
                company.Name.Equals(winners[0].Key, StringComparison.OrdinalIgnoreCase));
            if (winner is not null)
            {
                // Confirmed secret bids are binding. They are checked against
                // cash, savings and unused credit when submitted. If the
                // winner spends money afterward, the remaining purchase price
                // becomes Traders' Union debt rather than voiding the bid.
                winner.PayMandatoryExpense(highest);
                if (offer.IsShipUpgrade)
                {
                    var oldTons = winner.ShipTons;
                    TravelEncounterCatalog.ApplyShipExpansion(winner);
                    winner.ShipValue += highest;
                    LastTurnNews.Add($"{winner.Name} won the ship auction and upgraded from a {oldTons:N0}-ton " +
                                     $"to a {winner.ShipTons:N0}-ton ship for {highest:N0} kubars.");
                    PublishAuctionResult($"{winner.Name} won the auction!",
                        AuctionWinnerMessage(offer, winner, highest), AuctionWinnerImage(winner),
                        AuctionWinnerMusic(winner));
                }
                else
                {
                    Facilities.Add(new FacilityHolding(offer.Name, offer.Planet, winner.Name, offer.Fee));
                    LastTurnNews.Add($"{winner.Name} won the {offer.Name} on {offer.Planet} for {highest:N0} kubars.");
                    PublishAuctionResult($"{winner.Name} won the auction!",
                        AuctionWinnerMessage(offer, winner, highest), AuctionWinnerImage(winner),
                        AuctionWinnerMusic(winner));
                }
            }
            else
            {
                var message = $"The highest bidder for the {offer.Name} could no longer cover the {highest:N0} kubar bid. " +
                              "The auction closed without a winner.";
                LastTurnNews.Add(message);
                PublishAuctionResult("Auction Results", message);
            }
        }
        else if (winners.Length > 1)
        {
            var message = $"The auction for the {offer.Name} ended in a {highest:N0} kubar tie between " +
                          $"{string.Join(" and ", winners.Select(winner => winner.Key))}. It will be held again this week.";
            LastTurnNews.Add(message);
            PublishAuctionResult("Auction Tied", message);
            CurrentAuction = new AuctionOffer(offer.Name, offer.Planet, offer.Fee, Week + 1, offer.IsShipUpgrade);
            return;
        }
        else
        {
            var message = $"No company placed a bid for the {offer.Name}.";
            LastTurnNews.Add(message);
            PublishAuctionResult("Auction Results", message);
        }
        LastAuctionWeek = Week;
        CurrentAuction = null;
    }

    private string AuctionWinnerMessage(AuctionOffer offer, CompanyState winner, decimal highest)
    {
        var purchase = offer.IsShipUpgrade
            ? $"the 200-ton ship upgrade for {highest:N0} kubars"
            : $"the {offer.Name} on {offer.Planet} for {highest:N0} kubars";
        var runnerUp = offer.Bids
            .Where(bid => !bid.Key.Equals(winner.Name, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(bid => bid.Value)
            .FirstOrDefault();
        var runnerUpText = runnerUp.Value > 0m
            ? $"\n\nThe next highest was {runnerUp.Key} with a bid of {runnerUp.Value:N0} kubars."
            : string.Empty;
        return $"{winner.Name} bid highest, purchasing {purchase}." + runnerUpText;
    }

    private static string AuctionWinnerImage(CompanyState winner)
        => CompanyPortraitAsset(winner);

    public static string CompanyPortraitAsset(CompanyState company)
    {
        if (!company.IsHuman)
        {
            var opponent = AiOpponentCatalog.ForCompany(company.Name);
            return $"OP{opponent.Number}.PNG";
        }

        return $"SHIP{Math.Clamp(company.ShipNumber, 1, 12)}.SWF";
    }

    private static string AuctionWinnerMusic(CompanyState winner)
    {
        if (!winner.IsHuman)
            return $"OP{AiOpponentCatalog.ForCompany(winner.Name).Number}.MP3";

        var shipMusicNumber = winner.ShipNumber <= 6 ? winner.ShipNumber : winner.ShipNumber - 6;
        return $"SHIP{shipMusicNumber}.MP3";
    }

    private readonly record struct ArrivalReport(string Planet, ArrivalPurchase? Purchase);
}

public sealed class CompanyState
{
    private static readonly decimal[] AdvertisingBaseCosts = [0m, 1_000m, 2_000m, 3_000m, 4_000m, 5_000m, 10_000m];

    private static readonly int[] CargoCapacities = [100, 120, 80, 130, 100, 100, 80, 110, 90, 150, 75, 110];
    private static readonly int[] PassengerCapacities = [8, 8, 8, 11, 6, 8, 7, 5, 10, 1, 16, 8];
    private static readonly int[] FuelCapacities = [20, 40, 65, 50, 40, 40, 30, 40, 40, 35, 30, 40];
    private static readonly int[] Engines = [7, 5, 5, 2, 5, 5, 6, 6, 4, 3, 6, 6];
    private static readonly int[] StartingTurbocharges = [0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0];
    private static readonly int[] Crews = [4, 5, 3, 6, 3, 4, 4, 4, 3, 2, 12, 6];
    private static readonly string[] ShipModels =
        ["Stinger XII", "Fly Catcher", "Le Rock", "Whaler 2000", "Retina", "Cerebralis",
         "The Globulizer", "Locomotis", "Mantagon", "Kegger", "Worm Shuttle", "Squidocity"];

    public CompanyState(string name, bool isHuman, int shipNumber, string planet, decimal cash, decimal zinnLoan)
    {
        Name = name;
        IsHuman = isHuman;
        ShipNumber = Math.Clamp(shipNumber, 1, 12);
        Planet = planet;
        LastPlanet = planet;
        Cash = cash;
        ZinnLoan = zinnLoan;
        ShipValue = zinnLoan;
        Fuel = FuelCapacity;
        BaseEngineSpeed = Engines[ShipNumber - 1];
        Turbocharges = StartingTurbocharges[ShipNumber - 1];
        StartOfWeekNetWorth = NetWorth;
        PlanetVisitCounts[planet] = 1;
    }

    public string Name { get; }
    public bool IsHuman { get; }
    public int ShipNumber { get; }
    private decimal _shipValue;
    private decimal _cash;
    private decimal _bank;
    private decimal _loan;
    private decimal _zinnLoan;
    private decimal _zinnCreditLimit = 200_000m;
    private decimal _standardCreditLimit = 100_000m;
    private decimal _loanInterest;
    private decimal _zinnInterest;
    private decimal _savingsInterest;
    private decimal _pendingFacilityFees;
    private decimal _pendingFacilityRevenue;
    private decimal _startOfWeekNetWorth;
    private decimal _ticketPrice = 1_000m;
    private decimal _nextTicketPrice = 1_000m;
    private decimal _crewWagesOwed;
    private decimal _taxesOwed;
    private decimal _tariffsOwed;
    private decimal _crewSalary = 1_500m;
    private decimal _insuranceCost = 7_500m;
    private decimal _stockSpentThisWeek;
    private decimal _gamblingSpentThisWeek;
    private decimal _commodityProfitThisWeek;

    public decimal ShipValue { get => _shipValue; set => _shipValue = GameMath.WholeKubars(value); }
    public string Planet { get; set; }
    public string LastPlanet { get; set; } = string.Empty;
    public string PlannedDestination { get; set; } = string.Empty;
    public double TravelTime { get; set; }
    public decimal Cash { get => _cash; set => _cash = GameMath.WholeKubars(value); }
    public decimal Bank { get => _bank; set => _bank = GameMath.WholeKubars(value); }
    public decimal Loan { get => _loan; set => _loan = GameMath.WholeKubars(value); }
    public decimal ZinnLoan { get => _zinnLoan; set => _zinnLoan = GameMath.WholeKubars(value); }
    public decimal ZinnRate { get; set; } = 4m;
    public decimal ZinnCreditLimit { get => _zinnCreditLimit; set => _zinnCreditLimit = GameMath.WholeKubars(value); }
    public decimal StandardLoanRate { get; set; } = 5m;
    public decimal StandardCreditLimit { get => _standardCreditLimit; set => _standardCreditLimit = GameMath.WholeKubars(value); }
    public decimal SavingsRate { get; set; } = 1m;
    public decimal LoanInterest { get => _loanInterest; set => _loanInterest = GameMath.WholeKubars(value); }
    public decimal ZinnInterest { get => _zinnInterest; set => _zinnInterest = GameMath.WholeKubars(value); }
    public decimal SavingsInterest { get => _savingsInterest; set => _savingsInterest = GameMath.WholeKubars(value); }
    public decimal PendingFacilityFees { get => _pendingFacilityFees; set => _pendingFacilityFees = GameMath.WholeKubars(value); }
    public decimal PendingFacilityRevenue { get => _pendingFacilityRevenue; set => _pendingFacilityRevenue = GameMath.WholeKubars(value); }
    public decimal StartOfWeekNetWorth { get => _startOfWeekNetWorth; set => _startOfWeekNetWorth = GameMath.WholeKubars(value); }
    public List<decimal> NetWorthHistory { get; } = [];
    public decimal TicketPrice { get => _ticketPrice; set => _ticketPrice = GameMath.WholeKubars(value); }
    public decimal NextTicketPrice { get => _nextTicketPrice; set => _nextTicketPrice = GameMath.WholeKubars(value); }
    public int PassengerAdvertising { get; set; }
    public int CommodityAdvertising { get; set; }
    public string MarketAccessPlanet { get; set; } = string.Empty;
    public int MarketCommodityAccessUnits { get; set; }
    public Dictionary<int, int> CommodityPurchasesThisWeek { get; } = [];
    public bool AdvertisingLightOn { get; set; }
    public int PreferredPassengerAdvertising { get; set; }
    public int PreferredCommodityAdvertising { get; set; }
    public int Passengers { get; set; }
    public bool PassengersPickedUp { get; set; }
    public decimal Fuel { get; set; }
    public const int MinimumLuck = 15;
    public const int MaximumLuck = 85;
    private int _luck = 50;
    public int Luck
    {
        get => _luck;
        set => _luck = Math.Clamp(value, MinimumLuck, MaximumLuck);
    }
    public decimal CrewWagesOwed { get => _crewWagesOwed; set => _crewWagesOwed = GameMath.WholeKubars(value); }
    public decimal TaxesOwed { get => _taxesOwed; set => _taxesOwed = GameMath.WholeKubars(value); }
    public decimal TariffsOwed { get => _tariffsOwed; set => _tariffsOwed = GameMath.WholeKubars(value); }
    public decimal CrewSalary { get => _crewSalary; set => _crewSalary = GameMath.WholeKubars(value); }
    public int InsuranceLevel { get; set; }
    public int PassengerTaxRate { get; set; } = 15;
    public int ImportTariffRate { get; set; } = 3;
    public int ExportTariffRate { get; set; } = 2;
    public int InsurancePriceRange { get; set; } = 15;
    public decimal InsuranceCost { get => _insuranceCost; set => _insuranceCost = GameMath.WholeKubars(value); }
    public decimal InsuranceCoverage => InsuranceLevel > 0 ? 1m : 0m;
    public int BaseEngineSpeed { get; set; }
    public int Turbocharges { get; set; }
    public int CargoCapacityBonus { get; set; }
    public int PassengerCapacityBonus { get; set; }
    public int FuelCapacityBonus { get; set; }
    public int CrewCapacityBonus { get; set; }
    public int AutomatedCrewPositions { get; set; }
    public int TravelDelay { get; set; } = 1;
    public double TravelTimeMultiplier { get; set; } = 1d;
    // The original uses total ship mass—not cargo capacity—for fuel usage and
    // advertising costs. Every starting ship begins at 400 tons.
    public int ShipTons { get; set; } = 400;
    // Keep turbo state separate so it can be rebalanced later. For now it only
    // adds effective engine speed and has no fuel penalty.
    public decimal FuelMultiplier => 1m;
    public Dictionary<int, CargoLot> Cargo { get; } = [];
    public Dictionary<string, Dictionary<int, CargoLot>> Warehouses { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> PlanetVisitCounts { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, AiPassengerExperience> AiPassengerExperiences { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, AiWarehouseExperience> AiWarehouseExperiences { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> Shares { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, decimal> ShareAverageCosts { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, bool> Shortcuts { get; } = new(StringComparer.OrdinalIgnoreCase);
    public decimal StockSpentThisWeek { get => _stockSpentThisWeek; set => _stockSpentThisWeek = GameMath.WholeKubars(value); }
    public decimal GamblingSpentThisWeek { get => _gamblingSpentThisWeek; set => _gamblingSpentThisWeek = GameMath.WholeKubars(value); }
    public decimal CommodityProfitThisWeek { get => _commodityProfitThisWeek; set => _commodityProfitThisWeek = GameMath.WholeKubars(value); }
    public int LastSpecialWeek { get; set; }
    public bool IsBankrupt { get; set; }
    public bool CreditCrisisNoticePending { get; set; }
    public bool BankruptcyAccepted { get; set; }
    public int CreditCrisisWeeks { get; set; }
    public bool LastTravelEventGood { get; set; } = true;
    public int TaxUnpaidWeeks { get; set; }
    public string PendingTravelNotice { get; set; } = string.Empty;
    public string PendingExternalHeading { get; set; } = string.Empty;
    public string PendingExternalMessage { get; set; } = string.Empty;
    public string PendingExternalImage { get; set; } = string.Empty;
    public string PendingExternalAudio { get; set; } = string.Empty;
    public List<TurnNotice> PendingTurnNotices { get; } = [];

    public int BaseCargoCapacity => CargoCapacities[ShipNumber - 1];
    public int CargoCapacity => BaseCargoCapacity + CargoCapacityBonus;
    public int MarketStrength => ShipTons;
    public int PassengerCapacity => PassengerCapacities[ShipNumber - 1] + PassengerCapacityBonus;
    public int FuelCapacity => FuelCapacities[ShipNumber - 1] + FuelCapacityBonus;
    // The original warning is an absolute reserve based on total ship mass.
    // Its unusually generous margin also covers the original XOR-based distance
    // burn reproduced by TravelRules.
    public decimal LowFuelThreshold => ShipTons / 100m + 11m;
    public bool IsLowOnFuel => Fuel < LowFuelThreshold;
    public decimal TaxAuditThreshold => 35m * ShipTons;
    public bool IsTaxAuditRisk => TaxesOwed + TariffsOwed >= TaxAuditThreshold;
    public int EngineSpeed => BaseEngineSpeed + Turbocharges;
    public int CrewCount => Math.Max(2, Crews[ShipNumber - 1] + CrewCapacityBonus - AutomatedCrewPositions);
    public string ShipModel => ShipModels[ShipNumber - 1];
    public int CargoUsed => Cargo.Values.Sum(lot => lot.Quantity);
    public int CargoFree => Math.Max(0, CargoCapacity - CargoUsed);

    public void RecordPlanetVisit(string planet)
    {
        if (string.IsNullOrWhiteSpace(planet)) return;
        PlanetVisitCounts[planet] = PlanetVisitCounts.GetValueOrDefault(planet) + 1;
    }

    public decimal FacilityAuctionVisitMultiplier(string planet)
    {
        var returnVisits = Math.Max(0, PlanetVisitCounts.GetValueOrDefault(planet) - 1);
        return 1m + Math.Min(1.5m, returnVisits * 0.125m);
    }

    public void RecordAiPassengerResult(string planet, int passengers, int advertising, decimal fare)
    {
        if (IsHuman || string.IsNullOrWhiteSpace(planet)) return;
        if (!AiPassengerExperiences.TryGetValue(planet, out var experience))
        {
            experience = new AiPassengerExperience();
            AiPassengerExperiences[planet] = experience;
        }

        advertising = Math.Clamp(advertising, 0, 6);
        fare = Math.Clamp(GameMath.WholeKubars(fare), 100m, 10_000m);
        var passengerRevenue = passengers * fare * (100m - PassengerTaxRate) / 100m;
        var netProfit = GameMath.WholeKubars(passengerRevenue - AdvertisingCost(advertising));
        experience.Visits++;
        experience.LastPassengers = Math.Clamp(passengers, 0, PassengerCapacity);
        experience.LastAdvertising = advertising;
        experience.LastTicketPrice = fare;
        experience.LastNetProfit = netProfit;
        if (experience.HasBestResult && netProfit <= experience.BestNetProfit) return;
        experience.HasBestResult = true;
        experience.BestPassengers = experience.LastPassengers;
        experience.BestAdvertising = advertising;
        experience.BestTicketPrice = fare;
        experience.BestNetProfit = netProfit;
    }

    public void QueueExternalNotice(string heading, string message, string imageAsset, string audioAsset)
    {
        if (string.IsNullOrWhiteSpace(PendingExternalMessage))
        {
            PendingExternalHeading = heading;
            PendingExternalMessage = message;
            PendingExternalImage = imageAsset;
            PendingExternalAudio = audioAsset;
            return;
        }

        PendingExternalHeading = "Sabotage Report";
        PendingExternalMessage += "\n\n" + message;
    }

    public void ClearExternalNotice()
    {
        PendingExternalHeading = string.Empty;
        PendingExternalMessage = string.Empty;
        PendingExternalImage = string.Empty;
        PendingExternalAudio = string.Empty;
    }

    public decimal ProjectedLoanAfterInterest => Loan + GameMath.WholeKubars(Loan * StandardLoanRate / 100m);
    public decimal ProjectedZinnLoanAfterInterest => ZinnLoan + GameMath.WholeKubars(ZinnLoan * ZinnRate / 100m);
    // Gazillionaire compares the principal currently owed with the displayed
    // limit. It does not reserve room for the next interest charge.
    public decimal MaximumSafeUnionPrincipal => StandardCreditLimit;
    public decimal MaximumSafeZinnPrincipal => ZinnCreditLimit;
    public decimal AvailableSafeUnionCredit => Math.Max(0m, StandardCreditLimit - Loan);
    public decimal RequiredUnionCreditPayment => Math.Max(0m, Loan - StandardCreditLimit);
    public decimal RequiredZinnCreditPayment => Math.Max(0m, ZinnLoan - ZinnCreditLimit);
    public decimal RequiredCreditPayment => RequiredUnionCreditPayment + RequiredZinnCreditPayment;
    public bool WouldExceedUnionCreditLimit => Loan > StandardCreditLimit;
    public bool WouldExceedZinnCreditLimit => ZinnLoan > ZinnCreditLimit;
    public bool WouldExceedAnyCreditLimit => WouldExceedUnionCreditLimit || WouldExceedZinnCreditLimit;

    public decimal ProtectCreditLimitsBeforeTravel()
    {
        var paid = 0m;
        var unionPayment = RepayPrincipalToLimit(Loan, StandardCreditLimit);
        Loan -= unionPayment;
        paid += unionPayment;
        var zinnPayment = RepayPrincipalToLimit(ZinnLoan, ZinnCreditLimit);
        ZinnLoan -= zinnPayment;
        paid += zinnPayment;
        return paid;
    }

    public decimal PayRequiredCreditBalance()
    {
        var paid = ProtectCreditLimitsBeforeTravel();
        if (!WouldExceedAnyCreditLimit)
        {
            CreditCrisisNoticePending = false;
            BankruptcyAccepted = false;
            CreditCrisisWeeks = 0;
        }
        return paid;
    }

    private decimal RepayPrincipalToLimit(decimal principal, decimal creditLimit)
    {
        var required = Math.Max(0m, principal - creditLimit);
        if (required <= 0m) return 0m;
        var payment = Math.Min(required, Cash + Bank);
        var cashPaid = Math.Min(Cash, payment);
        Cash -= cashPaid;
        var savingsPaid = payment - cashPaid;
        Bank -= savingsPaid;
        return payment;
    }

    public int WarehouseCapacity { get; set; } = 50;
    // The original weekly ranking is liquid financial wealth. It excludes the
    // financed ship, cargo at cost, wages and tariffs that have not been paid.
    public decimal NetWorth => Cash + Bank - Loan - ZinnLoan;

    public TradeResult Buy(PlanetMarket market, int commodityIndex, int quantity)
    {
        using var action = GameplayLogger.BeginCompanyAction(this, "BUY COMMODITY",
            $"planet={market.Planet}; commodity={commodityIndex}; quantity={quantity}");
        if (quantity <= 0) return TradeResult.Fail("Enter a quantity greater than zero.");
        var listing = market.Listings[commodityIndex];
        var accessible = AccessibleCommodityQuantity(market, commodityIndex);
        if (quantity > accessible) return TradeResult.Fail($"Only {accessible} tons are available to your company.");
        if (quantity > CargoFree) return TradeResult.Fail($"Only {CargoFree} tons of cargo space remain.");
        var total = listing.Price * quantity;
        if (total > Cash) return TradeResult.Fail("You do not have enough cash.");

        Cash -= total;
        TariffsOwed += total * ImportTariffRate / 100m;
        listing.Quantity -= quantity;
        CommodityPurchasesThisWeek[commodityIndex] =
            CommodityPurchasesThisWeek.GetValueOrDefault(commodityIndex) + quantity;
        if (!Cargo.TryGetValue(commodityIndex, out var lot)) lot = new CargoLot();
        var oldValue = lot.Quantity * lot.AverageCost;
        lot.Quantity += quantity;
        lot.AverageCost = (oldValue + total) / lot.Quantity;
        Cargo[commodityIndex] = lot;
        return TradeResult.Success($"Bought {quantity} tons of {CommodityCatalog.All[commodityIndex].Name}.");
    }

    public int AccessibleCommodityQuantity(PlanetMarket market, int commodityIndex)
    {
        if (commodityIndex < 0 || commodityIndex >= market.Listings.Count) return 0;
        var listing = market.Listings[commodityIndex];
        if (!market.Planet.Equals(MarketAccessPlanet, StringComparison.OrdinalIgnoreCase) ||
            listing.AccessPool <= 0 || listing.AdvertisedQuantity <= 0)
            return listing.Quantity;

        var personalExtra = (int)decimal.Floor(
            listing.AdvertisedQuantity * Math.Max(0, MarketCommodityAccessUnits) /
            (decimal)listing.AccessPool);
        var personalLimit = listing.PublicQuantity + personalExtra;
        var alreadyBought = CommodityPurchasesThisWeek.GetValueOrDefault(commodityIndex);
        return Math.Max(0, Math.Min(listing.Quantity, personalLimit - alreadyBought));
    }

    public TradeResult BuySpecialCargo(int commodityIndex, int quantity, decimal pricePerTon)
    {
        using var action = GameplayLogger.BeginCompanyAction(this, "BUY SPECIAL CARGO",
            $"commodity={commodityIndex}; quantity={quantity}; pricePerTon={pricePerTon:0}");
        quantity = Math.Min(quantity, CargoFree);
        if (quantity <= 0) return TradeResult.Fail("There is no room in your cargo bay.");
        var total = quantity * pricePerTon;
        if (!TryPayFromCashAndBank(total))
            return TradeResult.Fail("You do not have enough cash and savings for this deal.");
        if (!Cargo.TryGetValue(commodityIndex, out var lot)) lot = new CargoLot();
        var oldValue = lot.Quantity * lot.AverageCost;
        lot.Quantity += quantity;
        lot.AverageCost = (oldValue + total) / lot.Quantity;
        Cargo[commodityIndex] = lot;
        var message = $"Bought {quantity} tons of {CommodityCatalog.All[commodityIndex].Name} for {total:N0} kubars.";
        return TradeResult.Success(message,
            OutcomeHighlight.Positive($"{quantity} tons of {CommodityCatalog.All[commodityIndex].Name}"),
            OutcomeHighlight.Negative($"{total:N0} kubars"));
    }

    public void AddFreeCargo(int commodityIndex, int quantity)
    {
        quantity = Math.Min(Math.Max(0, quantity), CargoFree);
        if (quantity == 0) return;
        if (!Cargo.TryGetValue(commodityIndex, out var lot)) lot = new CargoLot();
        var oldValue = lot.Quantity * lot.AverageCost;
        lot.Quantity += quantity;
        lot.AverageCost = oldValue / lot.Quantity;
        Cargo[commodityIndex] = lot;
        GameplayLogger.LogCompanyState("GAME ACTION", this,
            $"action=ADD FREE CARGO; commodity={commodityIndex}; quantity={quantity}");
    }

    public TradeResult Sell(PlanetMarket market, int commodityIndex, int quantity)
    {
        using var action = GameplayLogger.BeginCompanyAction(this, "SELL COMMODITY",
            $"planet={market.Planet}; commodity={commodityIndex}; quantity={quantity}");
        if (quantity <= 0) return TradeResult.Fail("Enter a quantity greater than zero.");
        if (!Cargo.TryGetValue(commodityIndex, out var lot) || lot.Quantity < quantity)
            return TradeResult.Fail("You do not have that much cargo to sell.");

        var listing = market.Listings[commodityIndex];
        var gross = listing.Price * quantity;
        CommodityProfitThisWeek += (listing.Price - lot.AverageCost) * quantity;
        Cash += gross;
        TariffsOwed += gross * ExportTariffRate / 100m;
        listing.Quantity += quantity;
        // Advertised access is a limit on net purchases at this planet, not a
        // permanent record of every Buy click. Goods sold back into the same
        // market restore both its shared quantity and the seller's consumed
        // allowance, without letting cargo brought from elsewhere create a
        // negative purchase count.
        var purchasedHere = CommodityPurchasesThisWeek.GetValueOrDefault(commodityIndex);
        if (purchasedHere > 0)
        {
            var remainingPurchases = Math.Max(0, purchasedHere - quantity);
            if (remainingPurchases == 0) CommodityPurchasesThisWeek.Remove(commodityIndex);
            else CommodityPurchasesThisWeek[commodityIndex] = remainingPurchases;
        }
        lot.Quantity -= quantity;
        if (lot.Quantity == 0) Cargo.Remove(commodityIndex);
        else Cargo[commodityIndex] = lot;
        return TradeResult.Success($"Sold {quantity} tons of {CommodityCatalog.All[commodityIndex].Name}.");
    }

    public TradeResult BuyFuel(PlanetMarket market, decimal quantity)
    {
        using var action = GameplayLogger.BeginCompanyAction(this, "BUY FUEL",
            $"planet={market.Planet}; quantity={quantity:0.###}; price={market.FuelPrice:0}");
        quantity = decimal.Round(quantity, 1);
        if (quantity <= 0) return TradeResult.Fail("Enter a fuel quantity greater than zero.");
        if (Fuel + quantity > FuelCapacity) return TradeResult.Fail("That much fuel will not fit in the tank.");
        // Fuel remains purchasable in tenths, but its cash charge is always a
        // whole kubar like every other monetary transaction.
        var cost = GameMath.WholeKubars(quantity * market.FuelPrice);
        if (cost > Cash) return TradeResult.Fail("You do not have enough cash.");
        Cash -= cost;
        Fuel += quantity;
        return TradeResult.Success($"Purchased {quantity:0.#} tons of fuel for {cost:N0} kubars.");
    }

    public decimal ApplyEmergencyRefuel()
    {
        // Decompiled original: fuelCapacity * fuelPriceRange(200) * 10 + 10,000.
        var fee = FuelCapacity * 2_000m + 10_000m;
        var cashPaid = Math.Min(Cash, fee);
        Cash -= cashPaid;
        var remaining = fee - cashPaid;
        var savingsPaid = Math.Min(Bank, remaining);
        Bank -= savingsPaid;
        remaining -= savingsPaid;
        if (remaining > 0m) Loan += remaining;
        Fuel = FuelCapacity;
        PendingTravelNotice = $"{Name} ran out of fuel. An emergency tanker refilled the ship and charged " +
                              $"{fee:N0} kubars. Voyager's Insurance does not cover this service." +
                              (remaining > 0m ? $" The unpaid {remaining:N0} kubars were added to the Traders' Union loan." : string.Empty);
        GameplayLogger.LogCompanyState("GAME ACTION", this,
            $"action=EMERGENCY REFUEL; fee={fee:0}; debtAdded={remaining:0}");
        return fee;
    }

    public TradeResult BuyFuelAtPrice(decimal quantity, decimal pricePerTon)
    {
        using var action = GameplayLogger.BeginCompanyAction(this, "BUY SPECIAL FUEL",
            $"quantity={quantity:0.###}; price={pricePerTon:0}");
        quantity = decimal.Round(Math.Min(quantity, FuelCapacity - Fuel), 1);
        if (quantity <= 0) return TradeResult.Fail("Your fuel tank is already full.");
        var cost = GameMath.WholeKubars(quantity * pricePerTon);
        if (!TryPayFromCashAndBank(cost))
            return TradeResult.Fail("You do not have enough cash and savings to fill the tank.");
        Fuel += quantity;
        var message = $"Purchased {quantity:0.#} tons of wholesale fuel for {cost:N0} kubars.";
        return TradeResult.Success(message,
            OutcomeHighlight.Positive($"{quantity:0.#} tons of wholesale fuel"),
            OutcomeHighlight.Negative($"{cost:N0} kubars"));
    }

    public TradeResult PayCrew()
    {
        using var action = GameplayLogger.BeginCompanyAction(this, "PAY CREW", $"owed={CrewWagesOwed:0}");
        if (CrewWagesOwed <= 0) return TradeResult.Fail("You do not owe your crew any wages.");
        if (Cash < CrewWagesOwed) return TradeResult.Fail("You do not have enough cash to pay the crew.");
        var paid = CrewWagesOwed;
        Cash -= paid;
        CrewWagesOwed = 0;
        return TradeResult.Success($"Paid the crew {paid:N0} kubars.");
    }

    public TradeResult PayTaxes()
    {
        using var action = GameplayLogger.BeginCompanyAction(this, "PAY TAXES",
            $"taxes={TaxesOwed:0}; tariffs={TariffsOwed:0}");
        var owed = TaxesOwed + TariffsOwed;
        if (owed <= 0) return TradeResult.Fail("You do not owe the Imperial Government any money.");
        if (Cash < owed) return TradeResult.Fail("You do not have enough cash to pay all taxes and tariffs.");
        Cash -= owed;
        TaxesOwed = 0;
        TariffsOwed = 0;
        TaxUnpaidWeeks = 0;
        return TradeResult.Success($"Paid {owed:N0} kubars in taxes and tariffs.");
    }

    public void PayMandatoryExpense(decimal amount)
    {
        var originalAmount = amount;
        amount = GameMath.WholeKubars(Math.Max(0m, amount));
        var cashPaid = Math.Min(Cash, amount);
        Cash -= cashPaid;
        amount -= cashPaid;
        var savingsPaid = Math.Min(Bank, amount);
        Bank -= savingsPaid;
        amount -= savingsPaid;
        if (amount > 0m) Loan += amount;
        GameplayLogger.LogCompanyState("GAME ACTION", this,
            $"action=MANDATORY EXPENSE; amount={originalAmount:0}; debtAdded={amount:0}");
    }

    public bool TryPayFromCashAndBank(decimal amount)
    {
        amount = GameMath.WholeKubars(Math.Max(0m, amount));
        if (Cash + Bank < amount) return false;

        var cashPaid = Math.Min(Cash, amount);
        Cash -= cashPaid;
        Bank -= amount - cashPaid;
        return true;
    }

    public bool AutoBankOnDeparture =>
        Shortcuts.GetValueOrDefault("deposit") || Shortcuts.GetValueOrDefault("bank");

    public void BankAllCash()
    {
        if (Cash <= 0m) return;
        var amount = Cash;
        Bank += Cash;
        Cash = 0m;
        GameplayLogger.LogCompanyState("GAME ACTION", this,
            $"action=AUTO BANK ALL CASH; amount={amount:0}");
    }

    public TradeResult StoreCargo(string planet, int commodityIndex, int quantity)
    {
        using var action = GameplayLogger.BeginCompanyAction(this, "STORE CARGO",
            $"planet={planet}; commodity={commodityIndex}; quantity={quantity}");
        if (!Warehouses.TryGetValue(planet, out var warehouse))
        {
            warehouse = [];
            Warehouses[planet] = warehouse;
        }
        if (!Cargo.TryGetValue(commodityIndex, out var cargo) || quantity <= 0 || quantity > cargo.Quantity)
            return TradeResult.Fail("You do not have that much cargo to store.");
        var warehouseUsed = warehouse.Values.Sum(lot => lot.Quantity);
        if (quantity > WarehouseCapacity - warehouseUsed)
            return TradeResult.Fail($"Only {Math.Max(0, WarehouseCapacity - warehouseUsed)} tons of warehouse space remain.");
        if (!warehouse.TryGetValue(commodityIndex, out var stored)) stored = new CargoLot();
        var oldValue = stored.Quantity * stored.AverageCost;
        stored.Quantity += quantity;
        stored.AverageCost = (oldValue + cargo.AverageCost * quantity) / stored.Quantity;
        warehouse[commodityIndex] = stored;
        cargo.Quantity -= quantity;
        if (cargo.Quantity == 0) Cargo.Remove(commodityIndex); else Cargo[commodityIndex] = cargo;
        return TradeResult.Success($"Stored {quantity} tons of {CommodityCatalog.All[commodityIndex].Name}.");
    }

    public TradeResult RetrieveCargo(string planet, int commodityIndex, int quantity)
    {
        using var action = GameplayLogger.BeginCompanyAction(this, "RETRIEVE CARGO",
            $"planet={planet}; commodity={commodityIndex}; quantity={quantity}");
        if (!Warehouses.TryGetValue(planet, out var warehouse) ||
            !warehouse.TryGetValue(commodityIndex, out var stored) || quantity <= 0 || quantity > stored.Quantity)
            return TradeResult.Fail("The warehouse does not contain that much cargo.");
        if (quantity > CargoFree) return TradeResult.Fail("There is not enough room on your ship.");
        if (!Cargo.TryGetValue(commodityIndex, out var cargo)) cargo = new CargoLot();
        var oldValue = cargo.Quantity * cargo.AverageCost;
        cargo.Quantity += quantity;
        cargo.AverageCost = (oldValue + stored.AverageCost * quantity) / cargo.Quantity;
        Cargo[commodityIndex] = cargo;
        stored.Quantity -= quantity;
        if (stored.Quantity == 0) warehouse.Remove(commodityIndex); else warehouse[commodityIndex] = stored;
        return TradeResult.Success($"Retrieved {quantity} tons of {CommodityCatalog.All[commodityIndex].Name}.");
    }

    public void RecordWarehouseFire(string planet, decimal actualLoss, int week, bool insured)
    {
        if (!AiWarehouseExperiences.TryGetValue(planet, out var experience))
            AiWarehouseExperiences[planet] = experience = new AiWarehouseExperience();
        experience.FireCount++;
        if (insured) experience.InsuredFireCount++;
        experience.ActualLoss += Math.Max(0m, GameMath.WholeKubars(actualLoss));
        experience.LastFireWeek = Math.Max(experience.LastFireWeek, week);
    }

    public int GeneratePassengers(Random random)
    {
        if (PassengersPickedUp) return Passengers;
        var waiting = PreviewPassengers(random);
        BoardPassengers(waiting);
        return waiting;
    }

    public int PreviewPassengers(Random random)
    {
        // Preserve the original demand pool: organic interest plus a random
        // return from the money invested in passenger advertising.
        var advertisingDemand = (int)decimal.Floor(AdvertisingCost(PassengerAdvertising) / 125m);
        var organicDemand = random.Next(0, PassengerCapacity + 1);
        var advertisingMinimum = advertisingDemand / 4;
        var advertisedDemand = advertisingDemand == 0
            ? 0
            : random.Next(advertisingMinimum, advertisingDemand + 1);
        var baseDemand = organicDemand + advertisedDemand;
        var waiting = decimal.Floor(baseDemand * 1_000m /
                                    (TicketPrice * PassengerFarePenalty(TicketPrice)));
        return Math.Clamp((int)waiting, 0, PassengerCapacity);
    }

    public decimal ExpectedPassengerDemand(int advertisingLevel)
        => ExpectedPassengerDemand(advertisingLevel, TicketPrice);

    public decimal ExpectedPassengerDemand(int advertisingLevel, decimal ticketPrice)
    {
        advertisingLevel = Math.Clamp(advertisingLevel, 0, 6);
        ticketPrice = Math.Clamp(ticketPrice, 100m, 10_000m);
        var advertisingDemand = decimal.Floor(AdvertisingCost(advertisingLevel) / 125m);
        var advertisingMinimum = decimal.Floor(advertisingDemand / 4m);
        var averageBaseDemand = PassengerCapacity / 2m +
                                (advertisingMinimum + advertisingDemand) / 2m;
        var expected = averageBaseDemand * 1_000m /
                       (ticketPrice * PassengerFarePenalty(ticketPrice));
        return Math.Clamp(expected, 0m, PassengerCapacity);
    }

    public static decimal PassengerFarePenalty(decimal ticketPrice)
    {
        ticketPrice = Math.Clamp(ticketPrice, 100m, 10_000m);
        if (ticketPrice <= 4_000m) return 1m;

        ReadOnlySpan<decimal> fares = [4_000m, 5_000m, 6_000m, 7_000m, 8_000m, 9_000m, 10_000m];
        ReadOnlySpan<decimal> penalties = [1m, 1.5m, 2.25m, 3.25m, 4.75m, 7m, 10m];
        for (var index = 1; index < fares.Length; index++)
        {
            if (ticketPrice > fares[index]) continue;
            var progress = (ticketPrice - fares[index - 1]) /
                           (fares[index] - fares[index - 1]);
            return penalties[index - 1] +
                   (penalties[index] - penalties[index - 1]) * progress;
        }
        return penalties[^1];
    }

    public TradeResult BoardPassengers(int waiting)
    {
        using var action = GameplayLogger.BeginCompanyAction(this, "PICK UP PASSENGERS",
            $"waiting={waiting}; fare={TicketPrice:0}; advertising={PassengerAdvertising}");
        if (PassengersPickedUp) return TradeResult.Fail("You have already picked up passengers on this planet.");
        waiting = Math.Clamp(waiting, 0, PassengerCapacity);
        Passengers = waiting;
        PassengersPickedUp = true;
        PassengerAdvertising = 0;
        var revenue = waiting * TicketPrice;
        Cash += revenue;
        TaxesOwed += revenue * PassengerTaxRate / 100m;
        return TradeResult.Success(waiting == 0
            ? "There are no passengers waiting at this fare."
            : $"{waiting} passenger(s) boarded for {revenue:N0} kubars.");
    }

    public TradeResult SetNextTicketPrice(decimal amount)
    {
        using var action = GameplayLogger.BeginCompanyAction(this, "SET TICKET PRICE", $"amount={amount:0}");
        amount = GameMath.WholeKubars(amount);
        if (amount < 100m) return TradeResult.Fail("The ticket price must be at least 100 kubars.");
        if (amount > 10_000m) return TradeResult.Fail("The ticket price cannot exceed 10,000 kubars.");
        NextTicketPrice = amount;
        return TradeResult.Success($"The ticket price for the next planet is now {amount:N0} kubars.");
    }

    public TradeResult DepositToBank(decimal amount)
    {
        using var action = GameplayLogger.BeginCompanyAction(this, "BANK DEPOSIT", $"amount={amount:0}");
        // Treat the displayed whole-number maximum as "all" so saves created
        // by older builds cannot leave an invisible fractional kubar behind.
        amount = GameMath.WholeKubars(amount);
        if (amount <= 0m) return TradeResult.Fail("There is no cash available to deposit.");
        if (amount > Cash) return TradeResult.Fail("You do not have enough cash.");
        Cash -= amount;
        Bank += amount;
        return TradeResult.Success($"Deposited {amount:N0} kubars.");
    }

    public TradeResult WithdrawFromBank(decimal amount)
    {
        using var action = GameplayLogger.BeginCompanyAction(this, "BANK WITHDRAWAL", $"amount={amount:0}");
        amount = GameMath.WholeKubars(amount);
        if (amount <= 0m) return TradeResult.Fail("There are no savings to withdraw.");
        if (amount > Bank)
            return TradeResult.Fail("You do not have enough money in your account.");
        Bank -= amount;
        Cash += amount;
        return TradeResult.Success($"Withdrew {amount:N0} kubars.");
    }

    public TradeResult BorrowFromTradersUnion(decimal amount)
    {
        using var action = GameplayLogger.BeginCompanyAction(this, "TRADERS UNION BORROW", $"amount={amount:0}");
        amount = GameMath.WholeKubars(amount);
        var available = AvailableSafeUnionCredit;
        if (amount <= 0m) return TradeResult.Fail("Enter an amount greater than zero.");
        if (amount > available)
            return TradeResult.Fail("You cannot borrow that much. Check your credit limit.");
        Loan += amount;
        Cash += amount;
        return TradeResult.Success($"Borrowed {amount:N0} kubars from the Traders' Union.");
    }

    public TradeResult RepayTradersUnion(decimal amount)
    {
        using var action = GameplayLogger.BeginCompanyAction(this, "TRADERS UNION REPAY", $"amount={amount:0}");
        amount = GameMath.WholeKubars(amount);
        if (Loan <= 0m)
            return TradeResult.Fail("The Traders' Union loan is already repaid.");
        if (amount <= 0m) return TradeResult.Fail(
            "Enter an amount greater than zero.");
        if (amount > Cash)
            return TradeResult.Fail("You do not have enough cash for that repayment.");
        amount = Math.Min(amount, Loan);
        if (amount <= 0m) return TradeResult.Fail(Loan <= 0m
            ? "The Traders' Union loan is already repaid."
            : "There is no cash available for a repayment.");
        Cash -= amount;
        Loan -= amount;
        return TradeResult.Success($"Repaid {amount:N0} kubars to the Traders' Union.");
    }

    public TradeResult RepayZinn(decimal amount)
    {
        using var action = GameplayLogger.BeginCompanyAction(this, "ZINN REPAY", $"amount={amount:0}");
        amount = GameMath.WholeKubars(amount);
        if (ZinnLoan <= 0m) return TradeResult.Fail("Mr. Zinn's loan is already repaid.");
        if (amount <= 0m) return TradeResult.Fail("Enter an amount greater than zero.");
        if (amount > Cash)
            return TradeResult.Fail("You do not have enough cash for that repayment.");
        amount = Math.Min(amount, ZinnLoan);
        Cash -= amount;
        ZinnLoan -= amount;
        return TradeResult.Success($"Repaid Mr. Zinn {amount:N0} kubars.");
    }

    public TradeResult SetAdvertising(bool passengers, int level)
    {
        using var action = GameplayLogger.BeginCompanyAction(this, "SET ADVERTISING",
            $"type={(passengers ? "passengers" : "commodities")}; level={level}");
        level = Math.Clamp(level, 0, 6);
        RememberAdvertisingSelection(passengers, level);
        var current = passengers ? PassengerAdvertising : CommodityAdvertising;
        var currentCost = AdvertisingCost(current);
        var newCost = AdvertisingCost(level);
        var difference = newCost - currentCost;
        if (difference > Cash) return TradeResult.Fail("You do not have enough cash for that advertising campaign.");
        Cash -= difference;
        if (passengers)
        {
            PassengerAdvertising = level;
        }
        else
        {
            CommodityAdvertising = level;
        }
        AdvertisingLightOn = PassengerAdvertising > 0 || CommodityAdvertising > 0;
        return TradeResult.Success($"{(passengers ? "Passenger" : "Commodity")} advertising set to level {level} for {newCost:N0} kubars.");
    }

    public TradeResult SetAdvertisingCampaign(int passengerLevel, int commodityLevel)
    {
        using var action = GameplayLogger.BeginCompanyAction(this, "SET ADVERTISING CAMPAIGN",
            $"passengerLevel={passengerLevel}; commodityLevel={commodityLevel}");
        passengerLevel = Math.Clamp(passengerLevel, 0, 6);
        commodityLevel = Math.Clamp(commodityLevel, 0, 6);
        RememberAdvertisingCampaign(passengerLevel, commodityLevel);
        var difference = AdvertisingCost(passengerLevel) - AdvertisingCost(PassengerAdvertising) +
                         AdvertisingCost(commodityLevel) - AdvertisingCost(CommodityAdvertising);
        if (difference > Cash) return TradeResult.Fail("You do not have enough cash for that advertising campaign.");
        Cash -= difference;
        PassengerAdvertising = passengerLevel;
        CommodityAdvertising = commodityLevel;
        AdvertisingLightOn = PassengerAdvertising > 0 || CommodityAdvertising > 0;
        return TradeResult.Success($"Placed passenger level {passengerLevel} and commodity level {commodityLevel} advertising for the next planet.");
    }

    public void RememberAdvertisingSelection(bool passengers, int level)
    {
        level = Math.Clamp(level, 0, 6);
        if (passengers) PreferredPassengerAdvertising = level;
        else PreferredCommodityAdvertising = level;
    }

    public void RememberAdvertisingCampaign(int passengerLevel, int commodityLevel)
    {
        PreferredPassengerAdvertising = Math.Clamp(passengerLevel, 0, 6);
        PreferredCommodityAdvertising = Math.Clamp(commodityLevel, 0, 6);
    }

    public TradeResult RepeatPreferredAdvertising()
    {
        using var action = GameplayLogger.BeginCompanyAction(this, "REPEAT ADVERTISING",
            $"passengerLevel={PreferredPassengerAdvertising}; commodityLevel={PreferredCommodityAdvertising}");
        var passengerLevel = Math.Clamp(PreferredPassengerAdvertising, 0, 6);
        var commodityLevel = Math.Clamp(PreferredCommodityAdvertising, 0, 6);
        var difference = AdvertisingCost(passengerLevel) - AdvertisingCost(PassengerAdvertising) +
                         AdvertisingCost(commodityLevel) - AdvertisingCost(CommodityAdvertising);
        if (difference > Cash) return TradeResult.Fail("You do not have enough cash to repeat the saved advertising campaign.");
        Cash -= difference;
        PassengerAdvertising = passengerLevel;
        CommodityAdvertising = commodityLevel;
        AdvertisingLightOn = PassengerAdvertising > 0 || CommodityAdvertising > 0;
        return TradeResult.Success($"Repeated passenger level {passengerLevel} and commodity level {commodityLevel} advertising for " +
                                   $"{AdvertisingCost(passengerLevel) + AdvertisingCost(commodityLevel):N0} kubars.");
    }

    public decimal AdvertisingCost(int level)
    {
        level = Math.Clamp(level, 0, AdvertisingBaseCosts.Length - 1);
        return GameMath.WholeKubars(AdvertisingBaseCosts[level] * ShipTons / 400m);
    }

    public int CommodityAdvertisingSupply => (int)decimal.Floor(AdvertisingCost(CommodityAdvertising) / 50m);

    public TradeResult SetInsurance(int level)
    {
        using var action = GameplayLogger.BeginCompanyAction(this, "BUY INSURANCE",
            $"level={level}; quote={InsuranceCost:0}");
        if (level <= 0) return TradeResult.Fail("Insurance already purchased cannot be cancelled for this trip.");
        if (InsuranceLevel > 0) return TradeResult.Fail("You have already purchased Voyager's Insurance for the next trip.");
        if (Cash < InsuranceCost) return TradeResult.Fail("You do not have enough cash to buy insurance.");
        Cash -= InsuranceCost;
        InsuranceLevel = 1;
        return TradeResult.Success($"Voyager's Insurance purchased for the next trip for {InsuranceCost:N0} kubars.");
    }

    public TradeResult TurbochargeEngine()
    {
        using var action = GameplayLogger.BeginCompanyAction(this, "TURBOCHARGE ENGINE");
        var cost = 10_000m + Turbocharges * 5_000m;
        if (!TryPayFromCashAndBank(cost))
            return TradeResult.Fail("You do not have enough cash and savings for another turbocharge.");
        Turbocharges++;
        return TradeResult.Success(
            $"Engine turbocharged to {EngineSpeed} kuarp. Turbo level is now {Turbocharges}.");
    }

    public TradeResult TurbochargeAtCost(decimal cost)
    {
        using var action = GameplayLogger.BeginCompanyAction(this, "TURBOCHARGE ENGINE", $"cost={cost:0}");
        if (!TryPayFromCashAndBank(cost))
            return TradeResult.Fail("There is not enough cash and savings for the turbocharge.");
        Turbocharges++;
        return TradeResult.Success($"Engine turbocharged to {EngineSpeed} kuarp.");
    }

    public TradeResult ReplaceEngine(int speed)
    {
        using var action = GameplayLogger.BeginCompanyAction(this, "REPLACE ENGINE", $"speed={speed}");
        speed = Math.Clamp(speed, 1, 10);
        var cost = speed * 12_000m;
        if (!TryPayFromCashAndBank(cost))
            return TradeResult.Fail("You do not have enough cash and savings for that engine.");
        BaseEngineSpeed = speed;
        return TradeResult.Success(
            $"Installed a new {speed}-kuarp engine. Your {Turbocharges} turbocharge upgrade(s) remain installed.");
    }

    public TradeResult ExpandCargoBay(decimal cost)
    {
        using var action = GameplayLogger.BeginCompanyAction(this, "EXPAND CARGO BAY", $"cost={cost:0}");
        if (!TryPayFromCashAndBank(cost))
            return TradeResult.Fail("You do not have enough cash and savings for the cargo-bay work.");
        CargoCapacityBonus += 10;
        var message = $"Cargo capacity increased by 10 tons to {CargoCapacity} for {cost:N0} kubars.";
        return TradeResult.Success(message,
            OutcomeHighlight.Positive($"increased by 10 tons to {CargoCapacity}"),
            OutcomeHighlight.Negative($"{cost:N0} kubars"));
    }

    public TradeResult AddPassengerSeat(decimal cost)
    {
        using var action = GameplayLogger.BeginCompanyAction(this, "ADD PASSENGER SEAT", $"cost={cost:0}");
        if (!TryPayFromCashAndBank(cost))
            return TradeResult.Fail("You do not have enough cash and savings for the passenger conversion.");
        PassengerCapacityBonus++;
        var message = $"Passenger capacity increased by one seat to {PassengerCapacity} for {cost:N0} kubars.";
        return TradeResult.Success(message,
            OutcomeHighlight.Positive($"increased by one seat to {PassengerCapacity}"),
            OutcomeHighlight.Negative($"{cost:N0} kubars"));
    }

    public TradeResult ExpandFuelTank(decimal cost)
    {
        using var action = GameplayLogger.BeginCompanyAction(this, "EXPAND FUEL TANK", $"cost={cost:0}");
        if (!TryPayFromCashAndBank(cost))
            return TradeResult.Fail("You do not have enough cash and savings for the fuel-tank work.");
        FuelCapacityBonus += 5;
        var message = $"Fuel capacity increased by 5 tons to {FuelCapacity} for {cost:N0} kubars.";
        return TradeResult.Success(message,
            OutcomeHighlight.Positive($"increased by 5 tons to {FuelCapacity}"),
            OutcomeHighlight.Negative($"{cost:N0} kubars"));
    }

    public TradeResult AutomateCrewPosition(decimal cost)
    {
        using var action = GameplayLogger.BeginCompanyAction(this, "AUTOMATE CREW POSITION", $"cost={cost:0}");
        if (CrewCount <= 2) return TradeResult.Fail("At least two crew members are still required to operate the ship.");
        PayMandatoryExpense(cost);
        AutomatedCrewPositions++;
        return TradeResult.Success($"One crew position was automated. {CrewCount} crew members remain.");
    }

    public TradeResult Gamble(decimal amount, bool won)
    {
        using var action = GameplayLogger.BeginCompanyAction(this, "GAMBLE", $"amount={amount:0}; won={won}");
        var remainingAllowance = Math.Max(0m, GameMath.WholeKubars(Cash * 0.05m) - GamblingSpentThisWeek);
        amount = GameMath.WholeKubars(amount);
        if (amount <= 0) return TradeResult.Fail("Enter a wager greater than zero.");
        if (amount > remainingAllowance)
            return TradeResult.Fail($"Under Kukubian Law, you cannot wager more than {remainingAllowance:N0} additional kubars this week.");
        if (Cash < amount) return TradeResult.Fail("You do not have enough cash for that wager.");
        GamblingSpentThisWeek += amount;
        Cash += won ? amount : -amount;
        var message = won
            ? $"You won the All Or Nothing wager and gained {amount:N0} kubars."
            : $"You lost {amount:N0} kubars at the Tilo casino.";
        return TradeResult.Success(message, won
            ? OutcomeHighlight.Positive($"gained {amount:N0} kubars")
            : OutcomeHighlight.Negative($"lost {amount:N0} kubars"));
    }

    public decimal MaximumTiloWager =>
        GamblingSpentThisWeek > 0m ? 0m : Math.Max(0m, GameMath.WholeKubars(Cash * 0.05m));

    public TradeResult PlaceTiloWager(decimal amount)
    {
        using var action = GameplayLogger.BeginCompanyAction(this, "PLACE TILO WAGER", $"amount={amount:0}");
        amount = GameMath.WholeKubars(amount);
        if (amount <= 0m) return TradeResult.Fail("You did not make a wager.");
        if (amount > Cash) return TradeResult.Fail("You cannot bet more than you have.");
        if (amount > MaximumTiloWager)
            return TradeResult.Fail($"Under Kukubian Law, you cannot wager more than {MaximumTiloWager:N0} kubars this week.");

        Cash -= amount;
        GamblingSpentThisWeek += amount;
        return TradeResult.Success($"Placed an All Or Nothing wager of {amount:N0} kubars.");
    }

    public void CollectTiloPayout(decimal amount)
    {
        Cash += Math.Max(0m, GameMath.WholeKubars(amount));
        GameplayLogger.LogCompanyState("GAME ACTION", this,
            $"action=COLLECT TILO PAYOUT; amount={amount:0}");
    }

    public TradeResult RequestZinnFavor(bool improveRate)
    {
        using var action = GameplayLogger.BeginCompanyAction(this, "REQUEST ZINN FAVOR",
            $"improveRate={improveRate}");
        if (improveRate)
        {
            if (ZinnRate >= 2m)
            {
                var oldRate = ZinnRate;
                ZinnRate--;
                var message = $"Mr. Zinn lowers your interest rate from {oldRate:0.#}% to {ZinnRate:0.#}%.";
                return TradeResult.Success(message, OutcomeHighlight.Positive($"lowers your interest rate from {oldRate:0.#}% to {ZinnRate:0.#}%"));
            }
            ZinnRate = 2m;
            return TradeResult.Success("Mr. Zinn becomes angry and raises your unusually low interest rate to 2%.",
                OutcomeHighlight.Negative("raises your unusually low interest rate to 2%"));
        }

        ZinnCreditLimit += 50_000m;
        var creditMessage = $"Mr. Zinn extends your credit limit by 50,000 kubars to {ZinnCreditLimit:N0}.";
        return TradeResult.Success(creditMessage,
            OutcomeHighlight.Positive($"extends your credit limit by 50,000 kubars to {ZinnCreditLimit:N0}"));
    }

    public TradeResult AdjustInsurancePriceRange(int change)
    {
        using var action = GameplayLogger.BeginCompanyAction(this, "ADJUST INSURANCE PRICE RANGE",
            $"change={change}");
        var previous = InsurancePriceRange;
        InsurancePriceRange = Math.Max(1, InsurancePriceRange + change);
        if (InsurancePriceRange == previous)
            return TradeResult.Success("Voyager's Insurance cannot lower your price range any further.");
        var message = change < 0
            ? $"Voyager's Insurance lowers your weekly premiums by an average of {previous - InsurancePriceRange:N0}%, from {previous:N0}% to {InsurancePriceRange:N0}%."
            : $"Voyager's Insurance raises your weekly premiums by an average of {InsurancePriceRange - previous:N0}%, from {previous:N0}% to {InsurancePriceRange:N0}%.";
        return TradeResult.Success(message, change < 0
            ? OutcomeHighlight.Positive($"lowers your weekly premiums by an average of {previous - InsurancePriceRange:N0}%")
            : OutcomeHighlight.Negative($"raises your weekly premiums by an average of {InsurancePriceRange - previous:N0}%"));
    }

    public TradeResult BuyShares(string companyName, decimal price, int requestedShares)
    {
        using var action = GameplayLogger.BeginCompanyAction(this, "BUY SHARES",
            $"exchange={companyName}; price={price:0}; shares={requestedShares}");
        if (StockSpentThisWeek > 0m)
            return TradeResult.Fail("Stock-market regulations allow only one share purchase per week.");
        var maximum = MaximumStockPurchaseShares(price);
        if (maximum <= 0)
            return TradeResult.Fail("You cannot afford one share under this week's investment limit.");
        if (requestedShares <= 0)
            return TradeResult.Fail("Enter at least one share.");
        if (requestedShares > maximum)
            return TradeResult.Fail($"You may buy no more than {maximum:N0} share(s) this week.");
        var quantity = requestedShares;
        var owned = Shares.GetValueOrDefault(companyName);
        var oldAverage = ShareAverageCosts.GetValueOrDefault(companyName);
        var gross = GameMath.WholeKubars(price * quantity);
        var commission = GameMath.WholeKubars(gross * 0.01m);
        PayMandatoryExpense(gross + commission);
        StockSpentThisWeek = gross;
        Shares[companyName] = owned + quantity;
        ShareAverageCosts[companyName] = GameMath.WholeKubars(
            (oldAverage * owned + price * quantity) / (owned + quantity));
        return TradeResult.Success(
            $"Bought {quantity:N0} share(s) on the {companyName} Exchange for {gross:N0} kubars, plus {commission:N0} kubars commission.");
    }

    public TradeResult SellShares(string companyName, decimal price, int requestedShares)
    {
        using var action = GameplayLogger.BeginCompanyAction(this, "SELL SHARES",
            $"exchange={companyName}; price={price:0}; shares={requestedShares}");
        var owned = Shares.GetValueOrDefault(companyName);
        if (requestedShares <= 0 || owned <= 0) return TradeResult.Fail("You do not own any of those shares.");
        if (requestedShares > owned)
            return TradeResult.Fail($"You own only {owned:N0} share(s) on this exchange.");
        var quantity = requestedShares;
        var average = ShareAverageCosts.GetValueOrDefault(companyName);
        var gross = GameMath.WholeKubars(price * quantity);
        var commission = GameMath.WholeKubars(gross * 0.01m);
        var grossProfit = GameMath.WholeKubars((price - average) * quantity);
        Cash += gross - commission;
        if (quantity == owned)
        {
            Shares.Remove(companyName);
            ShareAverageCosts.Remove(companyName);
        }
        else Shares[companyName] = owned - quantity;
        var result = grossProfit switch
        {
            > 0m => $"a gross profit of {grossProfit:N0} kubars",
            < 0m => $"a gross loss of {Math.Abs(grossProfit):N0} kubars",
            _ => "no gross profit or loss"
        };
        return TradeResult.Success(
            $"Sold {quantity:N0} share(s) on the {companyName} Exchange for {gross:N0} kubars with {result}. Brokerage commission was {commission:N0} kubars.");
    }

    public decimal MaximumStockInvestment =>
        Math.Max(10_000m, GameMath.WholeKubars((Cash + Bank) * 0.01m));

    public int MaximumStockPurchaseShares(decimal price) =>
        price <= 0m ? 0 : Math.Max(0, (int)decimal.Floor(MaximumStockInvestment / price));
}

public sealed class CargoLot
{
    private decimal _averageCost;

    public int Quantity { get; set; }
    public decimal AverageCost { get => _averageCost; set => _averageCost = GameMath.WholeKubars(value); }
}

public sealed class PlanetMarket
{
    private decimal _fuelPrice;

    private PlanetMarket(string planet, List<MarketListing> listings, decimal fuelPrice, int difficulty)
    {
        Planet = planet;
        Listings = listings;
        FuelPrice = fuelPrice;
        Difficulty = Math.Clamp(difficulty, 0, 4);
    }

    public string Planet { get; }
    public List<MarketListing> Listings { get; }
    public decimal FuelPrice { get => _fuelPrice; internal set => _fuelPrice = GameMath.WholeKubars(value); }
    public int Difficulty { get; }

    public static PlanetMarket Create(string planet, Random random, int difficulty)
    {
        var listings = CommodityCatalog.All.Select((commodity, index) =>
        {
            // Decompiled initializeGame: every planet/commodity comR value is
            // an independent 0..100 roll. The original does not apply a
            // commodity-specific rarity multiplier here.
            var supply = random.Next(0, 101);
            return CreateListing(commodity, index, supply, random, 0, difficulty);
        }).ToList();
        return new PlanetMarket(planet, listings, random.Next(200, 2_001), difficulty);
    }

    public void AdvanceWeek(Random random, int week, int availabilityPool = 0)
    {
        FuelPrice = Math.Clamp(FuelPrice + random.Next(-180, 181), 200m, 2_000m);
        for (var index = 0; index < Listings.Count; index++)
        {
            var current = Listings[index];
            // Decompiled frm_Travel3_economicChange: every comR moves by a
            // signed 0..20 roll, clamped to 0..100. commodityAvailable then
            // rerolls comA, and commodityPrice rebuilds comP from the new comR.
            var direction = random.Next(1, 3) == 1 ? 1 : -1;
            current.Supply = Math.Clamp(current.Supply + direction * random.Next(0, 21), 0, 100);
            current.Price = PriceForSupply(index, current.Supply, Difficulty);
            var quantities = CreateQuantities(current.Supply, random, availabilityPool);
            current.PublicQuantity = quantities.Public;
            current.AdvertisedQuantity = quantities.Advertised;
            current.AccessPool = Math.Max(0, availabilityPool);
            current.Quantity = quantities.Public + quantities.Advertised;
        }
    }

    internal void RestoreListing(int commodityIndex, int supply, decimal price, int quantity,
        int? publicQuantity = null, int advertisedQuantity = 0, int accessPool = 0)
    {
        if (commodityIndex < 0 || commodityIndex >= Listings.Count) return;
        var listing = Listings[commodityIndex];
        listing.Supply = Math.Clamp(supply, 0, 100);
        listing.Price = ClampCommodityPrice(commodityIndex, price);
        listing.Quantity = Math.Max(0, quantity);
        listing.PublicQuantity = Math.Max(0, publicQuantity ?? quantity);
        listing.AdvertisedQuantity = Math.Max(0, advertisedQuantity);
        listing.AccessPool = Math.Max(0, accessPool);
    }

    internal void SetListingPrice(int commodityIndex, decimal price)
    {
        if (commodityIndex < 0 || commodityIndex >= Listings.Count) return;
        Listings[commodityIndex].Price = ClampCommodityPrice(commodityIndex, price);
    }

    private static MarketListing CreateListing(
        CommodityDefinition commodity, int commodityIndex, int supply, Random random,
        int availabilityPool, int difficulty)
    {
        var minimumPrice = MinimumPrice(commodityIndex, difficulty);
        var price = PriceForSupply(commodityIndex, supply, difficulty);
        // Original comA generation: a random 20%-to-100% portion of comR,
        // plus a similarly randomized portion of the planet's pooled advertising
        // and excess ship capacity, both scaled by comR. A 0..130 roll can still
        // make a commodity unavailable, preserving genuine shortages.
        var quantities = CreateQuantities(supply, random, availabilityPool);
        return new MarketListing(supply,
            Math.Clamp(GameMath.WholeKubars(price), minimumPrice, commodity.MaximumPrice),
            quantities.Public + quantities.Advertised)
        {
            PublicQuantity = quantities.Public,
            AdvertisedQuantity = quantities.Advertised,
            AccessPool = Math.Max(0, availabilityPool)
        };
    }

    private decimal ClampCommodityPrice(int commodityIndex, decimal price)
    {
        var commodity = CommodityCatalog.All[commodityIndex];
        return Math.Clamp(GameMath.WholeKubars(price), MinimumPrice(commodityIndex, Difficulty), commodity.MaximumPrice);
    }

    public static decimal MinimumPrice(int commodityIndex, int difficulty) =>
        5m * (Math.Clamp(difficulty, 0, 4) + 1) * (commodityIndex + 1);

    public (decimal Minimum, decimal Maximum) PriceRange(int commodityIndex)
    {
        commodityIndex = Math.Clamp(commodityIndex, 0, CommodityCatalog.All.Length - 1);
        return (MinimumPrice(commodityIndex, Difficulty), CommodityCatalog.All[commodityIndex].MaximumPrice);
    }

    public static decimal PriceForSupply(int commodityIndex, int supply, int difficulty)
    {
        var minimum = MinimumPrice(commodityIndex, difficulty);
        var maximum = CommodityCatalog.All[commodityIndex].MaximumPrice;
        return Math.Clamp(GameMath.WholeKubars(minimum + (maximum - minimum) *
            (1m - Math.Clamp(supply, 0, 100) / 100m)), minimum, maximum);
    }

    private static (int Public, int Advertised) CreateQuantities(int supply, Random random, int availabilityPool)
    {
        var baseMinimum = (int)Math.Floor(supply * 0.2m);
        var baseQuantity = RandomInclusive(random, baseMinimum, supply);
        var scaledPool = (int)Math.Floor(availabilityPool * supply / 100m);
        var poolQuantity = RandomInclusive(random, (int)Math.Floor(scaledPool * 0.2m), scaledPool);
        return random.Next(0, 131) >= supply ? (0, 0) : (baseQuantity, poolQuantity);
    }

    private static int RandomInclusive(Random random, int minimum, int maximum)
    {
        if (maximum <= minimum) return Math.Max(0, minimum);
        return random.Next(Math.Max(0, minimum), maximum + 1);
    }
}

public sealed class MarketListing(int supply, decimal price, int quantity)
{
    private decimal _price = GameMath.WholeKubars(price);

    public int Supply { get; set; } = supply;
    public decimal Price { get => _price; set => _price = GameMath.WholeKubars(value); }
    public int Quantity { get; set; } = quantity;
    public int PublicQuantity { get; set; } = quantity;
    public int AdvertisedQuantity { get; set; }
    public int AccessPool { get; set; }
}

public sealed record CommodityDefinition(string Name, decimal MinimumPrice, decimal MaximumPrice, int AvailabilityScale);

public static class CommodityCatalog
{
    private static readonly string[] AudioFiles =
    [
        "CANTALOU.MP3", "JELLYBEA.MP3", "MOONFERN.MP3", "FROGLEG.MP3", "WHIPCREM.MP3", "BABEL.MP3",
        "DIAPERS.MP3", "UMBRELLA.MP3", "TOASTERS.MP3", "POLYESTR.MP3", "TONIC.MP3", "LAVALAMP.MP3",
        "OXYGEN.MP3", "OGGLE.MP3", "KRYPTOON.MP3", "XFUEL.MP3", "GEMS.MP3", "EXOTIC.MP3"
    ];

    public static readonly CommodityDefinition[] All =
    [
        // Original marketplace ranges increase by five kubars per commodity;
        // each maximum is exactly eight times its minimum.
        new("Cantaloupe", 5, 40, 150), new("Jelly Beans", 10, 80, 140),
        new("Moon Ferns", 15, 120, 125), new("Frog Legs", 20, 160, 115),
        new("Whip Cream", 25, 200, 105), new("Babel Seeds", 30, 240, 95),
        new("Diapers", 35, 280, 85), new("Umbrellas", 40, 320, 78),
        new("Toasters", 45, 360, 70), new("Polyester", 50, 400, 62),
        new("Hair Tonic", 55, 440, 55), new("Lava Lamps", 60, 480, 48),
        new("Oxygen", 65, 520, 42), new("Oggle Sand", 70, 560, 36),
        new("Kryptoons", 75, 600, 30), new("X Fuels", 80, 640, 24),
        new("Gems", 85, 680, 17), new("Exotic", 90, 720, 10)
    ];

    public static string AudioFile(int commodityIndex) =>
        AudioFiles[Math.Clamp(commodityIndex, 0, AudioFiles.Length - 1)];
}

public static class TravelRules
{
    // These are the seven navigation coordinates assigned by the original game.
    // The selected planets are shuffled into these slots at campaign creation.
    private static readonly (double X, double Y)[] MapSlots =
        [(20, 0), (14, 6), (11, 13), (8, 3), (3, 10), (21, 11), (0, 1)];

    public static (double X, double Y) MapPosition(int index) =>
        index >= 0 && index < MapSlots.Length ? MapSlots[index] : (10, 6);

    public static decimal FuelCost(
        string from, string to, CompanyState company, IReadOnlyList<string>? planetOrder = null,
        int week = 0)
    {
        var fromIndex = planetOrder is null ? -1 : IndexOf(planetOrder, from);
        var toIndex = planetOrder is null ? -1 : IndexOf(planetOrder, to);
        double distanceAllowance;
        if (fromIndex >= 0 && toIndex >= 0 && fromIndex < MapSlots.Length && toIndex < MapSlots.Length)
        {
            distanceAllowance = OriginalFuelDistanceAllowance(fromIndex, toIndex);
        }
        else
        {
            var hashDistance = Math.Abs((long)GameMath.StableHash(0, from) - GameMath.StableHash(0, to));
            distanceAllowance = (2d + hashDistance % 22) / 2d;
        }

        // Decompiled original: rnd(1, distance / 2) + rnd(1, shipTons / 100).
        // The original source attempted to square each coordinate delta with ^ 2.
        // In ActionScript ^ is integer XOR, and additive precedence turns the
        // expression into dx ^ (2 + dy) ^ 2. A negative result reaches sqrt as
        // NaN, which f_rnd's int return coerces to zero. Reproducing that old bug
        // is necessary for fuel to drain at the same rate as Gazillionaire.
        // A stable weekly seed keeps the quoted and charged values identical.
        var random = new Random(GameMath.StableHash(week, company.Name, from, to, "fuel"));
        var distanceBurn = OriginalInclusiveRoll(random, distanceAllowance);
        var massBurn = Math.Floor(1d + random.NextDouble() * (company.ShipTons / 100d));
        var baseCost = (decimal)(distanceBurn + massBurn);
        return decimal.Round(baseCost * company.FuelMultiplier, 1);
    }

    public static decimal MaximumFuelCost(CompanyState company)
    {
        var greatestDistanceBurn = 0d;
        for (var from = 0; from < MapSlots.Length; from++)
        {
            for (var to = 0; to < MapSlots.Length; to++)
            {
                if (from == to) continue;
                var allowance = OriginalFuelDistanceAllowance(from, to);
                if (!double.IsNaN(allowance))
                    greatestDistanceBurn = Math.Max(greatestDistanceBurn, Math.Ceiling(allowance));
            }
        }

        var maximumMassBurn = Math.Ceiling(company.ShipTons / 100d);
        var maximumBaseBurn = (decimal)(greatestDistanceBurn + maximumMassBurn);
        return decimal.Round(maximumBaseBurn * company.FuelMultiplier, 1);
    }

    private static double OriginalFuelDistanceAllowance(int fromIndex, int toIndex)
    {
        // Gazillionaire has already moved `planet` to the destination and keeps
        // the origin in `planetLast` when it performs this subtraction.
        var dx = (int)(MapSlots[toIndex].X - MapSlots[fromIndex].X);
        var dy = (int)(MapSlots[toIndex].Y - MapSlots[fromIndex].Y);
        var mistakenSquaredDistance = dx ^ (2 + dy) ^ 2;
        return Math.Sqrt(mistakenSquaredDistance) / 2d;
    }

    private static double OriginalInclusiveRoll(Random random, double maximum)
    {
        if (double.IsNaN(maximum)) return 0d;
        return Math.Floor(1d + random.NextDouble() * maximum);
    }

    public static double TravelTime(
        string from, string to, CompanyState company, IReadOnlyList<string>? planetOrder = null)
    {
        var fromIndex = planetOrder is null ? -1 : IndexOf(planetOrder, from);
        var toIndex = planetOrder is null ? -1 : IndexOf(planetOrder, to);
        if (fromIndex < 0 || toIndex < 0 || fromIndex >= MapSlots.Length || toIndex >= MapSlots.Length)
            return 5d / Math.Max(1, company.EngineSpeed);

        var dx = MapSlots[fromIndex].X - MapSlots[toIndex].X;
        var dy = MapSlots[fromIndex].Y - MapSlots[toIndex].Y;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        return distance * 5d / Math.Max(1, company.EngineSpeed);
    }

    private static int IndexOf(IReadOnlyList<string> planets, string planet)
    {
        for (var index = 0; index < planets.Count; index++)
            if (planets[index].Equals(planet, StringComparison.OrdinalIgnoreCase)) return index;
        return -1;
    }
}

internal sealed class PrimaryTravelEventState(int seed)
{
    public Random Random { get; } = new(seed);
    public bool QuasoChecked { get; set; }
    public bool ChainChosen { get; set; }
    public bool GoodChain { get; set; }
    public int GoodEventSlot { get; set; } = 2;
    public int BadEventIndex { get; set; }
    public int ModEventIndex { get; set; }
    public bool Finished { get; set; }
}

public sealed class JourneyEventSequence
{
    private readonly GameSession _session;
    private readonly CompanyState _company;
    private readonly PrimaryTravelEventState _primaryEvents;
    private int _stage;
    private bool _awaitingCompletion;
    private bool _fuelDelayPending;
    private bool _finished;

    internal JourneyEventSequence(GameSession session, CompanyState company)
    {
        _session = session;
        _company = company;
        _primaryEvents = session.BeginPrimaryTravelEvents(company);
        _session.LastJourneyEvents[company.Name] = [];
    }

    public TravelEventResult? Next()
    {
        if (_awaitingCompletion)
            throw new InvalidOperationException("Complete the current journey event before requesting the next one.");
        if (_finished) return null;

        while (true)
        {
            TravelEventResult? result;
            switch (_stage)
            {
                case 0:
                    result = _session.ResolveNextPrimaryTravelEvent(_company, _primaryEvents);
                    if (result is null) _stage = 1;
                    break;
                case 1:
                    _stage = 2;
                    result = _session.ResolveCrewStrike(_company);
                    break;
                case 2:
                    _stage = 3;
                    result = _session.ResolveFuelFailure(_company, out _fuelDelayPending);
                    break;
                case 3:
                    _stage = 4;
                    result = _fuelDelayPending ? _session.FuelDelayResult(_company) : null;
                    break;
                case 4:
                    _stage = 5;
                    result = _session.ResolveTaxAudit(_company);
                    break;
                default:
                    _session.FinalizeJourneyEvents(_company);
                    _finished = true;
                    return null;
            }
            if (result is null) continue;
            _awaitingCompletion = true;
            GameplayLogger.Log("EVENT PRESENTED", _company.Name,
                $"stage={_stage}; heading={result.Heading}; good={result.IsGood}; " +
                $"hasChoice={result.Choice is not null}; message={result.Message}");
            return result;
        }
    }

    public void Complete(TravelEventResult result)
    {
        if (!_awaitingCompletion)
            throw new InvalidOperationException("There is no active journey event to complete.");
        if (result.LuckOverride is int luckOverride)
        {
            _company.Luck = luckOverride;
            _company.LastTravelEventGood = result.IsGood;
        }
        _session.RecordJourneyEvent(_company, result);
        GameplayLogger.Log("EVENT OUTCOME", _company.Name,
            $"heading={result.Heading}; good={result.IsGood}; message={result.Message}");
        GameplayLogger.LogCompanyState("EVENT STATE", _company, $"after event={result.Heading}");
        _awaitingCompletion = false;
    }
}

public static class GameMath
{
    /// <summary>
    /// Kubars are indivisible. Monetary calculations use conventional nearest-
    /// whole rounding, with exact halves rounded away from zero.
    /// </summary>
    public static decimal WholeKubars(decimal amount) =>
        decimal.Round(amount, 0, MidpointRounding.AwayFromZero);

    public static int StableHash(int seed, params string[] values)
    {
        unchecked
        {
            var hash = seed == 0 ? 17 : seed;
            foreach (var value in values)
                foreach (var character in value.ToUpperInvariant()) hash = hash * 31 + character;
            return hash == int.MinValue ? int.MaxValue : hash;
        }
    }
}

public enum OutcomeTone
{
    Positive,
    Negative
}

public sealed record OutcomeHighlight(string Text, OutcomeTone Tone)
{
    public static OutcomeHighlight Positive(string text) => new(text, OutcomeTone.Positive);
    public static OutcomeHighlight Negative(string text) => new(text, OutcomeTone.Negative);
}

public sealed record TradeResult(bool IsSuccessful, string Message, params OutcomeHighlight[] Highlights)
{
    public static TradeResult Success(string message, params OutcomeHighlight[] highlights)
    {
        var result = new TradeResult(true, message, highlights);
        GameplayLogger.RecordTradeResult(result);
        return result;
    }

    public static TradeResult Fail(string message, params OutcomeHighlight[] highlights)
    {
        var result = new TradeResult(false, message, highlights);
        GameplayLogger.RecordTradeResult(result);
        return result;
    }
}

public sealed record TravelEventResult(
    string Heading,
    string Message,
    bool IsGood,
    string ImageAsset = "",
    string AudioAsset = "",
    TravelEventChoice? Choice = null,
    int? LuckOverride = null,
    bool SkipOutcomeScreen = false,
    bool SuppressAiEventNotice = false);

public sealed record TurnNotice(
    string Heading,
    string Message,
    string ImageAsset,
    string AudioAsset = "",
    bool UseCompanyAnnouncement = false);

public sealed class AiPassengerExperience
{
    public int Visits { get; set; }
    public int LastPassengers { get; set; }
    public int LastAdvertising { get; set; }
    public decimal LastTicketPrice { get; set; } = 1_000m;
    public decimal LastNetProfit { get; set; }
    public bool HasBestResult { get; set; }
    public int BestPassengers { get; set; }
    public int BestAdvertising { get; set; }
    public decimal BestTicketPrice { get; set; } = 1_000m;
    public decimal BestNetProfit { get; set; }
}

public sealed class AiWarehouseExperience
{
    public int FireCount { get; set; }
    public int InsuredFireCount { get; set; }
    public decimal ActualLoss { get; set; }
    public int LastFireWeek { get; set; }
}

public sealed class TravelEventChoice(
    string acceptLabel,
    string declineLabel,
    bool aiAccepts,
    Func<bool, TravelEventResult> resolver)
{
    private Func<bool, TravelEventResult>? _resolver = resolver;
    private TravelEventResult? _resolved;
    private Action<TravelEventResult>? _whenResolved;
    public string AcceptLabel { get; } = acceptLabel;
    public string DeclineLabel { get; } = declineLabel;
    public bool AiAccepts { get; } = aiAccepts;

    public TravelEventResult Resolve(bool accepted)
    {
        if (_resolved is not null) return _resolved;
        var apply = _resolver ?? throw new InvalidOperationException("This travel choice has already been resolved.");
        _resolved = apply(accepted);
        _resolver = null;
        _whenResolved?.Invoke(_resolved);
        _whenResolved = null;
        return _resolved;
    }

    public void WhenResolved(Action<TravelEventResult> callback)
    {
        if (_resolved is not null) callback(_resolved);
        else _whenResolved += callback;
    }
}

public sealed record FacilityHolding
{
    private decimal _revenue;

    public FacilityHolding(string name, string planet, string ownerName, decimal fee)
    {
        Name = name;
        Planet = planet;
        OwnerName = ownerName;
        Fee = GameMath.WholeKubars(fee);
    }

    public string Name { get; }
    public string Planet { get; }
    public string OwnerName { get; }
    public decimal Fee { get; }
    public decimal Revenue { get => _revenue; set => _revenue = GameMath.WholeKubars(value); }
}

public sealed record AuctionResultNotice(
    string Heading,
    string Message,
    string ImageAsset = "DRED.SWF",
    string AudioAsset = "AUCTION.MP3");

public sealed class AuctionOffer(string name, string planet, decimal fee, int week, bool isShipUpgrade = false)
{
    public string Name { get; } = name;
    public string Planet { get; } = planet;
    public decimal Fee { get; } = GameMath.WholeKubars(fee);
    public int Week { get; } = week;
    public bool IsShipUpgrade { get; } = isShipUpgrade;
    public Dictionary<string, decimal> Bids { get; } = new(StringComparer.OrdinalIgnoreCase);
    // Decompiled frm_Auction_ok passes these exact three values into
    // showAmountNew. They are auction-specific suggestions, not fractions of
    // the bidder's cash, credit, or net worth.
    public (decimal Lower, decimal Middle, decimal Upper) BidPresets => IsShipUpgrade
        ? (10_000m, 25_000m, 50_000m)
        : (5m * Fee, 15m * Fee, 30m * Fee);
}
