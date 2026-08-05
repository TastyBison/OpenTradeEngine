using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace OpenTradeEngine;

public static class GameSaveService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string AutosavePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OpenTradeEngine", "saves", "autosave.json");

    public static bool AutosaveExists => File.Exists(AutosavePath);

    public static string SlotPath(int slot) => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OpenTradeEngine", "saves", $"slot-{Math.Clamp(slot, 1, 6)}.json");

    public static void SaveSlot(GameSession session, int slot) => Save(session, SlotPath(slot));
    public static GameSession? LoadSlot(int slot) => Load(SlotPath(slot));

    public static void SaveAutosave(GameSession session) => Save(session, AutosavePath);

    public static void Save(GameSession session, string path)
    {
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var data = new SessionSave(
            session.Level, session.Seed, session.Week, session.ActiveHumanIndex, session.WinTarget, session.Planets.ToArray(),
            session.Companies.Select(company => new CompanySave(
                company.Name, company.IsHuman, company.ShipNumber, company.Planet, company.Cash, company.ShipValue,
                company.Bank, company.Loan, company.ZinnLoan, company.ZinnRate, company.ZinnCreditLimit,
                company.StandardLoanRate, company.StandardCreditLimit, company.SavingsRate,
                company.LoanInterest, company.ZinnInterest,
                company.SavingsInterest, company.TicketPrice, company.PassengerAdvertising,
                company.CommodityAdvertising, company.PreferredPassengerAdvertising,
                company.PreferredCommodityAdvertising, company.Passengers, company.PassengersPickedUp,
                company.Fuel, company.Cargo.ToDictionary(pair => pair.Key,
                    pair => new CargoSave(pair.Value.Quantity, pair.Value.AverageCost)),
                company.Warehouses.ToDictionary(warehouse => warehouse.Key,
                    warehouse => warehouse.Value.ToDictionary(pair => pair.Key,
                        pair => new CargoSave(pair.Value.Quantity, pair.Value.AverageCost))),
                new Dictionary<string, int>(company.Shares), new Dictionary<string, bool>(company.Shortcuts),
                company.StockSpentThisWeek, company.GamblingSpentThisWeek,
                company.LastSpecialWeek, company.Luck, company.IsBankrupt, company.LastTravelEventGood,
                company.TaxUnpaidWeeks,
                company.CrewWagesOwed, company.TaxesOwed, company.TariffsOwed, company.CrewSalary, company.WarehouseCapacity,
                company.InsuranceLevel, company.InsurancePriceRange, company.InsuranceCost,
                company.PassengerTaxRate, company.ImportTariffRate, company.ExportTariffRate,
                company.BaseEngineSpeed, company.Turbocharges,
                company.CargoCapacityBonus, company.PassengerCapacityBonus, company.FuelCapacityBonus,
                company.ShipTons, company.StartOfWeekNetWorth,
                new Dictionary<string, decimal>(company.ShareAverageCosts), company.LastPlanet,
                company.TravelTime, company.TravelDelay, company.AutomatedCrewPositions,
                company.CrewCapacityBonus, company.CommodityProfitThisWeek,
                company.NetWorthHistory.ToArray(), company.PlannedDestination, company.TravelTimeMultiplier,
                company.NextTicketPrice, company.CreditCrisisNoticePending,
                company.BankruptcyAccepted, company.CreditCrisisWeeks,
                company.PendingFacilityFees, company.PendingFacilityRevenue,
                company.PendingExternalHeading, company.PendingExternalMessage,
                company.PendingExternalImage, company.PendingExternalAudio,
                company.PendingTurnNotices.Select(notice => new TurnNoticeSave(
                    notice.Heading, notice.Message, notice.ImageAsset, notice.AudioAsset,
                    notice.UseCompanyAnnouncement)).ToArray(),
                new Dictionary<string, int>(company.PlanetVisitCounts),
                company.AdvertisingLightOn,
                company.AiPassengerExperiences.ToDictionary(pair => pair.Key, pair =>
                    new AiPassengerExperienceSave(pair.Value.Visits,
                        pair.Value.LastPassengers, pair.Value.LastAdvertising,
                        pair.Value.LastTicketPrice, pair.Value.LastNetProfit,
                        pair.Value.HasBestResult, pair.Value.BestPassengers,
                        pair.Value.BestAdvertising, pair.Value.BestTicketPrice,
                        pair.Value.BestNetProfit), StringComparer.OrdinalIgnoreCase),
                company.MarketAccessPlanet, company.MarketCommodityAccessUnits,
                new Dictionary<int, int>(company.CommodityPurchasesThisWeek),
                company.AiWarehouseExperiences.ToDictionary(pair => pair.Key, pair =>
                    new AiWarehouseExperienceSave(pair.Value.FireCount,
                        pair.Value.InsuredFireCount, pair.Value.ActualLoss,
                        pair.Value.LastFireWeek), StringComparer.OrdinalIgnoreCase))).ToArray(),
            session.Markets.Values.Select(market => new MarketSave(
                market.Planet, market.FuelPrice,
                market.Listings.Select(listing => new ListingSave(
                    listing.Supply, listing.Price, listing.Quantity, listing.PublicQuantity,
                    listing.AdvertisedQuantity, listing.AccessPool)).ToArray())).ToArray(),
            new Dictionary<string, decimal>(session.SharePrices),
            new Dictionary<string, int>(session.ExchangeClosedThroughWeek), session.WeatherPlanet, session.WeatherCode,
            session.Facilities.Select(facility => new FacilitySave(
                facility.Name, facility.Planet, facility.OwnerName, facility.Fee, facility.Revenue)).ToArray(),
            session.CurrentAuction is null ? null : new AuctionSave(
                session.CurrentAuction.Name, session.CurrentAuction.Planet, session.CurrentAuction.Fee,
                session.CurrentAuction.Week, session.CurrentAuction.IsShipUpgrade,
                new Dictionary<string, decimal>(session.CurrentAuction.Bids)), session.LastTurnNews.ToArray(),
            session.SharePriceHistory.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(),
                StringComparer.OrdinalIgnoreCase), session.TurnOrder.ToArray(), session.ActiveTurnIndex,
            session.TutorialStage, session.TutorialCompleted, session.PendingAuctionResult,
            session.AuctionResultAcknowledgedBy.ToArray(), session.LastAuctionWeek,
            new Dictionary<string, int>(session.StockTrends), session.CrashedExchanges.ToArray(),
            session.AiEventVisibility, session.WeeklyTravelEventClaims.ToArray());
        File.WriteAllText(path, JsonSerializer.Serialize(data, JsonOptions));
        GameplayLogger.Log("SAVE", "SYSTEM", $"Saved game to {path}");
        GameplayLogger.LogAllCompanyStates("SAVE COMPLETED");
    }

    public static GameSession? LoadAutosave() => Load(AutosavePath);

    public static GameSession? Load(string path)
    {
        GameplayLogger.Log("LOAD", "SYSTEM", $"Requested load from {path}");
        if (!File.Exists(path))
        {
            GameplayLogger.Log("LOAD", "SYSTEM", "Load failed: file does not exist.");
            return null;
        }
        try
        {
            var data = JsonSerializer.Deserialize<SessionSave>(File.ReadAllText(path), JsonOptions);
            if (data is null || data.Planets.Length == 0) return null;
            var session = new GameSession(data.Difficulty, data.Planets, data.Seed)
                { Week = data.Week, ActiveHumanIndex = data.ActiveHumanIndex,
                    WinTarget = data.WinTarget == 0m ? GameSession.StandardWinTarget : GameMath.WholeKubars(data.WinTarget),
                    WeatherPlanet = data.WeatherPlanet, WeatherCode = data.WeatherCode,
                    TutorialStage = data.TutorialStage <= 0 ? 1 : data.TutorialStage,
                    TutorialCompleted = data.TutorialCompleted,
                    PendingAuctionResult = data.PendingAuctionResult,
                    LastAuctionWeek = data.LastAuctionWeek,
                    AiEventVisibility = data.AiEventMode };
            foreach (var eventKey in data.WeeklyTravelEventClaims ?? [])
                session.WeeklyTravelEventClaims.Add(eventKey);
            foreach (var companyName in data.AuctionResultAcknowledgedBy ?? [])
                session.AuctionResultAcknowledgedBy.Add(companyName);
            var legacyStockScale = data.SharePrices.Count > 0 && data.SharePrices.Values.Max() < 500m &&
                                   (data.SharePriceHistory is null || data.SharePriceHistory.Count == 0 ||
                                    data.SharePriceHistory.Values.All(history => history.Length <= 2));
            if (!legacyStockScale)
            {
                foreach (var sharePrice in data.SharePrices)
                    session.SharePrices[sharePrice.Key] = GameMath.WholeKubars(sharePrice.Value);
                foreach (var history in data.SharePriceHistory ?? [])
                    session.SharePriceHistory[history.Key] = history.Value.Select(GameMath.WholeKubars).ToList();
            }
            foreach (var closure in data.ExchangeClosedThroughWeek ?? [])
                session.ExchangeClosedThroughWeek[closure.Key] = closure.Value;
            foreach (var trend in data.StockTrends ?? [])
                session.StockTrends[trend.Key] = trend.Value;
            foreach (var exchange in data.CrashedExchanges ?? [])
                session.CrashedExchanges.Add(exchange);
            // Saves made before the momentum model recorded only the closure.
            foreach (var closure in session.ExchangeClosedThroughWeek)
                if (session.SharePrices.GetValueOrDefault(closure.Key) <= 0m)
                    session.CrashedExchanges.Add(closure.Key);
            session.LastTurnNews.AddRange(data.LastTurnNews ?? []);
            foreach (var facility in data.Facilities ?? [])
                session.Facilities.Add(new FacilityHolding(facility.Name, facility.Planet, facility.OwnerName, facility.Fee)
                    { Revenue = facility.Revenue });
            if (data.CurrentAuction is not null)
            {
                session.CurrentAuction = new AuctionOffer(data.CurrentAuction.Name, data.CurrentAuction.Planet,
                    data.CurrentAuction.Fee, data.CurrentAuction.Week, data.CurrentAuction.IsShipUpgrade);
                foreach (var bid in data.CurrentAuction.Bids)
                    session.CurrentAuction.Bids[bid.Key] = GameMath.WholeKubars(bid.Value);
            }
            foreach (var savedMarket in data.Markets)
            {
                if (!session.Markets.TryGetValue(savedMarket.Planet, out var market)) continue;
                market.FuelPrice = savedMarket.FuelPrice;
                for (var index = 0; index < Math.Min(market.Listings.Count, savedMarket.Listings.Length); index++)
                {
                    var savedListing = savedMarket.Listings[index];
                    market.RestoreListing(index, savedListing.Supply, savedListing.Price, savedListing.Quantity,
                        savedListing.PublicQuantity, savedListing.AdvertisedQuantity, savedListing.AccessPool);
                }
            }

            foreach (var saved in data.Companies)
            {
                var company = new CompanyState(saved.Name, saved.IsHuman, saved.ShipNumber, saved.Planet,
                    WholeKubars(saved.Cash), saved.ZinnLoan)
                {
                    Bank = WholeKubars(saved.Bank),
                    ShipValue = saved.ShipValue == 0 ? saved.ZinnLoan : saved.ShipValue,
                    Loan = saved.Loan,
                    ZinnRate = saved.ZinnRate,
                    ZinnCreditLimit = saved.ZinnCreditLimit,
                    StandardLoanRate = saved.StandardLoanRate == 0 ? 5m : saved.StandardLoanRate,
                    StandardCreditLimit = saved.StandardCreditLimit == 0 ? 100_000m : saved.StandardCreditLimit,
                    SavingsRate = saved.SavingsRate == 0 ? 1m : saved.SavingsRate,
                    LoanInterest = saved.LoanInterest,
                    ZinnInterest = saved.ZinnInterest,
                    SavingsInterest = saved.SavingsInterest,
                    TicketPrice = saved.TicketPrice,
                    NextTicketPrice = saved.NextTicketPrice ?? saved.TicketPrice,
                    PassengerAdvertising = saved.PassengerAdvertising,
                    CommodityAdvertising = saved.CommodityAdvertising,
                    PreferredPassengerAdvertising = saved.PreferredPassengerAdvertising ?? saved.PassengerAdvertising,
                    PreferredCommodityAdvertising = saved.PreferredCommodityAdvertising ?? saved.CommodityAdvertising,
                    Passengers = saved.Passengers,
                    PassengersPickedUp = saved.PassengersPickedUp,
                    Fuel = saved.Fuel,
                    StockSpentThisWeek = saved.StockSpentThisWeek,
                    GamblingSpentThisWeek = saved.GamblingSpentThisWeek,
                    LastSpecialWeek = saved.LastSpecialWeek,
                    Luck = saved.Luck,
                    IsBankrupt = saved.IsBankrupt,
                    CreditCrisisNoticePending = saved.CreditCrisisNoticePending,
                    BankruptcyAccepted = saved.BankruptcyAccepted,
                    CreditCrisisWeeks = saved.CreditCrisisWeeks,
                    PendingFacilityFees = saved.PendingFacilityFees,
                    PendingFacilityRevenue = saved.PendingFacilityRevenue,
                    PendingExternalHeading = saved.PendingExternalHeading ?? string.Empty,
                    PendingExternalMessage = saved.PendingExternalMessage ?? string.Empty,
                    PendingExternalImage = saved.PendingExternalImage ?? string.Empty,
                    PendingExternalAudio = saved.PendingExternalAudio ?? string.Empty,
                    AdvertisingLightOn = saved.AdvertisingLightOn,
                    LastTravelEventGood = saved.LastTravelEventGood,
                    TaxUnpaidWeeks = saved.TaxUnpaidWeeks,
                    CrewWagesOwed = saved.CrewWagesOwed,
                    TaxesOwed = saved.TaxesOwed,
                    TariffsOwed = saved.TariffsOwed,
                    CrewSalary = saved.CrewSalary,
                    WarehouseCapacity = saved.WarehouseCapacity,
                    InsuranceLevel = saved.InsuranceLevel,
                    InsurancePriceRange = saved.InsurancePriceRange == 0 ? 15 : saved.InsurancePriceRange,
                    InsuranceCost = saved.InsuranceCost,
                    PassengerTaxRate = saved.PassengerTaxRate == 0 ? 15 : saved.PassengerTaxRate,
                    ImportTariffRate = saved.ImportTariffRate == 0 ? 3 : saved.ImportTariffRate,
                    ExportTariffRate = saved.ExportTariffRate == 0 ? 2 : saved.ExportTariffRate,
                    BaseEngineSpeed = saved.BaseEngineSpeed,
                    Turbocharges = saved.Turbocharges,
                    CargoCapacityBonus = saved.CargoCapacityBonus,
                    PassengerCapacityBonus = saved.PassengerCapacityBonus,
                    FuelCapacityBonus = saved.FuelCapacityBonus,
                    ShipTons = saved.ShipTons == 0 ? 400 : saved.ShipTons,
                    StartOfWeekNetWorth = saved.StartOfWeekNetWorth ?? 0m,
                    LastPlanet = string.IsNullOrWhiteSpace(saved.LastPlanet) ? saved.Planet : saved.LastPlanet,
                    TravelTime = saved.TravelTime ?? 0d,
                    TravelDelay = Math.Max(1, saved.TravelDelay ?? 1),
                    TravelTimeMultiplier = Math.Clamp(saved.TravelTimeMultiplier ?? 1d, 0.1d, 10d),
                    AutomatedCrewPositions = Math.Max(0, saved.AutomatedCrewPositions ?? 0),
                    CrewCapacityBonus = Math.Max(0, saved.CrewCapacityBonus ?? 0),
                    CommodityProfitThisWeek = saved.CommodityProfitThisWeek,
                    PlannedDestination = saved.PlannedDestination ?? string.Empty,
                    MarketAccessPlanet = saved.MarketAccessPlanet ?? saved.Planet,
                    MarketCommodityAccessUnits = Math.Max(0, saved.MarketCommodityAccessUnits)
                };
                foreach (var purchase in saved.CommodityPurchasesThisWeek ?? [])
                    company.CommodityPurchasesThisWeek[purchase.Key] = Math.Max(0, purchase.Value);
                if (saved.StartOfWeekNetWorth is null) company.StartOfWeekNetWorth = company.NetWorth;
                if (saved.NetWorthHistory is { Length: > 0 })
                    company.NetWorthHistory.AddRange(saved.NetWorthHistory.Select(GameMath.WholeKubars));
                foreach (var cargo in saved.Cargo)
                    company.Cargo[cargo.Key] = new CargoLot
                        { Quantity = cargo.Value.Quantity, AverageCost = cargo.Value.AverageCost };
                foreach (var warehouse in saved.Warehouses)
                {
                    company.Warehouses[warehouse.Key] = [];
                    foreach (var cargo in warehouse.Value)
                        company.Warehouses[warehouse.Key][cargo.Key] = new CargoLot
                            { Quantity = cargo.Value.Quantity, AverageCost = cargo.Value.AverageCost };
                }
                foreach (var shares in saved.Shares) company.Shares[shares.Key] = shares.Value;
                foreach (var average in saved.ShareAverageCosts ?? [])
                    company.ShareAverageCosts[average.Key] = GameMath.WholeKubars(average.Value);
                foreach (var shortcut in saved.Shortcuts ?? []) company.Shortcuts[shortcut.Key] = shortcut.Value;
                if (saved.PlanetVisitCounts is not null)
                {
                    company.PlanetVisitCounts.Clear();
                    foreach (var visit in saved.PlanetVisitCounts.Where(visit => visit.Value > 0))
                        company.PlanetVisitCounts[visit.Key] = visit.Value;
                }
                foreach (var pair in saved.AiPassengerExperiences ?? [])
                {
                    var savedExperience = pair.Value;
                    company.AiPassengerExperiences[pair.Key] = new AiPassengerExperience
                    {
                        Visits = Math.Max(0, savedExperience.Visits),
                        LastPassengers = Math.Max(0, savedExperience.LastPassengers),
                        LastAdvertising = Math.Clamp(savedExperience.LastAdvertising, 0, 6),
                        LastTicketPrice = Math.Clamp(savedExperience.LastTicketPrice, 100m, 10_000m),
                        LastNetProfit = WholeKubars(savedExperience.LastNetProfit),
                        HasBestResult = savedExperience.HasBestResult,
                        BestPassengers = Math.Max(0, savedExperience.BestPassengers),
                        BestAdvertising = Math.Clamp(savedExperience.BestAdvertising, 0, 6),
                        BestTicketPrice = Math.Clamp(savedExperience.BestTicketPrice, 100m, 10_000m),
                        BestNetProfit = WholeKubars(savedExperience.BestNetProfit)
                    };
                }
                foreach (var pair in saved.AiWarehouseExperiences ?? [])
                {
                    var savedExperience = pair.Value;
                    company.AiWarehouseExperiences[pair.Key] = new AiWarehouseExperience
                    {
                        FireCount = Math.Max(0, savedExperience.FireCount),
                        InsuredFireCount = Math.Clamp(savedExperience.InsuredFireCount, 0,
                            Math.Max(0, savedExperience.FireCount)),
                        ActualLoss = WholeKubars(Math.Max(0m, savedExperience.ActualLoss)),
                        LastFireWeek = Math.Max(0, savedExperience.LastFireWeek)
                    };
                }
                foreach (var notice in saved.PendingTurnNotices ?? [])
                    company.PendingTurnNotices.Add(new TurnNotice(
                        notice.Heading, notice.Message, notice.ImageAsset, notice.AudioAsset,
                        notice.UseCompanyAnnouncement));
                session.Companies.Add(company);
            }
            foreach (var name in data.TurnOrder ?? [])
                if (session.Companies.Any(company => company.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    session.TurnOrder.Add(name);
            session.ActiveTurnIndex = Math.Clamp(data.ActiveTurnIndex, 0,
                Math.Max(0, session.TurnOrder.Count - 1));
            session.InitializeTurnOrder();
            session.InitializeStocks();
            GameplayLogger.Log("LOAD", "SYSTEM",
                $"Loaded seed={session.Seed}; week={session.Week}; companies={session.Companies.Count}");
            return session;
        }
        catch (Exception exception)
        {
            GameplayLogger.Log("LOAD", "SYSTEM", $"Load failed: {exception.GetType().Name}: {exception.Message}");
            return null;
        }
    }

    private static decimal WholeKubars(decimal amount) => GameMath.WholeKubars(amount);

    private sealed record SessionSave(int Difficulty, int Seed, int Week, int ActiveHumanIndex, decimal WinTarget, string[] Planets,
        CompanySave[] Companies, MarketSave[] Markets, Dictionary<string, decimal> SharePrices,
        Dictionary<string, int>? ExchangeClosedThroughWeek,
        string WeatherPlanet, int WeatherCode, FacilitySave[]? Facilities, AuctionSave? CurrentAuction,
        string[]? LastTurnNews, Dictionary<string, decimal[]>? SharePriceHistory,
        string[]? TurnOrder = null, int ActiveTurnIndex = 0,
        int TutorialStage = 1, bool TutorialCompleted = false,
        AuctionResultNotice? PendingAuctionResult = null,
        string[]? AuctionResultAcknowledgedBy = null,
        int LastAuctionWeek = 0,
        Dictionary<string, int>? StockTrends = null,
        string[]? CrashedExchanges = null,
        AiEventVisibility AiEventMode = AiEventVisibility.Default,
        string[]? WeeklyTravelEventClaims = null);
    private sealed record CompanySave(string Name, bool IsHuman, int ShipNumber, string Planet,
        decimal Cash, decimal ShipValue, decimal Bank, decimal Loan, decimal ZinnLoan, decimal ZinnRate, decimal ZinnCreditLimit,
        decimal StandardLoanRate, decimal StandardCreditLimit, decimal SavingsRate,
        decimal LoanInterest,
        decimal ZinnInterest, decimal SavingsInterest, decimal TicketPrice, int PassengerAdvertising,
        int CommodityAdvertising, int? PreferredPassengerAdvertising, int? PreferredCommodityAdvertising,
        int Passengers, bool PassengersPickedUp, decimal Fuel,
        Dictionary<int, CargoSave> Cargo,
        Dictionary<string, Dictionary<int, CargoSave>> Warehouses,
        Dictionary<string, int> Shares, Dictionary<string, bool>? Shortcuts,
        decimal StockSpentThisWeek, decimal GamblingSpentThisWeek,
        int LastSpecialWeek, int Luck, bool IsBankrupt, bool LastTravelEventGood,
        int TaxUnpaidWeeks,
        decimal CrewWagesOwed, decimal TaxesOwed, decimal TariffsOwed, decimal CrewSalary, int WarehouseCapacity,
        int InsuranceLevel, int InsurancePriceRange, decimal InsuranceCost,
        int PassengerTaxRate, int ImportTariffRate, int ExportTariffRate,
        int BaseEngineSpeed, int Turbocharges,
        int CargoCapacityBonus, int PassengerCapacityBonus,
        int FuelCapacityBonus, int ShipTons, decimal? StartOfWeekNetWorth,
        Dictionary<string, decimal>? ShareAverageCosts,
        string? LastPlanet = null, double? TravelTime = null, int? TravelDelay = null,
        int? AutomatedCrewPositions = null, int? CrewCapacityBonus = null,
        decimal CommodityProfitThisWeek = 0m, decimal[]? NetWorthHistory = null,
        string? PlannedDestination = null, double? TravelTimeMultiplier = null,
        decimal? NextTicketPrice = null, bool CreditCrisisNoticePending = false,
        bool BankruptcyAccepted = false, int CreditCrisisWeeks = 0,
        decimal PendingFacilityFees = 0m, decimal PendingFacilityRevenue = 0m,
        string? PendingExternalHeading = null, string? PendingExternalMessage = null,
        string? PendingExternalImage = null, string? PendingExternalAudio = null,
        TurnNoticeSave[]? PendingTurnNotices = null,
        Dictionary<string, int>? PlanetVisitCounts = null,
        bool AdvertisingLightOn = false,
        Dictionary<string, AiPassengerExperienceSave>? AiPassengerExperiences = null,
        string? MarketAccessPlanet = null,
        int MarketCommodityAccessUnits = 0,
        Dictionary<int, int>? CommodityPurchasesThisWeek = null,
        Dictionary<string, AiWarehouseExperienceSave>? AiWarehouseExperiences = null);
    private sealed record AiPassengerExperienceSave(
        int Visits, int LastPassengers, int LastAdvertising,
        decimal LastTicketPrice, decimal LastNetProfit,
        bool HasBestResult, int BestPassengers, int BestAdvertising,
        decimal BestTicketPrice, decimal BestNetProfit);
    private sealed record AiWarehouseExperienceSave(
        int FireCount, int InsuredFireCount, decimal ActualLoss, int LastFireWeek);
    private sealed record TurnNoticeSave(
        string Heading,
        string Message,
        string ImageAsset,
        string AudioAsset = "",
        bool UseCompanyAnnouncement = false);
    private sealed record CargoSave(int Quantity, decimal AverageCost);
    private sealed record MarketSave(string Planet, decimal FuelPrice, ListingSave[] Listings);
    private sealed record ListingSave(int Supply, decimal Price, int Quantity,
        int? PublicQuantity = null, int AdvertisedQuantity = 0, int AccessPool = 0);
    private sealed record FacilitySave(string Name, string Planet, string OwnerName, decimal Fee, decimal Revenue = 0m);
    private sealed record AuctionSave(string Name, string Planet, decimal Fee, int Week, bool IsShipUpgrade,
        Dictionary<string, decimal> Bids);
}
