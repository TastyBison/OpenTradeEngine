using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenTradeEngine;

var planets = new[] { "Xeen", "Pyke", "Zile", "Bass", "Nosh", "Tilo", "Frac" };
var selectedOpponents = AiOpponentCatalog.Select(3, 12345);
Require(selectedOpponents.Count == 3 && selectedOpponents.Select(opponent => opponent.Name).Distinct().Count() == 3 &&
        selectedOpponents.All(opponent => AiOpponentCatalog.All.Contains(opponent)),
    "A reduced AI game did not select a unique random subset of the six original opponents.");
var opponentsSeenAcrossSeeds = Enumerable.Range(1, 100)
    .SelectMany(seed => AiOpponentCatalog.Select(2, seed))
    .Select(opponent => opponent.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
Require(opponentsSeenAcrossSeeds.Count == AiOpponentCatalog.All.Count,
    "Reduced AI games always selected the same leading opponents instead of sampling all six personalities.");
var occupiedStartWeights = planets.ToDictionary(
    planet => planet, planet => planet == "Xeen" ? 1 : 0, StringComparer.OrdinalIgnoreCase);
var startRandom = new Random(24680);
var occupiedStartSelections = Enumerable.Range(0, 1_000)
    .Count(_ => GameSession.PickStartingPlanet(planets, occupiedStartWeights, startRandom) == "Xeen");
Require(occupiedStartSelections is > 0 and < 100,
    "Random starting planets do not substantially reduce, while retaining, the chance of sharing an occupied planet.");
var spreadingOccupancy = planets.ToDictionary(
    planet => planet, _ => 0, StringComparer.OrdinalIgnoreCase);
var spreadRandom = new Random(13579);
var spreadStarts = Enumerable.Range(0, 7).Select(_ =>
{
    var planet = GameSession.PickStartingPlanet(planets, spreadingOccupancy, spreadRandom);
    spreadingOccupancy[planet]++;
    return planet;
}).ToArray();
Require(spreadStarts.Distinct(StringComparer.OrdinalIgnoreCase).Count() >= 5,
    "Starting-planet assignment clustered too many companies despite unused planets being available.");
var tutorialDifficulty = new GameSession(1, planets, 7001);
var noviceDifficulty = new GameSession(2, planets, 7002);
var beginnerDifficulty = new GameSession(3, planets, 7003);
Require(tutorialDifficulty.IsTutorial && tutorialDifficulty.Difficulty == 0 &&
        !noviceDifficulty.IsTutorial && noviceDifficulty.Difficulty == 0 &&
        beginnerDifficulty.Difficulty == 1,
    "Tutorial/Novice no longer share economy difficulty zero as initializeGame specifies.");
var noviceExoticRange = noviceDifficulty.Markets["Xeen"].PriceRange(17);
var beginnerExoticRange = beginnerDifficulty.Markets["Xeen"].PriceRange(17);
var masterRangeGame = new GameSession(6, planets, 7004);
var masterExoticRange = masterRangeGame.Markets["Xeen"].PriceRange(17);
Require(noviceExoticRange == (90m, 720m) && beginnerExoticRange == (180m, 720m) &&
        masterExoticRange == (450m, 720m),
    "Displayed commodity price ranges do not use the selected economy difficulty.");
var tutorialPlayer = new CompanyState("Tutorial Player", true, 6, "Xeen", 50_000m, 100_000m);
tutorialDifficulty.Companies.Add(tutorialPlayer);
Require(tutorialDifficulty.ShouldShowTutorial && tutorialDifficulty.TutorialStage == 1,
    "Tutorial did not begin at stage one.");
tutorialDifficulty.AdvanceWeek();
Require(!tutorialDifficulty.ShouldShowTutorial, "The original turn-two tutorial skip was lost.");
for (var index = 0; index < 7; index++)
{
    tutorialDifficulty.AdvanceWeek();
    _ = tutorialDifficulty.ShouldShowTutorial;
}
Require(tutorialDifficulty.Week == 9 && tutorialDifficulty.TutorialStage == 7 &&
        !tutorialDifficulty.CanAddTutorialFeature,
    "The fixed tutorial introduction sequence did not reach Fuel on turn nine.");
tutorialDifficulty.AdvanceWeek();
Require(tutorialDifficulty.CanAddTutorialFeature && tutorialDifficulty.AddTutorialFeature() &&
        tutorialDifficulty.TutorialStage == 8,
    "A one-player tutorial cannot add post-Fuel features at the player's pace.");
while (tutorialDifficulty.AddTutorialFeature()) { }
Require(tutorialDifficulty.TutorialStage == 17, "Tutorial feature progression did not reach Stock Markets.");
tutorialDifficulty.AdvanceWeek();
Require(tutorialDifficulty.TutorialCompleted && !tutorialDifficulty.ShouldShowTutorial,
    "The tutorial did not end after completing stage seventeen.");

var game = new GameSession(4, planets, 123456);
var expectedHistoryPages = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
{
    ["Vexx"] = 10, ["Pyke"] = 11, ["Mira"] = 8, ["Stye"] = 6, ["Loro"] = 5,
    ["Zile"] = 8, ["Frac"] = 10, ["Tilo"] = 5, ["Queg"] = 4, ["Xeen"] = 2,
    ["Ooom"] = 5, ["Hork"] = 3, ["Bass"] = 3, ["Nosh"] = 3
};
Require(expectedHistoryPages.All(pair => ExploreContentCatalog.HistoryPages(pair.Key).Count == pair.Value),
    "One or more original planetary history pages are missing.");
Require(ExploreContentCatalog.HistoryPages("Vexx")[^1].Contains("statue of him", StringComparison.OrdinalIgnoreCase) &&
        ExploreContentCatalog.HistoryPages("Pyke")[^1].Contains("martial law", StringComparison.OrdinalIgnoreCase) &&
        ExploreContentCatalog.HistoryPages("Nosh")[^1].Contains("trade routes", StringComparison.OrdinalIgnoreCase),
    "Planetary histories are incomplete or out of their original page order.");
var newsCatalogSession = new GameSession(4, planets, 8128);
newsCatalogSession.Companies.Add(new CompanyState("News Test Company", true, 6, "Xeen", 50_000m, 100_000m));
var originalNewsReports = Enumerable.Range(1, 124)
    .Select(report => ExploreContentCatalog.NewsReport(newsCatalogSession, report)).ToArray();
Require(originalNewsReports.All(report => !string.IsNullOrWhiteSpace(report) &&
                                          !report.Contains("<font", StringComparison.OrdinalIgnoreCase) &&
                                          !report.Contains("{", StringComparison.Ordinal)),
    "An original News Center report is empty, still contains Flash markup, or has an unresolved insertion.");
Require(originalNewsReports[0].Contains("Chichi Bobo Rebels", StringComparison.OrdinalIgnoreCase) &&
        originalNewsReports[^1].Contains("Space Pirates", StringComparison.OrdinalIgnoreCase),
    "The original 124-report News Center library is incomplete or out of order.");
var weatherReports = Enumerable.Range(1, 70)
    .Select(code => WeatherCatalog.Forecast(code, "Xeen", 8128, code)).ToArray();
Require(weatherReports.All(report => !string.IsNullOrWhiteSpace(report)) &&
        weatherReports[15].Contains("dry weather", StringComparison.OrdinalIgnoreCase) &&
        weatherReports[59].Contains("accidents", StringComparison.OrdinalIgnoreCase) &&
        weatherReports[60].Contains("meteor storm", StringComparison.OrdinalIgnoreCase),
    "The original seventy-entry Weather Bureau library is incomplete or out of order.");
Require(newsCatalogSession.KukubianDate == "139.00 A.B.",
    "The Kukubian calendar did not begin at 139.00 A.B.");
var globalEventSession = new GameSession(4, planets, 8129);
var globalEventHuman = new CompanyState("Market News Human", true, 6, "Xeen", 500_000m, 100_000m);
globalEventSession.Companies.Add(globalEventHuman);
while (globalEventSession.Week < 5) globalEventSession.AdvanceWeek();
Require(globalEventHuman.PendingTurnNotices.Count >= 1 &&
        globalEventHuman.PendingTurnNotices.All(notice =>
            ((notice.UseCompanyAnnouncement &&
              notice.ImageAsset.StartsWith("PLANET:", StringComparison.Ordinal) &&
              notice.AudioAsset == "EVENT.MP3") ||
             (!notice.UseCompanyAnnouncement &&
              notice.ImageAsset == "TAX1_N.SWF" &&
              notice.AudioAsset == "TAX.MP3")) &&
            !string.IsNullOrWhiteSpace(notice.Heading) &&
            !string.IsNullOrWhiteSpace(notice.Message)),
    "The weekly market-event roll did not route planet and government-rate notices to their correct presentation.");
Require(globalEventSession.KukubianDate == "139.08 A.B.",
    "The Kukubian calendar did not advance by one fiftieth of a year per week.");

var accessSession = new GameSession(4, planets, 8130);
var advertisedBuyer = new CompanyState("Advertised Buyer", true, 6, "Xeen", 1_000_000m, 100_000m)
    { CommodityAdvertising = 6 };
var ordinaryBuyer = new CompanyState("Ordinary Buyer", true, 6, "Xeen", 1_000_000m, 100_000m);
accessSession.Companies.Add(advertisedBuyer);
accessSession.Companies.Add(ordinaryBuyer);
accessSession.AdvanceWeek();
var accessMarket = accessSession.Markets["Xeen"];
var advertisedCommodity = Enumerable.Range(0, CommodityCatalog.All.Length)
    .FirstOrDefault(index => accessMarket.Listings[index].AdvertisedQuantity > 0);
Require(advertisedBuyer.AccessibleCommodityQuantity(accessMarket, advertisedCommodity) >=
        ordinaryBuyer.AccessibleCommodityQuantity(accessMarket, advertisedCommodity),
    "Commodity advertising did not grant company-specific access to the shared planetary stock pool.");
Require(FacilityCatalog.All.Length == 100 && FacilityCatalog.All[^1] == "Automated Repair Droid",
    "The original 100-facility catalog is incomplete.");
var human = new CompanyState("Smoke Test Inc.", true, 6, "Xeen", 500_000m, 130_000m);
var ai = new CompanyState("AI Test Ltd.", false, 8, "Xeen", 500_000m, 130_000m);
Require(human.ShipModel == "Cerebralis" && ai.ShipModel == "Locomotis",
    "Original ship model names are not mapped to their ship numbers.");
Require(OriginalHelpCatalog.ShipDetail(human, "engine").Contains("Pyke", StringComparison.Ordinal) &&
        OriginalHelpCatalog.ShipDetail(human, "fuel").Contains("2 and 16", StringComparison.Ordinal) &&
        OriginalHelpCatalog.Marketplace(human).Contains(human.Planet, StringComparison.Ordinal),
    "Dynamic original help content is incomplete.");
game.Companies.Add(human);
game.Companies.Add(ai);
game.InitializeStocks();
Require(game.SharePrices.Keys.All(planet => planets.Contains(planet)),
    "Stock exchanges must be planetary indices rather than trading-company shares.");
var bassRecommendationCases = new Dictionary<int, string>
{
    [0] = "very strong sell", [20] = "very strong sell",
    [21] = "strong sell", [30] = "strong sell",
    [31] = "sell", [40] = "sell",
    [41] = "hold", [60] = "hold",
    [61] = "buy", [70] = "buy",
    [71] = "strong buy", [80] = "strong buy",
    [81] = "very strong buy", [100] = "very strong buy"
};
foreach (var recommendationCase in bassRecommendationCases)
{
    game.StockTrends["Bass"] = recommendationCase.Key;
    Require(game.StockRecommendation("Bass") == recommendationCase.Value,
        $"Bass recommendation for trend {recommendationCase.Key} did not match the original.");
}
game.StockTrends["Bass"] = 50;
game.ExchangeClosedThroughWeek["Xeen"] = game.Week;
Require(!game.IsExchangeOpen("Xeen") && game.IsExchangeOpen("Pyke"),
    "One-week planetary exchange closure state is incorrect.");
game.ExchangeClosedThroughWeek.Clear();
Require(human.Loan == 0m && human.ZinnLoan == 130_000m, "Starting finance state is incorrect.");
Require(human.NetWorth == 370_000m,
    "Weekly ranking net worth should use cash and savings minus financial debt, excluding the financed ship.");
Require(human.CrewSalary == 1_500m && human.TicketPrice == 1_000m, "Original player defaults are incorrect.");
Require(human.PassengerTaxRate == 15 && human.ImportTariffRate == 3 && human.ExportTariffRate == 2,
    "Original tax and tariff defaults are incorrect.");
var taxRiskBoundary = new CompanyState("Tax Risk Boundary", true, 6, "Xeen", 0m, 0m)
    { ShipTons = 400, TaxesOwed = 13_999m };
Require(taxRiskBoundary.TaxAuditThreshold == 14_000m && !taxRiskBoundary.IsTaxAuditRisk,
    "Taxes below the original 35-times-ship-mass threshold were marked audit-eligible.");
taxRiskBoundary.TaxesOwed = 14_000m;
Require(taxRiskBoundary.IsTaxAuditRisk,
    "The red tax warning and audit eligibility do not meet at the same original threshold.");
Require(human.WarehouseCapacity == 50, "Original warehouse capacity is incorrect.");
var fractionalFuelSession = Enumerable.Range(8100, 1_000)
    .Select(seed => new GameSession(4, new[] { "Xeen" }, seed))
    .First(session => session.Markets["Xeen"].FuelPrice % 10m != 0m);
var fractionalFuelBuyer = new CompanyState("Whole Kubar Fuel", true, 1, "Xeen", 100m, 0m)
    { Fuel = 0m };
var fractionalFuelPrice = fractionalFuelSession.Markets["Xeen"].FuelPrice;
Require(fractionalFuelBuyer.BuyFuel(fractionalFuelSession.Markets["Xeen"], 0.1m).IsSuccessful &&
        fractionalFuelBuyer.Cash == 100m - GameMath.WholeKubars(fractionalFuelPrice * 0.1m) &&
        fractionalFuelBuyer.Cash == decimal.Floor(fractionalFuelBuyer.Cash),
    "Decimal fuel quantities created a hidden fractional-kubar cash remainder.");
var legacyFractionalMoney = new CompanyState("Legacy Fraction", true, 1, "Xeen", 100.6m, 200.5m)
    { Bank = 50.4m, Loan = 75.5m, TaxesOwed = 12.5m, CrewWagesOwed = 10.4m };
Require(legacyFractionalMoney.Cash == 101m && legacyFractionalMoney.Bank == 50m &&
        legacyFractionalMoney.Loan == 76m && legacyFractionalMoney.ZinnLoan == 201m &&
        legacyFractionalMoney.TaxesOwed == 13m && legacyFractionalMoney.CrewWagesOwed == 10m,
    "A monetary balance retained fractional kubars instead of rounding to the nearest whole kubar.");
Require(legacyFractionalMoney.ProjectedLoanAfterInterest == 80m &&
        legacyFractionalMoney.ProjectedZinnLoanAfterInterest == 209m,
    "Projected loan interest did not round to the nearest whole kubar.");
Require(legacyFractionalMoney.DepositToBank(101m).IsSuccessful &&
        legacyFractionalMoney.Cash == 0m && legacyFractionalMoney.Bank == 151m &&
        legacyFractionalMoney.WithdrawFromBank(151m).IsSuccessful &&
        legacyFractionalMoney.Bank == 0m && legacyFractionalMoney.Cash == 151m,
    "Deposit/withdraw all left an invisible legacy fraction in cash or savings.");
Require(CommodityCatalog.All[5].MinimumPrice == 30m && CommodityCatalog.All[5].MaximumPrice == 240m &&
        CommodityCatalog.All[9].MinimumPrice == 50m && CommodityCatalog.All[9].MaximumPrice == 400m &&
        CommodityCatalog.All[13].MinimumPrice == 70m && CommodityCatalog.All[13].MaximumPrice == 560m &&
        CommodityCatalog.All[17].MinimumPrice == 90m && CommodityCatalog.All[17].MaximumPrice == 720m,
    "Original commodity price ranges are incorrect.");
Require(CommodityCatalog.AudioFile(0) == "CANTALOU.MP3" &&
        CommodityCatalog.AudioFile(5) == "BABEL.MP3" &&
        CommodityCatalog.AudioFile(11) == "LAVALAMP.MP3" &&
        CommodityCatalog.AudioFile(17) == "EXOTIC.MP3",
    "The decompiled commodity voice mapping is incorrect.");
Require(game.Markets.Values.All(market => market.Listings.Select((listing, commodity) =>
        listing.Price >= CommodityCatalog.All[commodity].MinimumPrice &&
        listing.Price <= CommodityCatalog.All[commodity].MaximumPrice).All(inRange => inRange)),
    "A generated commodity price fell outside the range shown in the original marketplace.");
var expectedStartingShips = new (int Passengers, int Crew, int Fuel, int Cargo, int Engine)[]
{
    (8, 4, 20, 100, 7), (8, 5, 40, 120, 5), (8, 3, 65, 80, 5),
    (11, 6, 50, 130, 2), (6, 3, 40, 100, 5), (8, 4, 40, 100, 5),
    (7, 4, 30, 80, 7), (5, 4, 40, 110, 6), (10, 3, 40, 90, 4),
    (1, 2, 35, 150, 3), (16, 12, 30, 75, 6), (8, 6, 40, 110, 6)
};
for (var shipNumber = 1; shipNumber <= expectedStartingShips.Length; shipNumber++)
{
    var ship = new CompanyState($"Starting Ship {shipNumber}", true, shipNumber, "Xeen", 0m, 100_000m);
    var expected = expectedStartingShips[shipNumber - 1];
    Require(ship.PassengerCapacity == expected.Passengers && ship.CrewCount == expected.Crew &&
            ship.FuelCapacity == expected.Fuel && ship.CargoCapacity == expected.Cargo &&
            ship.EngineSpeed == expected.Engine,
        $"Starting ship {shipNumber} does not match the decompiled passenger, crew, fuel, cargo or engine values.");
}
var marketStrengthShip = new CompanyState("Market Strength", true, 11, "Xeen", 0m, 100_000m);
Require(marketStrengthShip.CargoCapacity == 75 && marketStrengthShip.MarketStrength == 400,
    "Market strength incorrectly uses the starting ship's cargo capacity.");
marketStrengthShip.CargoCapacityBonus = 500;
Require(marketStrengthShip.MarketStrength == 400,
    "Expanding the cargo hold incorrectly increased market strength.");
marketStrengthShip.ShipTons = 600;
Require(marketStrengthShip.MarketStrength == 600,
    "Increasing total ship size did not increase market strength.");
var globulizer = new CompanyState("Turbo Starter", true, 7, "Xeen", 0m, 130_000m);
Require(globulizer.BaseEngineSpeed == 6 && globulizer.Turbocharges == 1 && globulizer.EngineSpeed == 7 &&
        globulizer.FuelMultiplier == 1m,
    "The Globulizer's factory turbocharged seven-kuarp engine is incorrect.");
var unturboFuelCompany = new CompanyState("Fuel Parity", true, 6, "Xeen", 0m, 0m)
    { Turbocharges = 0 };
var turboFuelCompany = new CompanyState("Fuel Parity", true, 6, "Xeen", 0m, 0m)
    { Turbocharges = 3 };
Require(
    TravelRules.FuelCost("Xeen", "Pyke", unturboFuelCompany, planets, 7) ==
    TravelRules.FuelCost("Xeen", "Pyke", turboFuelCompany, planets, 7),
    "Turbocharging should not change fuel consumption while its balance is undecided.");
Require(TravelRules.TravelTime("Xeen", "Pyke", human, planets) > 0, "Travel time was not calculated.");
var quasoCompany = new CompanyState("Quaso Redirect", true, 6, "Xeen", 0m, 0m) { Luck = 20 };
var quasoResult = TravelEncounterCatalog.QuasoRedirect(quasoCompany, "Pyke").Choice!.Resolve(true);
Require(quasoCompany.Planet == "Pyke" && quasoResult.LuckOverride == 85 &&
        quasoResult.ImageAsset == "QUASO.SWF" && quasoResult.AudioAsset == "BLESSING.MP3",
    "Quaso Mutta did not redirect the destination or apply the original explicit luck value and assets.");
var shipExpansionCompany = new CompanyState("Larger Ship", true, 6, "Xeen", 10_000m, 0m)
    { Bank = 10_000m };
TravelEncounterCatalog.NewShipOffer(shipExpansionCompany, 25_000m).Choice!.Resolve(true);
Require(shipExpansionCompany.Cash == 0m && shipExpansionCompany.Bank == 0m && shipExpansionCompany.Loan == 5_000m &&
        shipExpansionCompany.StandardCreditLimit == 125_000m && shipExpansionCompany.ShipTons == 600 &&
        shipExpansionCompany.CargoCapacity == 150 && shipExpansionCompany.PassengerCapacity == 12 &&
        shipExpansionCompany.FuelCapacity == 55 && shipExpansionCompany.CrewCount == 6 &&
        shipExpansionCompany.InsurancePriceRange == 21,
    "The original 200-ton Cerebralis expansion package or Traders' Union financing is incorrect.");
var expectedShipExpansion = new (int Engine, int Fuel, int Cargo, int Passengers, int Crew, int Insurance)[]
{
    (1, 5, 50, 4, 2, 6), (0, 10, 60, 3, 2, 8), (0, 30, 40, 3, 1, 2),
    (0, 10, 65, 5, 3, 8), (0, 15, 50, 2, 1, 6), (0, 15, 50, 4, 2, 6),
    (1, 10, 40, 3, 2, 6), (0, 15, 55, 3, 2, 6), (0, 15, 45, 5, 2, 4),
    (0, 10, 75, 1, 1, 10), (0, 10, 40, 8, 6, 2), (0, 15, 55, 4, 3, 6)
};
for (var shipNumber = 1; shipNumber <= 12; shipNumber++)
{
    var expanded = new CompanyState($"Expanded Ship {shipNumber}", true, shipNumber, "Xeen", 25_000m, 0m);
    var oldEngine = expanded.BaseEngineSpeed;
    var oldFuel = expanded.FuelCapacity;
    var oldCargo = expanded.CargoCapacity;
    var oldPassengers = expanded.PassengerCapacity;
    var oldCrew = expanded.CrewCount;
    var oldInsurance = expanded.InsurancePriceRange;
    TravelEncounterCatalog.NewShipOffer(expanded, 25_000m).Choice!.Resolve(true);
    var expected = expectedShipExpansion[shipNumber - 1];
    Require(expanded.BaseEngineSpeed == oldEngine + expected.Engine &&
            expanded.FuelCapacity == oldFuel + expected.Fuel && expanded.CargoCapacity == oldCargo + expected.Cargo &&
            expanded.PassengerCapacity == oldPassengers + expected.Passengers &&
            expanded.CrewCount == oldCrew + expected.Crew &&
            expanded.InsurancePriceRange == oldInsurance + expected.Insurance,
        $"Ship {shipNumber}'s decompiled event-2 expansion package is incorrect.");
}
var warehouseExpansionCompany = new CompanyState("Warehouse Expansion", true, 6, "Xeen", 5_000m, 0m)
    { Bank = 5_000m };
TravelEncounterCatalog.WarehouseExpansionOffer(warehouseExpansionCompany, 15_000m).Choice!.Resolve(true);
Require(warehouseExpansionCompany.Cash == 0m && warehouseExpansionCompany.Bank == 0m &&
        warehouseExpansionCompany.Loan == 5_000m && warehouseExpansionCompany.WarehouseCapacity == 100 &&
        warehouseExpansionCompany.InsurancePriceRange == 20 &&
        warehouseExpansionCompany.StandardCreditLimit == 125_000m,
    "The original global warehouse expansion, insurance range and Union financing changes are incorrect.");
var idleWarehouseAi = new CompanyState("Idle Warehouse AI", false, 6, "Xeen", 100_000m, 0m);
Require(!TravelEncounterCatalog.WarehouseExpansionOffer(idleWarehouseAi, 15_000m).Choice!.AiAccepts,
    "AI tried to buy warehouse space despite not using its existing warehouses.");
idleWarehouseAi.Warehouses["Xeen"] = new Dictionary<int, CargoLot>
{
    [0] = new() { Quantity = 40, AverageCost = 100m }
};
Require(TravelEncounterCatalog.WarehouseExpansionOffer(idleWarehouseAi, 15_000m).Choice!.AiAccepts,
    "AI refused warehouse space despite filling at least three quarters of its existing capacity.");
var zinnEncounterCompany = new CompanyState("Zinn Encounter", true, 6, "Xeen", 5_000m, 100_000m);
var zinnEncounter = TravelEncounterCatalog.ZinnLoanExtension(zinnEncounterCompany);
var zinnChoice = zinnEncounter.Choice ?? throw new InvalidOperationException("Mr. Zinn's original loan-extension encounter has no choice.");
zinnChoice.Resolve(true);
Require(zinnEncounterCompany.Cash == 55_000m && zinnEncounterCompany.ZinnLoan == 150_000m &&
        zinnEncounterCompany.ZinnCreditLimit == 300_000m,
    "Accepting Mr. Zinn's 50,000-kubar extension did not update cash, principal and credit limit.");
var exoticEncounterCompany = new CompanyState("Nectum Encounter", true, 6, "Xeen", 10_000m, 100_000m);
var exoticEncounter = TravelEncounterCatalog.ExoticForSale(exoticEncounterCompany);
exoticEncounter.Choice!.Resolve(true);
var exoticIndex = CommodityCatalog.All.Length - 1;
Require(exoticEncounterCompany.Cargo.GetValueOrDefault(exoticIndex)?.Quantity == exoticEncounterCompany.CargoCapacity &&
        exoticEncounterCompany.Cargo[exoticIndex].AverageCost == 100m && exoticEncounterCompany.Cash == 0m,
    "Nectum's 100-kubar Exotic offer did not use real cash, cost basis and cargo capacity.");
var leahyEncounter = TravelEncounterCatalog.CaptainLeahyOffer(exoticEncounterCompany);
leahyEncounter.Choice!.Resolve(true);
Require(exoticEncounterCompany.Cargo.Count == 0 && exoticEncounterCompany.Cash == 20_000m,
    "Captain Leahy did not buy all cargo for twice its purchase cost.");
var slegCompany = new CompanyState("Sleg Trade", true, 7, "Xeen", 10_000m, 0m)
    { BaseEngineSpeed = 6, Turbocharges = 2 };
var slegResult = TravelEncounterCatalog.SlegEngineTrade(slegCompany).Choice!.Resolve(true);
Require(slegResult.IsGood && slegCompany.Cash == 160_000m && slegCompany.BaseEngineSpeed == 3 &&
        slegCompany.Turbocharges == 2 && slegCompany.EngineSpeed == 5,
    "Sleg's fixed 150,000-kubar engine trade did not downgrade the base engine while preserving turbos.");
var royalVisitor = TravelEncounterCatalog.RoyalVisitor(slegCompany);
Require(royalVisitor.Choice is null && royalVisitor.ImageAsset == "DRED.SWF" && royalVisitor.AudioAsset == "DRED.MP3",
    "Emperor Dred's original non-interactive royal visit is not mapped to its installed assets.");
var lowFuelCompany = new CompanyState("Low Fuel", true, 6, "Xeen", 0m, 0m) { ShipTons = 400, Fuel = 14.9m };
Require(lowFuelCompany.LowFuelThreshold == 15m && lowFuelCompany.IsLowOnFuel,
    "A 400-ton ship should show the original low-fuel warning below 15 tons.");
lowFuelCompany.Fuel = 15m;
Require(!lowFuelCompany.IsLowOnFuel,
    "The original low-fuel comparison should not warn at exactly the threshold.");
lowFuelCompany.ShipTons = 600;
Require(lowFuelCompany.LowFuelThreshold == 17m,
    "The low-fuel threshold should rise with total ship mass.");
var isoCompany = new CompanyState("Iso Gift", true, 6, "Xeen", 0m, 0m) { ShipTons = 400 };
TravelEncounterCatalog.IsoGift(isoCompany, 25).Choice!.Resolve(true);
Require(isoCompany.Cash == 10_000m,
    "Iso's gift must use the original 25..75 roll multiplied by total ship mass.");
var raffetyCompany = new CompanyState("Raffety Sale", true, 6, "Xeen", 0m, 0m);
raffetyCompany.Shares["Pyke"] = 10;
raffetyCompany.ShareAverageCosts["Pyke"] = 1_000m;
TravelEncounterCatalog.RaffetyShareOffer(raffetyCompany, "Pyke", 1_200m).Choice!.Resolve(true);
Require(raffetyCompany.Cash == 13_800m && !raffetyCompany.Shares.ContainsKey("Pyke") &&
        !raffetyCompany.ShareAverageCosts.ContainsKey("Pyke"),
    "R.J. Raffety did not purchase the entire holding for 115% of its current market value.");
var nebbitCompany = new CompanyState("Nebbit Purchase", true, 6, "Xeen", 4_000m, 0m) { Bank = 4_000m };
TravelEncounterCatalog.NebbitShareOffer(nebbitCompany, "Pyke", 1_000m).Choice!.Resolve(true);
Require(nebbitCompany.Cash == 2_400m && nebbitCompany.Bank == 4_000m && nebbitCompany.Shares["Pyke"] == 2 &&
        nebbitCompany.ShareAverageCosts["Pyke"] == 800m,
    "Nebbit's 20%-discount stock offer did not use only 25% of liquid funds or preserve its cost basis.");
var rokeScaleNebbit = new CompanyState("Roke-Scale Nebbit", false, 6, "Queg", 1_224_351m, 0m);
var rokeScaleOffer = TravelEncounterCatalog.NebbitShareOffer(
    rokeScaleNebbit, "Pyke", 5_864m).Choice!.Resolve(true);
Require(rokeScaleOffer.IsGood && rokeScaleNebbit.Shares["Pyke"] == 65 &&
        rokeScaleNebbit.Cash == 919_436m,
    "Nebbit incorrectly converted Roke-scale liquid funds into more than the original 25% offer.");
var tatilusCompany = new CompanyState("Tatilus Passengers", true, 6, "Pyke", 0m, 0m)
    { Passengers = 3, PassengersPickedUp = true };
TravelEncounterCatalog.TatilusPassengerOffer(tatilusCompany).Choice!.Resolve(true);
Require(tatilusCompany.Passengers == tatilusCompany.PassengerCapacity && tatilusCompany.Cash == 25_000m,
    "Tatilus did not fill the five empty passenger seats at the original 5,000 kubars per passenger.");
var disembarkGame = new GameSession(4, planets, 8128);
var disembarkCompany = new CompanyState("Passenger Arrival", true, 6, planets[1], 0m, 0m)
{
    LastPlanet = planets[0],
    Passengers = 4,
    PassengersPickedUp = true
};
Require(disembarkCompany.SetNextTicketPrice(4_000m).IsSuccessful &&
        disembarkCompany.TicketPrice == 1_000m && disembarkCompany.NextTicketPrice == 4_000m,
    "The next ticket price could not be changed after passengers had already boarded.");
Require(CompanyState.PassengerFarePenalty(4_000m) == 1m &&
        CompanyState.PassengerFarePenalty(4_500m) == 1.25m &&
        CompanyState.PassengerFarePenalty(5_000m) == 1.5m &&
        CompanyState.PassengerFarePenalty(5_500m) == 1.875m &&
        CompanyState.PassengerFarePenalty(10_000m) == 10m,
    "Passenger willingness does not interpolate continuously through the original fare-band anchors.");
Require(!disembarkCompany.SetNextTicketPrice(10_001m).IsSuccessful &&
        disembarkCompany.NextTicketPrice == 4_000m,
    "Passenger ticket prices were allowed above the new 10,000-kubar maximum.");
disembarkGame.RecordTravelTime(disembarkCompany);
Require(disembarkCompany.Passengers == 0 && !disembarkCompany.PassengersPickedUp &&
        disembarkCompany.TicketPrice == 4_000m,
    "Passengers did not disembark or the next ticket price was not activated on arrival.");
var gurttleCompany = new CompanyState("Gurttle Fuel", true, 6, "Xeen", 0m, 0m) { Fuel = 20m };
TravelEncounterCatalog.GurttleFuelOffer(gurttleCompany).Choice!.Resolve(true);
Require(gurttleCompany.Fuel == 10m && gurttleCompany.Cash == 35_000m,
    "Gurttle did not consume half the fuel at the original 3,500 kubars per ton.");
var lordCompany = new CompanyState("Lord 104 Cargo", true, 6, "Xeen", 0m, 0m);
lordCompany.Cargo[0] = new CargoLot { Quantity = 10, AverageCost = 100m };
TravelEncounterCatalog.Lord104CargoOffer(lordCompany).Choice!.Resolve(true);
Require(lordCompany.Cargo.Count == 0 && lordCompany.Cash == 3_000m,
    "Lord 104 did not buy all cargo for three times its original purchase cost.");
var squowkCompany = new CompanyState("Squowk Swap", true, 6, "Xeen", 0m, 0m);
squowkCompany.Cargo[0] = new CargoLot { Quantity = 10, AverageCost = 100m };
TravelEncounterCatalog.SquowkCargoSwap(squowkCompany, 5).Choice!.Resolve(true);
Require(squowkCompany.Cargo.Count == 1 && squowkCompany.Cargo[5].Quantity == squowkCompany.CargoCapacity &&
        squowkCompany.Cargo[5].AverageCost == 10m,
    "Squowk did not fill the hold with the offered commodity while carrying across the old total cost basis.");
var scooterCompany = new CompanyState("Scooter Sale", true, 6, "Xeen", 0m, 0m);
scooterCompany.Cargo[0] = new CargoLot { Quantity = 10, AverageCost = 100m };
var scooterCaught = TravelEncounterCatalog.ScooterJayOffer(scooterCompany, true).Choice!.Resolve(true);
Require(!scooterCaught.IsGood && scooterCompany.Cargo.Count == 0 && scooterCompany.Cash == 0m &&
        scooterCompany.Loan == 3_000m,
    "Scooter Jay's caught outcome must pay four-times cargo cost, confiscate cargo, then charge the seven-times-cost fine.");
var handsCompany = new CompanyState("Hands Trade", true, 6, "Xeen", 0m, 0m);
handsCompany.Cargo[0] = new CargoLot { Quantity = 10, AverageCost = 100m };
TravelEncounterCatalog.HandsCargoOffer(handsCompany, false).Choice!.Resolve(true);
Require(handsCompany.Cargo.Count == 1 && handsCompany.Cargo[exoticIndex].Quantity == handsCompany.CargoCapacity &&
        handsCompany.Cargo[exoticIndex].AverageCost == 10m,
    "Hands' successful stolen-goods swap did not fill the hold with Exotic and transfer the old cost basis.");
var handsCaughtCompany = new CompanyState("Hands Caught", true, 6, "Xeen", 0m, 0m);
handsCaughtCompany.Cargo[0] = new CargoLot { Quantity = 1, AverageCost = 100m };
TravelEncounterCatalog.HandsCargoOffer(handsCaughtCompany, true).Choice!.Resolve(true);
Require(handsCaughtCompany.Cargo.Count == 0 && handsCaughtCompany.Loan == 50_000m,
    "Hands' police outcome did not confiscate the cargo and apply the original uncovered 50,000-kubar fine.");
var curtonianCompany = new CompanyState("Curtonian Loan", true, 6, "Xeen", 6_000m, 0m);
TravelEncounterCatalog.CurtonianLoan(curtonianCompany, 15, true).Choice!.Resolve(true);
Require(curtonianCompany.Cash == 36_000m,
    "Curtonian's successful outcome did not repay six times the original 15-times-ship-mass loan.");
var quistCompany = new CompanyState("Quist Investment", true, 6, "Xeen", 10_000m, 0m);
var quistOffer = TravelEncounterCatalog.QuistInvestment(quistCompany, 25);
var quistOutcome = quistOffer.Choice!.Resolve(true);
Require(!quistOffer.Choice.AiAccepts && !quistOutcome.IsGood && quistOutcome.SkipOutcomeScreen &&
        quistCompany.Cash == 0m,
    "Quist's investment must consume the money, skip the redundant result screen, and be rejected by player-like AI.");
var wobblerCompany = new CompanyState("Wobbler Hit", true, 6, "Xeen", 6_000m, 0m);
TravelEncounterCatalog.WobblerSponsorship(wobblerCompany, 15, true).Choice!.Resolve(true);
Require(wobblerCompany.Cash == 18_000m,
    "The Wobbler's successful production did not repay three times the original sponsorship.");
var browBuyer = new CompanyState("Brow Buyer", true, 6, "Xeen", 40_000m, 0m);
var browTargetA = new CompanyState("Brow Target A", false, 6, "Pyke", 0m, 0m);
var browTargetB = new CompanyState("Brow Target B", false, 6, "Zile", 0m, 0m) { ShipTons = 500 };
TravelEncounterCatalog.BrowSabotage(browBuyer, new[] { browBuyer, browTargetA, browTargetB },
    1, false, 1234).Choice!.Resolve(true);
var browVictims = new[] { browTargetA, browTargetB }.Where(target => target.Loan > 0m).ToArray();
Require(browBuyer.Cash == 36_000m && browVictims.Length == 1 &&
        (browVictims[0] == browTargetA
            ? browVictims[0].Loan is >= 12_000m and <= 18_000m
            : browVictims[0].Loan is >= 15_000m and <= 22_500m),
    "Brow did not hit exactly one of two opponents with ship-mass-scaled damage.");
Require(browBuyer.PendingTurnNotices.Count == 1 &&
        browBuyer.PendingTurnNotices[0].UseCompanyAnnouncement &&
        browBuyer.PendingTurnNotices[0].Message.Contains(browVictims[0].Name, StringComparison.Ordinal) &&
        !browBuyer.PendingTurnNotices[0].Message.Contains(browBuyer.Name, StringComparison.Ordinal),
    "Brow sabotage did not queue a separate anonymous public damage announcement.");
var underfundedBrowAi = new CompanyState("Underfunded Brow AI", false, 6, "Xeen", 19_999m, 0m);
Require(!TravelEncounterCatalog.BrowSabotage(underfundedBrowAi,
        new[] { underfundedBrowAi, browTargetA }, 10, false, 1).Choice!.AiAccepts,
    "AI accepted Brow without enough liquid funds for the fee and four-fee police fine.");
var browCaughtBuyer = new CompanyState("Brow Caught", true, 6, "Xeen", 20_000m, 0m);
var browUntouchedTarget = new CompanyState("Brow Untouched", false, 6, "Pyke", 0m, 0m);
var browCaught = TravelEncounterCatalog.BrowSabotage(browCaughtBuyer,
    new[] { browCaughtBuyer, browUntouchedTarget }, 10, true, 5678).Choice!.Resolve(true);
Require(!browCaught.IsGood && browCaughtBuyer.Cash == 0m && browCaughtBuyer.Loan == 0m &&
        browUntouchedTarget.Loan == 0m,
    "Brow's police outcome must charge the fee plus four-times-fee fine without damaging competitors.");
var hapaBuyer = new CompanyState("Hapa Buyer", true, 6, "Xeen", 50_000m, 0m);
var hapaTargetA = new CompanyState("Hapa Target A", false, 6, "Pyke", 0m, 0m);
var hapaTargetB = new CompanyState("Hapa Target B", false, 6, "Zile", 0m, 0m) { ShipTons = 500 };
TravelEncounterCatalog.HapaJilloSabotage(hapaBuyer, new[] { hapaBuyer, hapaTargetA, hapaTargetB },
    1, false, 4321).Choice!.Resolve(true);
var hapaVictims = new[] { hapaTargetA, hapaTargetB }.Where(target => target.Loan > 0m).ToArray();
Require(hapaBuyer.Cash == 42_000m && hapaVictims.Length == 1 &&
        (hapaVictims[0] == hapaTargetA
            ? hapaVictims[0].Loan is >= 24_000m and <= 36_000m
            : hapaVictims[0].Loan is >= 30_000m and <= 45_000m),
    "Hapa Jillo sabotage did not hit exactly one of two opponents with ship-scaled damage.");
Require(hapaBuyer.PendingTurnNotices.Count == 1 &&
        hapaBuyer.PendingTurnNotices[0].UseCompanyAnnouncement &&
        hapaBuyer.PendingTurnNotices[0].Message.Contains(hapaVictims[0].Name, StringComparison.Ordinal) &&
        !hapaBuyer.PendingTurnNotices[0].Message.Contains(hapaBuyer.Name, StringComparison.Ordinal),
    "Hapa Jillo sabotage did not queue a separate anonymous public damage announcement.");
var publicSabotageAttacker = new CompanyState("Public Sabotage Attacker", true, 6, "Xeen", 100_000m, 0m);
var publicSabotageObserver = new CompanyState("Public Sabotage Observer", true, 6, "Pyke", 100_000m, 0m);
var publicSabotageAi = new CompanyState("Public Sabotage AI", false, 6, "Zile", 100_000m, 0m);
TravelEncounterCatalog.BrowSabotage(publicSabotageAttacker,
    new[] { publicSabotageAttacker, publicSabotageObserver, publicSabotageAi }, 10, false, 1994)
    .Choice!.Resolve(true);
Require(publicSabotageAttacker.PendingTurnNotices.Count == 1 &&
        publicSabotageObserver.PendingTurnNotices.Count == 1 &&
        publicSabotageAttacker.PendingTurnNotices[0] == publicSabotageObserver.PendingTurnNotices[0] &&
        publicSabotageAttacker.PendingTurnNotices[0].UseCompanyAnnouncement,
    "A sabotage victim's individual damage card was not announced to every active human player.");
var sevenCompanyAttacker = new CompanyState("Seven Company Attacker", true, 6, "Xeen", 500_000m, 0m);
var sevenCompanyTargets = Enumerable.Range(1, 6)
    .Select(index => new CompanyState($"Anonymous Target {index}", false, 6, "Xeen", 0m, 0m))
    .ToArray();
TravelEncounterCatalog.BrowSabotage(sevenCompanyAttacker,
    new[] { sevenCompanyAttacker }.Concat(sevenCompanyTargets).ToArray(), 10, false, 2468)
    .Choice!.Resolve(true);
var sevenCompanyVictimCount = sevenCompanyTargets.Count(target => target.Loan > 0m);
Require(sevenCompanyVictimCount is >= 3 and <= 5 &&
        sevenCompanyAttacker.PendingExternalHeading.Length == 0 &&
        sevenCompanyAttacker.PendingTurnNotices.Count == sevenCompanyVictimCount,
    "Seven-company sabotage did not select 3..5 opponents or selected the attacker.");
var sabotageGateSession = new GameSession(4, new[] { "Xeen", "Pyke" }, 43210);
Require(!sabotageGateSession.SabotageEventsUnlocked,
    "Sabotage events were available during the opening week.");
for (var lockedWeek = 1; lockedWeek < 10; lockedWeek++) sabotageGateSession.AdvanceWeek();
Require(sabotageGateSession.Week == 10 && !sabotageGateSession.SabotageEventsUnlocked,
    "Sabotage events became available before the first ten weeks were complete.");
Require(sabotageGateSession.BrowSabotageRollMaximum == 10 &&
        sabotageGateSession.HapaJilloSabotageRollMaximum == 20,
    "Sabotage rolls did not preserve their original minimums during the opening game.");
sabotageGateSession.AdvanceWeek();
Require(sabotageGateSession.Week == 11 && sabotageGateSession.SabotageEventsUnlocked,
    "Sabotage events did not unlock at week eleven.");
Require(sabotageGateSession.BrowSabotageRollMaximum == 11 &&
        sabotageGateSession.HapaJilloSabotageRollMaximum == 20,
    "Week-eleven sabotage did not preserve original minimums under the progressive upper cap.");
for (var scalingWeek = 11; scalingWeek < 80; scalingWeek++) sabotageGateSession.AdvanceWeek();
Require(sabotageGateSession.Week == 80 &&
        sabotageGateSession.BrowSabotageRollMaximum == 50 &&
        sabotageGateSession.HapaJilloSabotageRollMaximum == 70,
    "Late-game sabotage did not stop at the original Brow and Hapa Jillo maximum rolls.");
var hapaCaughtBuyer = new CompanyState("Hapa Caught", true, 6, "Xeen", 20_000m, 0m);
var hapaUntouchedTarget = new CompanyState("Hapa Untouched", false, 6, "Pyke", 0m, 0m);
var hapaCaught = TravelEncounterCatalog.HapaJilloSabotage(hapaCaughtBuyer,
    new[] { hapaCaughtBuyer, hapaUntouchedTarget }, 20, true, 8765).Choice!.Resolve(true);
Require(!hapaCaught.IsGood && hapaCaught.LuckOverride == 45 && hapaCaughtBuyer.Cash == 0m &&
        hapaCaughtBuyer.Loan == 20_000m && hapaCaughtBuyer.TravelDelay == 2 && hapaUntouchedTarget.Loan == 0m,
    "Hapa Jillo's police outcome must charge fee plus four-times-fee fine, delay travel, set luck to 45, and spare competitors.");
var underfundedHapaAi = new CompanyState("Underfunded Hapa AI", false, 6, "Xeen", 39_999m, 0m);
Require(!TravelEncounterCatalog.HapaJilloSabotage(underfundedHapaAi,
        new[] { underfundedHapaAi, hapaUntouchedTarget }, 20, false, 1).Choice!.AiAccepts,
    "AI accepted Hapa Jillo sabotage without enough liquid funds for its full police exposure.");
var yoyoCompany = new CompanyState("Yoyo Coin", true, 6, "Xeen", 0m, 0m);
var yoyoOffer = TravelEncounterCatalog.YoyoCoinFlip(yoyoCompany, 1);
Require(!yoyoOffer.Choice!.AiAccepts && !yoyoOffer.Choice.Resolve(true).IsGood && yoyoCompany.Loan == 200m,
    "Yoyo's rigged coin did not charge half the advertised prize or teach AI to refuse it.");
var limpusCompany = new CompanyState("Limpus Charity", true, 6, "Xeen", 0m, 0m) { Luck = 20 };
limpusCompany.Cargo[0] = new CargoLot { Quantity = 3, AverageCost = 100m };
var limpusResult = TravelEncounterCatalog.LimpusCharity(limpusCompany).Choice!.Resolve(true);
Require(limpusCompany.Cargo.Count == 0 && limpusResult.LuckOverride == 85,
    "Limpus's blessing did not donate every cargo lot or preserve the original explicit luck value of 85.");
Require(TravelEncounterCatalog.AdviceCount == 20 &&
        Enumerable.Range(0, TravelEncounterCatalog.AdviceCount).All(index =>
            TravelEncounterCatalog.FreeAdvice(index).Message.Contains("Mulls", StringComparison.Ordinal)),
    "Mulls' original free-advice library is incomplete.");
var teeterTurbo = new CompanyState("Teeter Turbo", true, 6, "Xeen", 10_000m, 0m);
var teeterCargo = new CompanyState("Teeter Cargo", true, 6, "Xeen", 10_000m, 0m);
var teeterPassenger = new CompanyState("Teeter Passenger", true, 6, "Xeen", 10_000m, 0m);
var teeterFuel = new CompanyState("Teeter Fuel", true, 6, "Xeen", 10_000m, 0m);
TravelEncounterCatalog.TeeterOffer(teeterTurbo, 1, 10).Choice!.Resolve(true);
TravelEncounterCatalog.TeeterOffer(teeterCargo, 2, 10).Choice!.Resolve(true);
TravelEncounterCatalog.TeeterOffer(teeterPassenger, 3, 10).Choice!.Resolve(true);
TravelEncounterCatalog.TeeterOffer(teeterFuel, 4, 10).Choice!.Resolve(true);
Require(teeterTurbo.Turbocharges == 1 && teeterTurbo.Cash == 9_940m &&
        teeterCargo.CargoCapacityBonus == 10 && teeterCargo.Cash == 9_880m &&
        teeterPassenger.PassengerCapacityBonus == 1 && teeterPassenger.Cash == 9_820m &&
        teeterFuel.FuelCapacityBonus == 5 && teeterFuel.Cash == 9_760m,
    "Teeter's fixed-roll upgrade selection, original cost formula, or ship upgrades are incorrect.");
var meegSuccessCompany = new CompanyState("Meeg Success", true, 6, "Xeen", 5_000m, 0m) { Bank = 5_000m };
var meegSuccess = TravelEncounterCatalog.MeegOffer(meegSuccessCompany, 10_000m, true);
meegSuccess.Choice!.Resolve(true);
Require(meegSuccessCompany.CrewCount == 3 && meegSuccessCompany.AutomatedCrewPositions == 1 &&
        meegSuccessCompany.Cash == 0m && meegSuccessCompany.Bank == 0m,
    "Meeg's successful automation did not remove one paid crew position for the original cost.");
var meegFailureCompany = new CompanyState("Meeg Failure", true, 6, "Xeen", 5_000m, 0m) { Bank = 2_000m };
var meegFailure = TravelEncounterCatalog.MeegOffer(meegFailureCompany, 5_000m, false);
Require(meegFailure.Choice!.AiAccepts,
    "AI should not receive advance knowledge that Meeg's computer work will fail.");
var meegFailureResult = meegFailure.Choice.Resolve(true);
Require(!meegFailureResult.IsGood && meegFailureCompany.Cash == 0m && meegFailureCompany.Bank == 0m &&
        meegFailureCompany.Loan == 3_000m && meegFailureCompany.TravelDelay == 2 && meegFailureCompany.CrewCount == 4,
    "Meeg's failed automation did not charge double, add uncovered debt and delay the trip.");
var callbackCount = 0;
var callbackChoice = TravelEncounterCatalog.TeeterOffer(new CompanyState("Callback", true, 6, "Xeen", 1_000m, 0m), 1, 1).Choice!;
callbackChoice.WhenResolved(_ => callbackCount++);
callbackChoice.Resolve(false);
callbackChoice.Resolve(true);
Require(callbackCount == 1, "Interactive travel outcome finalization ran more than once.");
var spikeCompany = new CompanyState("Spike Test", true, 6, "Xeen", 5_000m, 0m) { Bank = 2_000m, InsuranceLevel = 1 };
var spikeResult = TravelEncounterCatalog.SpikeAdoption(spikeCompany, 15).Choice!.Resolve(true);
Require(!spikeResult.IsGood && spikeCompany.Cash == 0m && spikeCompany.Bank == 1_000m &&
        spikeCompany.Loan == 0m && spikeCompany.InsuranceLevel == 1,
    "Spike's original ship-mass damage or household-pet insurance exclusion is incorrect.");
var heavySpikeCompany = new CompanyState("Heavy Spike Test", true, 6, "Xeen", 0m, 0m) { ShipTons = 500 };
TravelEncounterCatalog.SpikeAdoption(heavySpikeCompany, 50).Choice!.Resolve(true);
Require(heavySpikeCompany.Loan == 25_000m,
    "Spike damage must scale from the original 15..50 roll using total ship mass.");
var nibbleSuccessCompany = new CompanyState("Nibble Success", true, 6, "Xeen", 20_000m, 0m);
TravelEncounterCatalog.NibbleOffer(nibbleSuccessCompany, 15, true).Choice!.Resolve(true);
Require(nibbleSuccessCompany.Cash == 14_000m && nibbleSuccessCompany.CrewSalary == 1_400m,
    "Nibble's successful fee or 100-kubar salary reduction is incorrect.");
var nibbleFailureCompany = new CompanyState("Nibble Failure", true, 6, "Xeen", 5_000m, 0m)
    { Bank = 2_000m, CrewWagesOwed = 4_000m };
var nibbleFailureResult = TravelEncounterCatalog.NibbleOffer(nibbleFailureCompany, 15, false).Choice!.Resolve(true);
Require(!nibbleFailureResult.IsGood && nibbleFailureCompany.Cash == 0m && nibbleFailureCompany.Bank == 0m &&
        nibbleFailureCompany.Loan == 3_000m && nibbleFailureCompany.CrewWagesOwed == 0m &&
        nibbleFailureCompany.CrewSalary == 1_600m,
    "Nibble's failed union busting did not charge the fee and back wages or raise salary by 100 kubars.");
var speevakPaidCompany = new CompanyState("Speevak Paid", true, 6, "Xeen", 0m, 0m);
TravelEncounterCatalog.SpeevakOffer(speevakPaidCompany, 25, false).Choice!.Resolve(true);
Require(speevakPaidCompany.Cash == 10_000m && speevakPaidCompany.TravelDelay == 1,
    "The Speevak's original ship-mass-scaled payment is incorrect.");
var speevakCaughtCompany = new CompanyState("Speevak Caught", true, 6, "Xeen", 0m, 0m)
    { InsuranceLevel = 1 };
var speevakCaught = TravelEncounterCatalog.SpeevakOffer(speevakCaughtCompany, 25, true).Choice!.Resolve(true);
Require(!speevakCaught.IsGood && speevakCaughtCompany.Cash == 0m && speevakCaughtCompany.Loan == 10_000m &&
        speevakCaughtCompany.TravelDelay == 2 && speevakCaughtCompany.InsuranceLevel == 1,
    "The sanitation-police fine must be twice the Speevak payment, delayed, and excluded from insurance.");
var nearFuel = TravelRules.FuelCost("Xeen", "Pyke", human, planets, game.Week);
var farFuel = TravelRules.FuelCost("Xeen", planets[^1], human, planets, game.Week);
Require(nearFuel >= 1m && nearFuel <= 7m && farFuel >= 1m && farFuel <= 7m &&
        TravelRules.MaximumFuelCost(human) == 7m,
    "Fuel usage did not reproduce the original XOR-based distance calculation.");
var timingSession = new GameSession(4, planets, 2222);
var normalTraveller = new CompanyState("Normal Traveller", true, 6, "Pyke", 0m, 0m)
    { LastPlanet = "Xeen", BaseEngineSpeed = 5, TravelDelay = 1 };
var delayedTraveller = new CompanyState("Delayed Traveller", true, 6, "Pyke", 0m, 0m)
    { LastPlanet = "Xeen", BaseEngineSpeed = 5, TravelDelay = 2 };
timingSession.Companies.Add(normalTraveller);
timingSession.Companies.Add(delayedTraveller);
timingSession.RecordTravelTime(normalTraveller);
timingSession.RecordTravelTime(delayedTraveller);
Require(delayedTraveller.TravelTime == normalTraveller.TravelTime * 2d && delayedTraveller.TravelDelay == 1,
    "Travel2 delays must multiply the current distance/engine journey and then reset to one.");
normalTraveller.TravelTime = 12d;
delayedTraveller.TravelTime = 24d;
var timingAi = new CompanyState("Timing AI", false, 6, "Xeen", 500_000m, 0m) { TravelTime = 6d };
timingSession.Companies.Add(timingAi);
timingSession.BuildArrivalOrder();
Require(timingSession.TurnOrder.SequenceEqual(new[] { timingAi.Name, normalTraveller.Name, delayedTraveller.Name }),
    "Humans and AI were not placed in one ascending original-style travel-time order.");

var sharedOrderSession = new GameSession(4, planets, 3333);
var firstHuman = new CompanyState("First Human", true, 6, "Xeen", 500_000m, 0m);
var middleAi = new CompanyState("Middle AI", false, 8, "Xeen", 500_000m, 0m);
var secondHuman = new CompanyState("Second Human", true, 6, "Xeen", 500_000m, 0m);
sharedOrderSession.Companies.Add(firstHuman);
sharedOrderSession.Companies.Add(middleAi);
sharedOrderSession.Companies.Add(secondHuman);
sharedOrderSession.InitializeStocks();
sharedOrderSession.InitializeTurnOrder();
for (var index = 0; index < CommodityCatalog.All.Length; index++)
{
    sharedOrderSession.Markets["Xeen"].Listings[index].Quantity = 100;
    sharedOrderSession.Markets["Xeen"].Listings[index].Price = 100m;
    sharedOrderSession.Markets["Pyke"].Listings[index].Price = 2_000m;
}
var stockBeforeAiTurn = sharedOrderSession.Markets["Xeen"].Listings.Sum(listing => listing.Quantity);
Require(!sharedOrderSession.AdvanceScheduledTurnsAfterHuman() &&
        sharedOrderSession.CurrentTurnCompany == secondHuman && sharedOrderSession.ActiveTurnIndex == 2,
    "The scheduler did not run an intervening AI turn before the next human arrival.");
var stockAfterAiTurn = sharedOrderSession.Markets["Xeen"].Listings.Sum(listing => listing.Quantity);
Require(stockAfterAiTurn < stockBeforeAiTurn,
    "An AI arriving before the next human did not deplete that human's shared commodity pool.");
Require(secondHuman.PendingTurnNotices.Any(notice =>
            notice.Heading.Contains(firstHuman.Name, StringComparison.Ordinal) &&
            notice.Message.Contains('"')) &&
        secondHuman.PendingTurnNotices.Any(notice =>
            notice.Heading.Contains(middleAi.Name, StringComparison.Ordinal) &&
            notice.Message.Contains("buys up", StringComparison.OrdinalIgnoreCase) &&
            notice.ImageAsset.Equals("OP2.PNG", StringComparison.OrdinalIgnoreCase)),
    "Earlier human and AI companies did not queue their original-style arrival taunts for the later human.");

var twoHumanGuardSession = new GameSession(4, planets, 3334);
var guardedFirstHuman = new CompanyState("Guarded First Human", true, 6, "Xeen", 500_000m, 0m)
    { Loan = 20_000m };
var guardedSecondHuman = new CompanyState("Guarded Second Human", true, 7, "Pyke", 500_000m, 0m)
    { Loan = 30_000m };
twoHumanGuardSession.Companies.Add(guardedFirstHuman);
twoHumanGuardSession.Companies.Add(guardedSecondHuman);
twoHumanGuardSession.InitializeTurnOrder();
Require(!twoHumanGuardSession.AdvanceScheduledTurnsAfterHuman(guardedFirstHuman) &&
        twoHumanGuardSession.CurrentTurnCompany == guardedSecondHuman &&
        twoHumanGuardSession.Week == 1,
    "The first human turn did not hand off exactly once to the second human.");
twoHumanGuardSession.AdvanceScheduledTurnsAfterHuman(guardedFirstHuman);
Require(twoHumanGuardSession.CurrentTurnCompany == guardedSecondHuman &&
        twoHumanGuardSession.ActiveTurnIndex == 1 && twoHumanGuardSession.Week == 1,
    "A stale first-player callback advanced the second player's turn.");
Require(twoHumanGuardSession.AdvanceScheduledTurnsAfterHuman(guardedSecondHuman) &&
        twoHumanGuardSession.Week == 2 &&
        guardedFirstHuman.Loan == 21_000m && guardedSecondHuman.Loan == 31_500m,
    "A two-human week did not advance once, or charged weekly loan interest more than once.");
var plannerPlanets = new[] { "Bass", "Pyke", "Zile" };
var plannerSession = new GameSession(4, plannerPlanets, 4545);
plannerSession.InitializeStocks();
foreach (var plannerMarket in plannerSession.Markets.Values)
foreach (var listing in plannerMarket.Listings)
{
    listing.Quantity = 0;
    listing.Price = 100m;
}
plannerSession.Markets["Bass"].Listings[0].Quantity = 1;
plannerSession.Markets["Pyke"].Listings[0].Price = 10_000m;
plannerSession.Markets["Bass"].Listings[1].Quantity = 100;
plannerSession.Markets["Zile"].Listings[1].Price = 500m;
var intermediatePlanner = new CompanyState("Trading Corp. IV", false, 1, "Bass", 200_000m, 0m)
    { Luck = 95, InsuranceCost = 100_000m };
plannerSession.Companies.Add(intermediatePlanner);
plannerSession.RunAiTurn(intermediatePlanner);
Require(intermediatePlanner.Planet == "Zile" &&
        intermediatePlanner.Cargo.GetValueOrDefault(1)?.Quantity == intermediatePlanner.CargoCapacity,
    "Intermediate AI chose the largest per-ton margin instead of the destination with the greatest attainable total cargo profit.");
Require(intermediatePlanner.PassengerAdvertising is >= 0 and <= 6 &&
        intermediatePlanner.CommodityAdvertising is >= 0 and <= 6,
    "Intermediate AI produced an invalid capacity-weighted advertising decision.");
var workingCapitalSession = new GameSession(4, new[] { "Bass", "Pyke" }, 4555);
foreach (var workingMarket in workingCapitalSession.Markets.Values)
foreach (var listing in workingMarket.Listings)
{
    listing.Quantity = 0;
    listing.Price = 100m;
}
workingCapitalSession.Markets["Bass"].Listings[0].Quantity = 100;
workingCapitalSession.Markets["Pyke"].Listings[0].Price = 500m;
var zeroCashAi = new CompanyState("Trading Corp. IV", false, 1, "Bass", 0m, 130_000m)
    { Luck = 95, InsuranceCost = 100_000m };
workingCapitalSession.Companies.Add(zeroCashAi);
workingCapitalSession.RunAiTurn(zeroCashAi);
Require(zeroCashAi.Loan > 0m && zeroCashAi.CargoUsed > 0,
    "An Intermediate AI with financed startup assets did not borrow legal working capital and buy profitable cargo.");
Require(zeroCashAi.Fuel >= 0m && string.IsNullOrWhiteSpace(zeroCashAi.PendingTravelNotice),
    "AI fuel planning triggered an avoidable emergency tanker charge.");

var expensiveFuelSession = Enumerable.Range(4556, 1_000)
    .Select(seed => new GameSession(4, new[] { "Bass", "Pyke" }, seed))
    .First(session => session.Markets["Bass"].FuelPrice > session.Markets["Pyke"].FuelPrice);
foreach (var fuelMarket in expensiveFuelSession.Markets.Values)
foreach (var listing in fuelMarket.Listings)
    listing.Quantity = 0;
var expensiveFuelAi = new CompanyState("Roke Transport", false, 1, "Bass", 100_000m, 0m)
    { Fuel = 0m, InsuranceCost = 100_000m };
expensiveFuelSession.Companies.Add(expensiveFuelAi);
expensiveFuelSession.RunAiTurn(expensiveFuelAi);
Require(expensiveFuelAi.Planet == "Pyke" && expensiveFuelAi.Fuel == 0m &&
        string.IsNullOrWhiteSpace(expensiveFuelAi.PendingTravelNotice),
    "AI did not buy only the exact journey requirement at an expensive fuel stop.");

var cheapFuelSession = Enumerable.Range(5557, 1_000)
    .Select(seed => new GameSession(4, new[] { "Bass", "Pyke" }, seed))
    .First(session => session.Markets["Bass"].FuelPrice < session.Markets["Pyke"].FuelPrice);
foreach (var fuelMarket in cheapFuelSession.Markets.Values)
foreach (var listing in fuelMarket.Listings)
    listing.Quantity = 0;
var cheapFuelAi = new CompanyState("Roke Transport", false, 1, "Bass", 100_000m, 0m)
    { Fuel = 0m, InsuranceCost = 100_000m };
var cheapJourneyFuel = TravelRules.FuelCost("Bass", "Pyke", cheapFuelAi, cheapFuelSession.Planets,
    cheapFuelSession.Week);
cheapFuelSession.Companies.Add(cheapFuelAi);
cheapFuelSession.RunAiTurn(cheapFuelAi);
Require(cheapFuelAi.Planet == "Pyke" &&
        cheapFuelAi.Fuel == cheapFuelAi.FuelCapacity - cheapJourneyFuel,
    "AI did not fill its tank before departing a genuinely cheap fuel stop.");

var delayedExpenseSession = new GameSession(4, new[] { "Bass", "Pyke" }, 4558);
var delayedExpenseAi = new CompanyState("Doll Inc", false, 1, "Bass", 1_000_000m, 0m)
    { Luck = 50 };
var weeklyCrewBill = delayedExpenseAi.CrewCount * delayedExpenseAi.CrewSalary;
delayedExpenseAi.CrewWagesOwed = weeklyCrewBill * 2m;
delayedExpenseAi.TaxesOwed = delayedExpenseAi.ShipTons * 30m;
delayedExpenseSession.Companies.Add(delayedExpenseAi);
delayedExpenseSession.AdvanceWeek();
Require(delayedExpenseAi.CrewWagesOwed == weeklyCrewBill * 3m &&
        delayedExpenseAi.TaxesOwed == delayedExpenseAi.ShipTons * 30m &&
        delayedExpenseAi.Luck == 50,
    "AI paid a non-red expense or unpaid wages altered a nonexistent morale/luck system.");
delayedExpenseAi.CrewWagesOwed = weeklyCrewBill * 3m;
delayedExpenseAi.TaxesOwed = delayedExpenseAi.ShipTons * 35m;
delayedExpenseSession.AdvanceWeek();
Require(delayedExpenseAi.CrewWagesOwed == 0m &&
        delayedExpenseAi.TaxesOwed == 0m && delayedExpenseAi.TariffsOwed == 0m,
    "AI did not pay crew wages and taxes when the displayed amount turned red.");

var cargoRoutingSession = new GameSession(4, new[] { "Bass", "Pyke", "Zile" }, 4565);
foreach (var routingMarket in cargoRoutingSession.Markets.Values)
foreach (var listing in routingMarket.Listings)
{
    listing.Quantity = 0;
    listing.Price = 100m;
}
cargoRoutingSession.Markets["Bass"].Listings[0].Price = 80m;
cargoRoutingSession.Markets["Pyke"].Listings[0].Price = 300m;
cargoRoutingSession.Markets["Zile"].Listings[0].Price = 150m;
var cargoRoutingAi = new CompanyState("Trading Corp. IV", false, 1, "Bass", 100_000m, 0m)
    { Luck = 95, InsuranceCost = 100_000m };
cargoRoutingAi.Cargo[0] = new CargoLot { Quantity = 20, AverageCost = 120m };
cargoRoutingSession.Companies.Add(cargoRoutingAi);
cargoRoutingSession.RunAiTurn(cargoRoutingAi);
Require(cargoRoutingAi.Cargo.GetValueOrDefault(0)?.Quantity == 20 && cargoRoutingAi.Planet == "Pyke",
    "AI sold carried cargo at a loss or failed to route it toward the strongest resale market.");
var replacementSession = new GameSession(4, new[] { "Bass", "Pyke" }, 45651);
foreach (var replacementMarket in replacementSession.Markets.Values)
foreach (var listing in replacementMarket.Listings)
{
    listing.Quantity = 0;
    listing.Price = 100m;
}
replacementSession.Markets["Bass"].Listings[0].Price = 400m;
replacementSession.Markets["Pyke"].Listings[0].Price = 450m;
replacementSession.Markets["Bass"].Listings[10].Price = 1_000m;
replacementSession.Markets["Bass"].Listings[10].Quantity = 200;
replacementSession.Markets["Pyke"].Listings[10].Price = 5_000m;
var replacementAi = new CompanyState("Trading Corp. IV", false, 1, "Bass", 20_000m, 0m)
    { Luck = 95, InsuranceCost = 100_000m };
replacementAi.Cargo[0] = new CargoLot { Quantity = replacementAi.CargoCapacity, AverageCost = 500m };
replacementSession.Companies.Add(replacementAi);
replacementSession.RunAiTurn(replacementAi);
Require(!replacementAi.Cargo.ContainsKey(0) && replacementAi.Cargo.GetValueOrDefault(10)?.Quantity > 0,
    "AI would not accept a controlled cargo loss to replace it with a substantially more profitable exotic trade. " +
    $"Cargo: {string.Join(", ", replacementAi.Cargo.Select(pair => $"{pair.Key}={pair.Value.Quantity}"))}; " +
    $"cash={replacementAi.Cash:N0}, loan={replacementAi.Loan:N0}, planet={replacementAi.Planet}.");

var warehouseStrategySession = new GameSession(4, new[] { "Hork", "Nosh" }, 45652);
foreach (var warehouseMarket in warehouseStrategySession.Markets.Values)
foreach (var listing in warehouseMarket.Listings)
{
    listing.Quantity = 0;
    listing.PublicQuantity = 0;
    listing.Price = listing.Price <= 0m ? 100m : listing.Price;
}
var cheapWarehouseListing = warehouseStrategySession.Markets["Hork"].Listings[17];
cheapWarehouseListing.Price = PlanetMarket.MinimumPrice(17, warehouseStrategySession.Difficulty);
cheapWarehouseListing.Quantity = 100;
cheapWarehouseListing.PublicQuantity = 100;
warehouseStrategySession.Markets["Nosh"].Listings[17].Price = cheapWarehouseListing.Price;
var warehouseStrategyAi = new CompanyState("Trading Corp. IV", false, 1, "Hork", 2_000_000m, 0m)
    { Luck = 50, InsuranceCost = 100_000m };
for (var visit = 0; visit < 5; visit++) warehouseStrategyAi.RecordPlanetVisit("Hork");
warehouseStrategySession.Companies.Add(warehouseStrategyAi);
warehouseStrategySession.RunAiTurn(warehouseStrategyAi);
Require(warehouseStrategyAi.Warehouses.GetValueOrDefault("Hork")?.GetValueOrDefault(17)?.Quantity > 0,
    "A surplus-rich AI did not stockpile a cheap, high-value commodity at a frequently visited planet.");

var fireAverseSession = new GameSession(4, new[] { "Hork", "Nosh" }, 45653);
foreach (var warehouseMarket in fireAverseSession.Markets.Values)
foreach (var listing in warehouseMarket.Listings)
{
    listing.Quantity = 0;
    listing.PublicQuantity = 0;
}
var fireAverseListing = fireAverseSession.Markets["Hork"].Listings[17];
fireAverseListing.Price = PlanetMarket.MinimumPrice(17, fireAverseSession.Difficulty);
fireAverseListing.Quantity = 100;
fireAverseListing.PublicQuantity = 100;
fireAverseSession.Markets["Nosh"].Listings[17].Price = fireAverseListing.Price;
var fireAverseAi = new CompanyState("Trading Corp. IV", false, 1, "Hork", 2_000_000m, 0m)
    { Luck = 50, InsuranceCost = 100_000m };
for (var visit = 0; visit < 5; visit++) fireAverseAi.RecordPlanetVisit("Hork");
fireAverseAi.RecordWarehouseFire("Hork", 100_000m, fireAverseSession.Week, insured: false);
fireAverseSession.Companies.Add(fireAverseAi);
fireAverseSession.RunAiTurn(fireAverseAi);
Require(fireAverseAi.Warehouses.GetValueOrDefault("Hork")?.Values.Sum(lot => lot.Quantity) is null or 0,
    "An AI immediately resumed warehouse speculation at a planet after a major uninsured fire loss.");

var warehouseSaleSession = new GameSession(4, new[] { "Hork", "Nosh" }, 45654);
foreach (var warehouseMarket in warehouseSaleSession.Markets.Values)
foreach (var listing in warehouseMarket.Listings)
{
    listing.Quantity = 0;
    listing.PublicQuantity = 0;
}
warehouseSaleSession.Markets["Hork"].Listings[17].Price = CommodityCatalog.All[17].MaximumPrice;
var warehouseSaleAi = new CompanyState("Trading Corp. IV", false, 1, "Hork", 200_000m, 0m)
    { Luck = 50, InsuranceCost = 100_000m };
warehouseSaleAi.Warehouses["Hork"] = new Dictionary<int, CargoLot>
{
    [17] = new() { Quantity = 10, AverageCost = 100m }
};
warehouseSaleSession.Companies.Add(warehouseSaleAi);
warehouseSaleSession.RunAiTurn(warehouseSaleAi);
Require(!warehouseSaleAi.Warehouses["Hork"].ContainsKey(17) &&
        warehouseSaleSession.Markets["Hork"].Listings[17].Quantity >= 10,
    "AI did not retrieve and sell warehouse cargo after reaching a strong profitable market price.");

var wormPassengerSession = new GameSession(4, new[] { "Bass", "Pyke" }, 4566);
foreach (var passengerMarket in wormPassengerSession.Markets.Values)
foreach (var listing in passengerMarket.Listings) listing.Quantity = 0;
var wormPassengerAi = new CompanyState("Trading Corp. IV", false, 11, "Bass", 100_000m, 0m)
    { Luck = 95, InsuranceCost = 100_000m };
wormPassengerSession.Companies.Add(wormPassengerAi);
wormPassengerSession.RunAiTurn(wormPassengerAi);
Require(wormPassengerAi.PassengerAdvertising == 6 && wormPassengerAi.NextTicketPrice == 4_000m,
    "Passenger-heavy AI did not combine the 10,000-kubar campaign with the 4,000-kubar fare cap.");
var aiSpecialStockSession = new GameSession(4, new[] { "Bass" }, 45661);
var aiSpecialStockCompany = new CompanyState("Trading Corp. IV", false, 1, "Bass", 500_000m, 0m)
    { Luck = 50, InsuranceCost = 100_000m };
aiSpecialStockSession.Companies.Add(aiSpecialStockCompany);
aiSpecialStockSession.InitializeStocks();
aiSpecialStockSession.StockTrends["Bass"] = 70;
aiSpecialStockSession.RunAiTurn(aiSpecialStockCompany);
Require(aiSpecialStockCompany.LastSpecialWeek == aiSpecialStockSession.Week,
    "AI did not use Bass's weekly planet special.");
Require(aiSpecialStockCompany.Shares.GetValueOrDefault("Bass") > 0 &&
        aiSpecialStockSession.LastTurnNews.Any(item => item.Contains("shares on the Bass Exchange", StringComparison.Ordinal)),
    "AI did not make or report an affordable stock-market investment in a bullish market.");
var aiSplashQueued = false;
var aiSplashSeed = 0;
for (var noticeSeed = 1; noticeSeed <= 1_000 && !aiSplashQueued; noticeSeed++)
{
    var noticeSession = new GameSession(4, new[] { "Xeen", "Pyke" }, noticeSeed);
    var noticeHuman = new CompanyState("Notice Human", true, 6, "Xeen", 500_000m, 0m);
    var noticeAi = new CompanyState("Gizzy Shipping", false, 2, "Pyke", 500_000m, 0m);
    noticeSession.Companies.Add(noticeHuman);
    noticeSession.Companies.Add(noticeAi);
    noticeSession.AdvanceWeek();
    noticeSession.ResolveJourneyEvents(noticeAi);
    if (noticeHuman.PendingTurnNotices.Count == 0) continue;
    var notice = noticeHuman.PendingTurnNotices[0];
    aiSplashQueued = notice.ImageAsset == "OP1.PNG" &&
                     (notice.Message.Contains("accepted an offer", StringComparison.OrdinalIgnoreCase) ||
                      notice.Message.Contains("caught by the Imperial Police", StringComparison.OrdinalIgnoreCase)) &&
                     !notice.Message.Contains("kubars", StringComparison.OrdinalIgnoreCase);
    if (aiSplashQueued) aiSplashSeed = noticeSeed;
}
Require(aiSplashQueued,
    "An accepted AI choice did not queue a non-financial start-of-turn splash using that opponent's portrait.");
var fullNoticeQueued = false;
for (var noticeSeed = 1; noticeSeed <= 1_000 && !fullNoticeQueued; noticeSeed++)
{
    var noticeSession = new GameSession(4, new[] { "Xeen", "Pyke" }, noticeSeed)
        { AiEventVisibility = AiEventVisibility.Full };
    var noticeHuman = new CompanyState("Full Notice Human", true, 6, "Xeen", 500_000m, 0m);
    var noticeAi = new CompanyState("Gizzy Shipping", false, 2, "Pyke", 500_000m, 0m);
    noticeSession.Companies.Add(noticeHuman);
    noticeSession.Companies.Add(noticeAi);
    noticeSession.AdvanceWeek();
    noticeSession.ResolveJourneyEvents(noticeAi);
    fullNoticeQueued = noticeHuman.PendingTurnNotices.Any(notice =>
        !notice.UseCompanyAnnouncement && notice.ImageAsset == "OP1.PNG" &&
        notice.Heading.StartsWith("Gizzy Shipping:", StringComparison.Ordinal));
}
Require(fullNoticeQueued, "Full AI event reporting did not queue a resolved AI travel-event report.");
var hiddenNoticeSession = new GameSession(4, new[] { "Xeen", "Pyke" }, aiSplashSeed)
    { AiEventVisibility = AiEventVisibility.None };
var hiddenNoticeHuman = new CompanyState("Hidden Notice Human", true, 6, "Xeen", 500_000m, 0m);
var hiddenNoticeAi = new CompanyState("Gizzy Shipping", false, 2, "Pyke", 500_000m, 0m);
hiddenNoticeSession.Companies.Add(hiddenNoticeHuman);
hiddenNoticeSession.Companies.Add(hiddenNoticeAi);
hiddenNoticeSession.AdvanceWeek();
hiddenNoticeSession.ResolveJourneyEvents(hiddenNoticeAi);
Require(hiddenNoticeHuman.PendingTurnNotices.All(notice => notice.UseCompanyAnnouncement),
    "None AI event reporting did not suppress optional AI travel-event notices.");
var originalStockSession = new GameSession(4, new[] { "Bass" }, 4567);
var originalStockMarket = originalStockSession.Markets["Bass"];
for (var commodityIndex = 0; commodityIndex < originalStockMarket.Listings.Count; commodityIndex++)
{
    var listing = originalStockMarket.Listings[commodityIndex];
    var minimum = PlanetMarket.MinimumPrice(commodityIndex, originalStockSession.Difficulty);
    var expected = PlanetMarket.PriceForSupply(
        commodityIndex, listing.Supply, originalStockSession.Difficulty);
    Require(listing.Supply is >= 0 and <= 100 && listing.Price == expected,
        "Initial planet commodity stock or price does not match the original comR/comP formula.");
}
var originalSupplies = originalStockMarket.Listings.Select(listing => listing.Supply).ToArray();
originalStockMarket.AdvanceWeek(new Random(4568), 2);
Require(originalStockMarket.Listings.Select((listing, index) =>
        Math.Abs(listing.Supply - originalSupplies[index]) <= 20 &&
        listing.Price == PlanetMarket.PriceForSupply(index, listing.Supply, originalStockSession.Difficulty)).All(valid => valid),
    "Weekly planet stock did not follow the original signed 0..20 comR move and comP price formula.");
var balancePlanets = new[] { "Bass", "Pyke", "Zile", "Xeen", "Stye", "Nosh", "Queg" };
var balanceSession = new GameSession(4, balancePlanets, 4575);
for (var aiIndex = 0; aiIndex < AiOpponentCatalog.All.Count; aiIndex++)
{
    var profile = AiOpponentCatalog.All[aiIndex];
    var competitor = new CompanyState(profile.Name, false, aiIndex + 1,
        balancePlanets[aiIndex % balancePlanets.Length], 0m, 130_000m)
        { Luck = 55, InsuranceCost = 25_000m };
    balanceSession.Companies.Add(competitor);
}
balanceSession.InitializeStocks();
for (var balanceWeek = 0; balanceWeek < 10; balanceWeek++)
{
    balanceSession.RunAiTurns();
    balanceSession.ResolveTravelEvents();
    balanceSession.AdvanceWeek();
}
var survivingCompetitors = balanceSession.Companies.Count(company => !company.IsBankrupt);
Require(survivingCompetitors >= 4,
    $"Intermediate AI economy collapsed too quickly: only {survivingCompetitors} of six companies survived ten weeks. " +
    string.Join("; ", balanceSession.Companies.Select(company =>
        $"{company.Name}: bankrupt={company.IsBankrupt}, cash={company.Cash:N0}, loan={company.Loan:N0}, zinn={company.ZinnLoan:N0}, cargo={company.CargoUsed}")));

static (GameSession Session, CompanyState Company) CreateInsurancePlanner(string name, int seed)
{
    var aiPlanets = new[] { "Bass", "Pyke" };
    var session = new GameSession(4, aiPlanets, seed);
    session.InitializeStocks();
    foreach (var insuranceMarket in session.Markets.Values)
    foreach (var listing in insuranceMarket.Listings)
    {
        listing.Quantity = 0;
        listing.Price = 100m;
    }
    session.Markets["Bass"].Listings[0].Quantity = 100;
    session.Markets["Pyke"].Listings[0].Price = 500m;
    var company = new CompanyState(name, false, 1, "Bass", 200_000m, 0m)
        { Luck = 50, InsuranceCost = 12_000m };
    session.Companies.Add(company);
    return (session, company);
}

var cautiousInsurance = CreateInsurancePlanner("Vandergriff Ltd.", 4646);
cautiousInsurance.Session.RunAiTurn(cautiousInsurance.Company);
var riskyInsurance = CreateInsurancePlanner("Roke Transport", 4646);
riskyInsurance.Session.RunAiTurn(riskyInsurance.Company);
Require(cautiousInsurance.Company.InsuranceLevel == 1 && riskyInsurance.Company.InsuranceLevel == 0,
    "Expected-loss insurance did not distinguish the cautious and risk-taking personalities under identical exposure.");
Require(sharedOrderSession.AdvanceScheduledTurnsAfterHuman() && sharedOrderSession.Week == 2 &&
        sharedOrderSession.CurrentTurnCompany is { IsHuman: true },
    "Completing the final scheduled company did not advance one week and stop at the next human arrival.");
var stranded = new CompanyState("Stranded Ltd.", true, 6, "Xeen", 5_000m, 130_000m) { Fuel = 0m };
var rescueFee = stranded.ApplyEmergencyRefuel();
Require(rescueFee == 90_000m && stranded.Fuel == stranded.FuelCapacity && stranded.Loan == 85_000m,
    "Original emergency fuel rescue fee/refill/loan behavior is incorrect.");

var market = game.Markets[human.Planet];
var commodity = market.Listings.FindIndex(listing => listing.Quantity > 3 && listing.Price > 0);
Require(commodity >= 0, "No purchasable commodity generated.");
Require(human.Buy(market, commodity, 3).IsSuccessful, "Commodity purchase failed.");
Require(human.CargoUsed == 3, "Cargo quantity was not updated.");
Require(human.TariffsOwed > 0m, "The original 3% import tariff was not accrued.");
market.Listings[commodity].Price += 10m;
Require(human.Sell(market, commodity, 1).IsSuccessful, "Commodity sale failed.");
Require(human.CommodityProfitThisWeek == 10m, "Realized weekly commodity profit was not recorded.");
var resaleMarket = game.Markets[human.Planet];
var resaleCommodity = (commodity + 1) % CommodityCatalog.All.Length;
var resaleListing = resaleMarket.Listings[resaleCommodity];
resaleListing.Quantity = 20;
resaleListing.PublicQuantity = 10;
resaleListing.AdvertisedQuantity = 10;
resaleListing.AccessPool = 10;
resaleListing.Price = 10m;
var resaleCompany = new CompanyState("Same Planet Reseller", true, 6, human.Planet, 10_000m, 0m)
{
    MarketAccessPlanet = human.Planet,
    MarketCommodityAccessUnits = 10
};
var accessibleBeforeResale = resaleCompany.AccessibleCommodityQuantity(resaleMarket, resaleCommodity);
Require(resaleCompany.Buy(resaleMarket, resaleCommodity, 5).IsSuccessful &&
        resaleCompany.Sell(resaleMarket, resaleCommodity, 5).IsSuccessful &&
        resaleListing.Quantity == 20 &&
        resaleCompany.AccessibleCommodityQuantity(resaleMarket, resaleCommodity) == accessibleBeforeResale,
    "Selling a same-planet purchase back restored shared stock but left its advertised-access allowance consumed.");
var cashBeforeWarehouseStorage = human.Cash;
Require(human.StoreCargo("Xeen", commodity, 2).IsSuccessful, "Warehouse storage failed.");
Require(human.Cash == cashBeforeWarehouseStorage,
    "Using the standard warehouse incorrectly charged a purchase fee.");
Require(human.RetrieveCargo("Xeen", commodity, 1).IsSuccessful, "Warehouse retrieval failed.");
Require(human.SetAdvertising(true, 2).IsSuccessful, "Passenger advertising failed.");
human.GeneratePassengers(new Random(44));
Require(human.SetAdvertising(true, 6).IsSuccessful && human.PassengerAdvertising == 6,
    "The original level-six advertising choice is unavailable.");
var advertisingCheck = new CompanyState("Advertising Check", true, 6, "Xeen", 50_000m, 0m);
Require(advertisingCheck.AdvertisingCost(6) == 10_000m,
    "Baseline level-six advertising should cost 10,000 kubars.");
var campaignCheck = new CompanyState("Campaign Check", true, 6, "Xeen", 10_000m, 0m);
Require(campaignCheck.SetAdvertisingCampaign(2, 3).IsSuccessful && campaignCheck.Cash == 5_000m &&
        campaignCheck.PassengerAdvertising == 2 && campaignCheck.CommodityAdvertising == 3 &&
        campaignCheck.AdvertisingLightOn &&
        campaignCheck.PreferredPassengerAdvertising == 2 && campaignCheck.PreferredCommodityAdvertising == 3,
    "The staged passenger and commodity campaign was not purchased atomically.");
Require(!campaignCheck.SetAdvertisingCampaign(6, 6).IsSuccessful && campaignCheck.Cash == 5_000m &&
        campaignCheck.PassengerAdvertising == 2 && campaignCheck.CommodityAdvertising == 3 &&
        campaignCheck.PreferredPassengerAdvertising == 6 && campaignCheck.PreferredCommodityAdvertising == 6,
    "An unaffordable staged campaign changed paid advertising or failed to remember the shortcut selection.");
var rememberedCampaign = new CompanyState("Remembered Ads", true, 6, "Xeen", 20_000m, 0m);
rememberedCampaign.RememberAdvertisingCampaign(4, 5);
Require(rememberedCampaign.Cash == 20_000m && rememberedCampaign.PassengerAdvertising == 0 &&
        rememberedCampaign.CommodityAdvertising == 0 &&
        rememberedCampaign.PreferredPassengerAdvertising == 4 &&
        rememberedCampaign.PreferredCommodityAdvertising == 5,
    "Choosing advertising shortcut levels charged cash or purchased advertising before confirmation.");
Require(rememberedCampaign.RepeatPreferredAdvertising().IsSuccessful &&
        rememberedCampaign.PassengerAdvertising == 4 && rememberedCampaign.CommodityAdvertising == 5 &&
        rememberedCampaign.AdvertisingLightOn && rememberedCampaign.Cash == 11_000m,
    "The advertising shortcut did not use selections remembered without payment.");
var advertisingArrivalGame = new GameSession(4, planets, 1994);
rememberedCampaign.LastPlanet = "Xeen";
rememberedCampaign.Planet = "Pyke";
advertisingArrivalGame.RecordTravelTime(rememberedCampaign);
Require(!rememberedCampaign.AdvertisingLightOn && rememberedCampaign.PassengerAdvertising == 4 &&
        rememberedCampaign.CommodityAdvertising == 5 &&
        rememberedCampaign.PreferredPassengerAdvertising == 4 && rememberedCampaign.PreferredCommodityAdvertising == 5,
    "Arriving at the advertised planet did not extinguish the lamp or discarded the pending/remembered campaign.");
Require(advertisingCheck.SetAdvertising(false, 6).IsSuccessful &&
        advertisingCheck.Cash == 40_000m && advertisingCheck.CommodityAdvertisingSupply == 200,
    "Advertising spend or its original spend-divided-by-50 supply contribution is incorrect.");
Require(advertisingCheck.SetAdvertising(false, 3).IsSuccessful && advertisingCheck.Cash == 47_000m,
    "Replacing an advertising selection should refund the previous selection before charging the new one.");
advertisingCheck.ShipTons = 600;
Require(advertisingCheck.AdvertisingCost(6) == 15_000m,
    "Advertising prices should scale with ship mass.");
advertisingCheck.ShipTons = 400;
Require(advertisingCheck.SetAdvertising(true, 2).IsSuccessful,
    "A preferred passenger campaign could not be selected.");
advertisingCheck.PassengerAdvertising = 0;
advertisingCheck.CommodityAdvertising = 0;
var cashBeforeRepeat = advertisingCheck.Cash;
Require(advertisingCheck.RepeatPreferredAdvertising().IsSuccessful &&
        advertisingCheck.PassengerAdvertising == 2 && advertisingCheck.CommodityAdvertising == 3 &&
        advertisingCheck.Cash == cashBeforeRepeat - 5_000m,
    "Quick advertising did not repeat the saved passenger and commodity campaign.");
var unadvertisedGame = new GameSession(4, planets, 424242);
var advertisedGame = new GameSession(4, planets, 424242);
unadvertisedGame.Companies.Add(new CompanyState("Pool Check", true, 6, "Xeen", 100_000m, 0m));
advertisedGame.Companies.Add(new CompanyState("Pool Check", true, 6, "Xeen", 100_000m, 0m)
    { CommodityAdvertising = 6 });
unadvertisedGame.AdvanceWeek();
advertisedGame.AdvanceWeek();
Require(advertisedGame.Markets["Xeen"].Listings.Sum(listing => listing.Quantity) >
        unadvertisedGame.Markets["Xeen"].Listings.Sum(listing => listing.Quantity),
    "Commodity advertising did not increase the destination's shared market pool.");
var cashBeforeInsurance = human.Cash;
Require(human.SetInsurance(1).IsSuccessful, "Insurance purchase failed.");
Require(human.InsuranceCoverage == 1m && human.Cash == cashBeforeInsurance - human.InsuranceCost,
    "Next-trip insurance did not charge the quote or provide full coverage.");
Require(human.TurbochargeEngine().IsSuccessful, "Turbocharge failed.");
Require(human.EngineSpeed == 6 && human.Turbocharges == 1 && human.FuelMultiplier == 1m,
    "Turbo state should add speed while remaining separate and carrying no fuel penalty.");
Require(human.ExpandCargoBay(1_000m).IsSuccessful && human.CargoCapacity == 110,
    "Xeen cargo expansion failed.");
Require(human.AddPassengerSeat(1_000m).IsSuccessful && human.PassengerCapacity == 9,
    "Xeen passenger expansion failed.");
Require(human.ExpandFuelTank(1_000m).IsSuccessful && human.FuelCapacity == 45,
    "Xeen fuel expansion failed.");
var savingsFundedSpecial = new CompanyState("Savings Special", true, 1, "Xeen", 100m, 0m)
    { Bank = 1_000m };
Require(savingsFundedSpecial.ExpandCargoBay(750m).IsSuccessful &&
        savingsFundedSpecial.Cash == 0m && savingsFundedSpecial.Bank == 350m &&
        savingsFundedSpecial.CargoCapacity == 110,
    "A planet special did not use cash first and take the remainder from savings.");
var unaffordableSpecial = new CompanyState("Unaffordable Special", true, 1, "Xeen", 100m, 0m)
    { Bank = 200m };
Require(!unaffordableSpecial.AddPassengerSeat(301m).IsSuccessful &&
        unaffordableSpecial.Cash == 100m && unaffordableSpecial.Bank == 200m &&
        unaffordableSpecial.PassengerCapacity == 8,
    "An unaffordable planet special changed the company's money or ship.");
var autoBankCompany = new CompanyState("Auto Bank", true, 1, "Xeen", 275m, 0m)
    { Bank = 50m };
autoBankCompany.Shortcuts["bank"] = true;
Require(autoBankCompany.AutoBankOnDeparture,
    "The Bank shortcut did not enable automatic departure banking.");
autoBankCompany.BankAllCash();
Require(autoBankCompany.Cash == 0m && autoBankCompany.Bank == 325m,
    "Automatic departure banking did not move all cash into savings.");
human.Fuel = 20m;
Require(human.BuyFuelAtPrice(5m, 100m).IsSuccessful && human.Fuel == 25m,
    "Nosh wholesale fuel purchase failed.");
var preGambleCash = human.Cash;
Require(human.Gamble(decimal.Floor(preGambleCash * 0.05m), true).IsSuccessful && human.Cash > preGambleCash,
    "Tilo gambling failed.");
var tiloLimitCompany = new CompanyState("Tilo Limit", true, 1, "Tilo", 100_000m, 0m);
Require(!tiloLimitCompany.Gamble(5_001m, true).IsSuccessful && tiloLimitCompany.Cash == 100_000m &&
        tiloLimitCompany.GamblingSpentThisWeek == 0m,
    "Tilo silently reduced an illegal wager instead of enforcing the original up-to-five-percent choice.");
var tiloDoubleCompany = new CompanyState("Tilo Double", true, 1, "Tilo", 100_000m, 0m);
Require(tiloDoubleCompany.MaximumTiloWager == 5_000m &&
        tiloDoubleCompany.PlaceTiloWager(5_000m).IsSuccessful &&
        tiloDoubleCompany.Cash == 95_000m,
    "Tilo did not deduct a valid initial wager while its result was pending.");
tiloDoubleCompany.CollectTiloPayout(10_000m);
Require(tiloDoubleCompany.Cash == 105_000m,
    "Collecting after the first Tilo win did not return the stake plus equal winnings.");
Require(human.RequestZinnFavor(true).IsSuccessful && human.ZinnRate == 3m,
    "Zile interest-rate favor failed.");
Require(human.RequestZinnFavor(false).IsSuccessful && human.ZinnCreditLimit == 250_000m,
    "Zile credit-limit favor failed.");
var lowerInsuranceResult = human.AdjustInsurancePriceRange(-5);
Require(lowerInsuranceResult.IsSuccessful && human.InsurancePriceRange == 10,
    "Frac insurance-rate review failed.");
Require(lowerInsuranceResult.Message.Contains("5%") &&
        lowerInsuranceResult.Highlights.Any(highlight => highlight.Tone == OutcomeTone.Positive && highlight.Text.Contains("5%")),
    "A favorable Frac premium percentage is not marked as a positive outcome.");
var vexxSession = new GameSession(4, planets, 8181);
var vexxPetitioner = new CompanyState("Vexx Petitioner", true, 6, "Vexx", 0m, 0m)
    { TaxesOwed = 8_000m, TariffsOwed = 2_000m };
var vexxRival = new CompanyState("Vexx Rival", false, 6, "Pyke", 0m, 0m);
vexxSession.Companies.Add(vexxPetitioner);
vexxSession.Companies.Add(vexxRival);
vexxSession.ResolveVexxPetition(vexxPetitioner, 7);
Require(vexxPetitioner.PassengerTaxRate == 14 && vexxRival.PassengerTaxRate == 14,
    "Vexx passenger-tax changes are not system-wide as in the original.");
vexxPetitioner.ImportTariffRate = vexxRival.ImportTariffRate = 1;
vexxSession.ResolveVexxPetition(vexxPetitioner, 8);
Require(vexxPetitioner.ImportTariffRate == 2 && vexxRival.ImportTariffRate == 2,
    "A Vexx petition at the minimum import rate did not backfire for every company.");
vexxSession.ResolveVexxPetition(vexxPetitioner, 10);
Require(vexxPetitioner.TaxesOwed == 0m && vexxPetitioner.TariffsOwed == 0m,
    "Vexx emergency tax relief did not clear the petitioner's accrued balance.");
var rateBeforeFlavourAudience = vexxPetitioner.ExportTariffRate;
vexxSession.ResolveVexxPetition(vexxPetitioner, 1);
Require(vexxPetitioner.ExportTariffRate == rateBeforeFlavourAudience,
    "A flavour-only Vexx audience unexpectedly altered tax rates.");
var pykeSession = Enumerable.Range(1, 500)
    .Select(seed => new GameSession(4, planets, seed))
    .First(candidate => candidate.IsPykeEngineAvailable());
var pykeBuyer = new CompanyState("Pyke Buyer", true, 7, "Pyke", 1_000m, 0m)
    { Bank = 2_000m, Turbocharges = 2 };
pykeSession.Companies.Add(pykeBuyer);
var pykeCost = pykeSession.PykeEngineCost(pykeBuyer);
var pykeFinancesBefore = pykeBuyer.Cash + pykeBuyer.Bank - pykeBuyer.Loan;
Require(pykeSession.ResolvePykeEnginePurchase(pykeBuyer).IsSuccessful &&
        pykeBuyer.BaseEngineSpeed == 7 && pykeBuyer.Turbocharges == 2 && pykeBuyer.EngineSpeed == 9 &&
        pykeFinancesBefore - (pykeBuyer.Cash + pykeBuyer.Bank - pykeBuyer.Loan) == pykeCost,
    "Pyke did not install the next base engine, preserve turbos and use the original quoted-cost financing order.");
var closedPykeSession = Enumerable.Range(1, 500)
    .Select(seed => new GameSession(4, planets, seed))
    .First(candidate => !candidate.IsPykeEngineAvailable());
var closedPykeBuyer = new CompanyState("Closed Pyke Buyer", true, 6, "Pyke", 50_000m, 0m);
closedPykeSession.Companies.Add(closedPykeBuyer);
closedPykeSession.ResolvePykeEnginePurchase(closedPykeBuyer);
Require(closedPykeBuyer.BaseEngineSpeed == 5 && closedPykeBuyer.Cash == 50_000m,
    "Pyke's original one-in-four unavailable week still installed an engine.");
var miraSession = new GameSession(4, planets, 8282);
var miraVisitor = new CompanyState("Mira Visitor", true, 6, "Mira", 0m, 0m) { Luck = 40 };
miraSession.Companies.Add(miraVisitor);
var miraBlessing = miraSession.ResolveMiraBlessing(miraVisitor, 7);
Require(miraVisitor.Luck == 70 && miraVisitor.LastTravelEventGood,
    "Mira's 70% blessing floor was not reproduced.");
Require(miraBlessing.Highlights.Any(highlight => highlight.Tone == OutcomeTone.Positive),
    "Mira's blessing phrase is not marked as a positive outcome.");
miraSession.ResolveMiraBlessing(miraVisitor, 7);
Require(miraVisitor.Luck == 75,
    "Mira did not add five points when a visitor already met a blessing floor.");
var miraCurse = miraSession.ResolveMiraBlessing(miraVisitor, 6);
Require(miraVisitor.Luck == 15 && !miraVisitor.LastTravelEventGood,
    "Mira's original curse did not set the explicit 15% bad-event state.");
Require(miraCurse.Highlights.Any(highlight => highlight.Tone == OutcomeTone.Negative &&
                                             highlight.Text.Contains("curse")),
    "Mira's curse phrase is not marked as a negative outcome.");
miraSession.ResolveMiraBlessing(miraVisitor, 1);
Require(miraVisitor.Luck == 15,
    "A flavour-only Mira audience unexpectedly changed luck.");
var ooomSession = new GameSession(4, planets, 83811);
ooomSession.AdvanceWeek();
ooomSession.AdvanceWeek();
var ooomVisitor = new CompanyState("Ooom Visitor", true, 6, "Ooom", 100_000m, 0m)
    { Luck = 20, ShipTons = 400 };
ooomSession.Companies.Add(ooomVisitor);
var ooomCost = ooomSession.OoomFortuneCost(ooomVisitor);
Require(ooomCost is >= 40m and <= 4_000m,
    "Ooom's original 1..100 journey-roll and ship-mass fee formula is incorrect.");
var ooomCashBefore = ooomVisitor.Cash;
var cursedFortune = ooomSession.ResolveOoomFortune(ooomVisitor, true);
Require(ooomVisitor.Luck == 20 && ooomVisitor.Cash == ooomCashBefore - ooomCost &&
        cursedFortune.Highlights.Any(highlight => highlight.Tone == OutcomeTone.Negative),
    "Ooom changed a negative visitor's luck or failed to report and charge for the existing aura.");
ooomVisitor.Luck = 85;
ooomVisitor.Cash = 100_000m;
var blessedFortune = ooomSession.ResolveOoomFortune(ooomVisitor, true);
Require(ooomVisitor.Luck == 85 && ooomVisitor.Cash == 100_000m - ooomCost + ooomCost * 25m &&
        blessedFortune.Highlights.Any(highlight => highlight.Tone == OutcomeTone.Positive &&
                                                   highlight.Text.Contains("kubars")),
    "Ooom's highest positive reading did not preserve luck and apply its original 25x windfall chance.");
var closedOoomSession = new GameSession(4, planets, 83812);
for (var week = 1; week < 5; week++) closedOoomSession.AdvanceWeek();
var closedOoomVisitor = new CompanyState("Closed Ooom Visitor", true, 6, "Ooom", 100_000m, 0m);
var closedOoomCash = closedOoomVisitor.Cash;
var closedOoomResult = closedOoomSession.ResolveOoomFortune(closedOoomVisitor, true);
Require(closedOoomResult.IsSuccessful && closedOoomVisitor.Cash == closedOoomCash &&
        closedOoomVisitor.Luck == 50,
    "Ooom's every-fifth-week closure charged money or changed luck.");
var loroSession = new GameSession(4, planets, 8382);
var loroVisitor = new CompanyState("Loro Visitor", true, 6, "Loro", 50_000m, 0m)
    { CrewSalary = 1_500m, CrewWagesOwed = 12_000m, Fuel = 5m };
loroSession.Companies.Add(loroVisitor);
var loroSalaryResult = loroSession.ResolveLoroCrewLeave(loroVisitor, 6, 9_000m);
Require(loroVisitor.CrewSalary == 1_400m &&
        loroSalaryResult.Highlights.Any(highlight => highlight.Tone == OutcomeTone.Positive),
    "Loro did not reproduce the crew-salary reduction result.");
var loroWagesResult = loroSession.ResolveLoroCrewLeave(loroVisitor, 8, 9_000m);
Require(loroVisitor.CrewWagesOwed == 0m &&
        loroWagesResult.Highlights.Any(highlight => highlight.Tone == OutcomeTone.Positive),
    "Loro did not reproduce the forgiven-back-wages result.");
loroVisitor.CrewSalary = 500m;
loroSession.ResolveLoroCrewLeave(loroVisitor, 21, 9_000m);
Require(loroVisitor.Fuel == loroVisitor.FuelCapacity,
    "Loro did not reproduce the crew-funded fuel-tank result when salary could not be reduced.");
var specialRulesSession = new GameSession(4,
    new[] { "Zile", "Frac", "Xeen", "Queg", "Hork", "Bass", "Nosh" }, 83821);
var sharedRollVisitor = new CompanyState("Shared Roll Visitor", true, 6, "Nosh", 50_000m, 0m);
var originalCompanyRoll = specialRulesSession.PlanetSpecialRoll(
    sharedRollVisitor, "original company random", 1, 100);
Require(specialRulesSession.OriginalPlanetSpecialNewsData is >= 1 and <= 100 &&
        specialRulesSession.PykeEngineCost(sharedRollVisitor) ==
        originalCompanyRoll * specialRulesSession.OriginalPlanetSpecialNewsData * 6m,
    "Planet specials no longer share the original weekly news roll for availability and prices.");
var zileVisitor = new CompanyState("Zile Visitor", true, 6, "Zile", 0m, 0m)
    { ZinnLoan = 120_000m, ZinnCreditLimit = 200_000m };
specialRulesSession.ResolveZileFavor(zileVisitor, 9);
Require(zileVisitor.ZinnLoan == 80_000m,
    "Zile's original one-third Zinn-debt forgiveness result is incorrect.");
specialRulesSession.ResolveZileFavor(zileVisitor, 19);
Require(zileVisitor.ZinnCreditLimit == 275_000m,
    "Zile's second-band 75,000-kubar credit extension was lost.");
var xeenVisitor = new CompanyState("Xeen Visitor", true, 6, "Xeen", 0m, 0m);
specialRulesSession.ResolveXeenUpgrade(xeenVisitor, 1, 12_000m, 1, 1);
Require(xeenVisitor.CargoCapacityBonus == 0 && xeenVisitor.Loan == 0m,
    "A failed Xeen attempt charged the player or installed the upgrade.");
specialRulesSession.ResolveXeenUpgrade(xeenVisitor, 1, 12_000m, 2, 1);
Require(xeenVisitor.CargoCapacityBonus == 10 && xeenVisitor.Loan == 12_000m,
    "A successful Xeen job did not install the original +10 cargo upgrade with automatic financing.");
var horkVisitor = new CompanyState("Hork Visitor", true, 6, "Hork", 0m, 0m);
specialRulesSession.ResolveHorkPublicity(horkVisitor, 1, 30_000m);
Require(horkVisitor.Cash == 30_000m && horkVisitor.Luck == 50 &&
        horkVisitor.PassengerAdvertising == 0 && horkVisitor.CommodityAdvertising == 0,
    "Hork's original cash-only media outcomes still mutate luck or advertising.");
var horkBeforeBill = horkVisitor.Cash - horkVisitor.Loan;
specialRulesSession.ResolveHorkPublicity(horkVisitor, 13, 40_000m);
Require(horkBeforeBill - (horkVisitor.Cash - horkVisitor.Loan) == 40_000m,
    "Hork's hospital bill did not use the original ship-scaled amount and financing order.");
var fracVisitor = new CompanyState("Frac Visitor", true, 6, "Frac", 0m, 0m)
    { InsurancePriceRange = 15 };
specialRulesSession.ResolveFracInsuranceReview(fracVisitor, 8, 20_000m);
Require(fracVisitor.InsurancePriceRange == 10,
    "Frac's original favorable premium-range result was not applied.");
var quegVisitor = new CompanyState("Queg Visitor", true, 6, "Queg", 0m, 0m);
specialRulesSession.ResolveQuegOffer(quegVisitor, 0, 10, 500m, 1);
Require(quegVisitor.Cargo.GetValueOrDefault(0)?.Quantity == 10 && quegVisitor.Loan == 5_000m,
    "Queg's completed deal did not add its full offered cargo and automatically finance the price.");
var noshVisitor = new CompanyState("Nosh Visitor", true, 6, "Nosh", 50_000m, 0m) { Fuel = 0m };
var noshUnavailableCash = noshVisitor.Cash;
specialRulesSession.ResolveNoshWholesaler(noshVisitor, 10m, 5);
Require(noshVisitor.Fuel == 0m && noshVisitor.Cash == noshUnavailableCash,
    "Nosh ignored the original one-in-five shared-news unavailability result.");
specialRulesSession.ResolveNoshWholesaler(noshVisitor, 10m, 1);
Require(noshVisitor.Fuel == noshVisitor.FuelCapacity &&
        noshVisitor.Cash == noshUnavailableCash - noshVisitor.FuelCapacity * 10m,
    "Nosh's available wholesaler did not fill the tank at the quoted price.");
var styeSession = new GameSession(4, planets, 8383);
var styeMember = new CompanyState("Stye Member", true, 6, "Stye", 0m, 0m)
    { Loan = 120_000m };
styeSession.Companies.Add(styeMember);
styeSession.ResolveStyeAssistance(styeMember, 7);
Require(styeMember.StandardCreditLimit == 150_000m,
    "Stye's original 50,000-kubar credit extension failed.");
styeSession.ResolveStyeAssistance(styeMember, 8);
Require(styeMember.StandardLoanRate == 4m,
    "Stye did not lower an eligible Traders' Union loan rate.");
styeSession.ResolveStyeAssistance(styeMember, 9);
Require(styeMember.SavingsRate == 2m,
    "Stye did not raise a savings rate at or below the original 2% threshold.");
styeSession.ResolveStyeAssistance(styeMember, 10);
Require(styeMember.Loan == 80_000m,
    "Stye's one-third debt-forgiveness result is incorrect.");
styeMember.SavingsRate = 1m;
styeSession.ResolveStyeAssistance(styeMember, 11);
Require(styeMember.SavingsRate == 0m,
    "Stye's backfiring assistance did not lower the minimum savings rate as in the original.");
var styeTermsBeforeNeutral = (styeMember.StandardCreditLimit, styeMember.StandardLoanRate,
    styeMember.SavingsRate, styeMember.Loan);
styeSession.ResolveStyeAssistance(styeMember, 1);
Require(styeTermsBeforeNeutral == (styeMember.StandardCreditLimit, styeMember.StandardLoanRate,
        styeMember.SavingsRate, styeMember.Loan),
    "A flavour-only Stye audience unexpectedly changed financial terms.");
human.Shortcuts["fuel"] = true;
var worthBeforeStock = game.NetWorthOf(human);
Require(human.BuyShares(human.Planet, game.SharePrices[human.Planet], 1).IsSuccessful, "Stock purchase failed.");
Require(human.Shares.ContainsKey(human.Planet) && game.NetWorthOf(human) > worthBeforeStock - human.StockSpentThisWeek,
    "Planetary exchange holdings are not represented in net worth.");
var regulatedStockBuyer = new CompanyState("Regulated Stock Buyer", true, 6, "Xeen", 100_000m, 0m);
Require(regulatedStockBuyer.MaximumStockInvestment == 10_000m &&
        regulatedStockBuyer.MaximumStockPurchaseShares(1_000m) == 10,
    "The original 10,000-kubar-or-1%-of-liquid-assets weekly stock limit is incorrect.");
Require(!regulatedStockBuyer.BuyShares("Xeen", 1_000m, 11).IsSuccessful,
    "A stock purchase above the weekly regulatory limit was accepted.");
Require(regulatedStockBuyer.BuyShares("Xeen", 1_000m, 10).IsSuccessful &&
        regulatedStockBuyer.Shares["Xeen"] == 10 && regulatedStockBuyer.Cash == 89_900m,
    "Stock purchase price or 1% brokerage commission is incorrect.");
Require(!regulatedStockBuyer.BuyShares("Xeen", 1_000m, 1).IsSuccessful,
    "A second stock purchase in the same week was accepted.");
Require(regulatedStockBuyer.SellShares("Xeen", 1_200m, 4).IsSuccessful &&
        regulatedStockBuyer.Shares["Xeen"] == 6 && regulatedStockBuyer.Cash == 94_652m,
    "Partial stock sale or 1% brokerage commission is incorrect.");

game.RunAiTurns();
game.ResolveTravelEvents();
var weeklyFinance = new GameSession(4, planets, 9090);
var financed = new CompanyState("Weekly Finance", true, 6, "Xeen", 0m, 100_000m)
    { Loan = 100_000m, Bank = 100_000m };
weeklyFinance.Companies.Add(financed);
weeklyFinance.AdvanceWeek();
Require(financed.Loan == 105_000m && financed.ZinnLoan == 104_000m && financed.Bank == 101_000m,
    "Per-journey bank, Zinn and savings interest rates are incorrect.");
Require(financed.LoanInterest == 5_000m && financed.ZinnInterest == 4_000m && financed.SavingsInterest == 1_000m,
    "The three finance screens cannot report the interest charged or earned last week.");
var cappedSavingsSession = new GameSession(4, planets, 9091);
var cappedSaver = new CompanyState("Savings Cap", true, 6, "Xeen", 0m, 0m) { Bank = 20_000_000m };
cappedSavingsSession.Companies.Add(cappedSaver);
cappedSavingsSession.AdvanceWeek();
Require(cappedSaver.SavingsInterest == 100_000m && cappedSaver.Bank == 20_100_000m,
    "Weekly bank interest did not respect the original 100,000-kubar maximum.");
var avoidableBankruptcySession = new GameSession(4, planets, 9092);
var avoidableBankruptcy = new CompanyState("Avoidable Bankruptcy", true, 6, "Xeen", 1_000_000m, 0m)
    { Loan = 99_000m };
avoidableBankruptcySession.Companies.Add(avoidableBankruptcy);
Require(!avoidableBankruptcy.WouldExceedUnionCreditLimit &&
        avoidableBankruptcy.RequiredUnionCreditPayment == 0m,
    "Debt below the displayed limit was treated as over-limit before interest was actually posted.");
avoidableBankruptcySession.AdvanceWeek();
Require(!avoidableBankruptcy.IsBankrupt && !avoidableBankruptcy.CreditCrisisNoticePending &&
        avoidableBankruptcy.Loan == 103_950m && avoidableBankruptcy.WouldExceedUnionCreditLimit &&
        avoidableBankruptcy.RequiredUnionCreditPayment == 3_950m,
    "Weekly interest did not leave an over-limit company operating until its next departure choice.");
var crisisPayment = avoidableBankruptcy.PayRequiredCreditBalance();
Require(crisisPayment == 3_950m && avoidableBankruptcy.Loan == 100_000m &&
        !avoidableBankruptcy.WouldExceedAnyCreditLimit &&
        !avoidableBankruptcy.CreditCrisisNoticePending,
    "The minimum over-limit payment did not restore the debt to the displayed credit limit.");
var prudentAi = new CompanyState("Prudent AI", false, 6, "Xeen", 10_000m, 0m) { Loan = 103_950m };
var protectivePayment = prudentAi.ProtectCreditLimitsBeforeTravel();
Require(protectivePayment == 3_950m && prudentAi.Loan == 100_000m && prudentAi.Cash == 6_050m &&
        !prudentAi.WouldExceedUnionCreditLimit,
    "AI credit protection did not repay only the amount currently above the limit.");
var cargoRescueSession = new GameSession(4, new[] { "Xeen", "Pyke" }, 90921);
var cargoRescueAi = new CompanyState("Cargo Rescue AI", false, 6, "Xeen", 0m, 0m)
    { Loan = 105_000m, StandardCreditLimit = 100_000m, InsuranceCost = 100_000m };
cargoRescueSession.Markets["Xeen"].Listings[0].Price = 10_000m;
cargoRescueAi.Cargo[0] = new CargoLot { Quantity = 2, AverageCost = 1_000m };
cargoRescueSession.Companies.Add(cargoRescueAi);
cargoRescueSession.RunAiTurn(cargoRescueAi);
Require(!cargoRescueAi.IsBankrupt && cargoRescueAi.Loan <= cargoRescueAi.StandardCreditLimit,
    "A solvent AI was bankrupted before it could liquidate cargo to cure an over-limit loan.");
var shareRescueSession = new GameSession(4, new[] { "Xeen", "Bass" }, 90922);
shareRescueSession.InitializeStocks();
shareRescueSession.SharePrices["Bass"] = 1_500m;
var shareRescueAi = new CompanyState("Share Rescue AI", false, 6, "Xeen", 0m, 0m)
    { Loan = 105_000m, StandardCreditLimit = 100_000m, InsuranceCost = 100_000m };
shareRescueAi.Shares["Bass"] = 1_000;
shareRescueAi.ShareAverageCosts["Bass"] = 500m;
shareRescueSession.Companies.Add(shareRescueAi);
shareRescueSession.RunAiTurn(shareRescueAi);
Require(!shareRescueAi.IsBankrupt && shareRescueAi.Loan <= shareRescueAi.StandardCreditLimit &&
        shareRescueAi.Shares.GetValueOrDefault("Bass") is > 0 and < 1_000,
    "A high-net-worth AI was bankrupted instead of liquidating enough shares to cure its loan.");
var zinnWarning = new CompanyState("Zinn Warning", true, 6, "Xeen", 0m, 206_960m);
Require(zinnWarning.WouldExceedZinnCreditLimit && zinnWarning.WouldExceedAnyCreditLimit &&
        zinnWarning.RequiredZinnCreditPayment == 6_960m,
    "Mr. Zinn's warning did not use the current balance and displayed credit limit.");
var financeActions = new CompanyState("Finance Actions", true, 6, "Xeen", 30_000m, 20_000m);
Require(financeActions.DepositToBank(10_000m).IsSuccessful && financeActions.Bank == 10_000m && financeActions.Cash == 20_000m,
    "Bank deposits are incorrect.");
Require(financeActions.WithdrawFromBank(4_000m).IsSuccessful && financeActions.Bank == 6_000m && financeActions.Cash == 24_000m,
    "Bank withdrawals are incorrect.");
Require(financeActions.BorrowFromTradersUnion(10_000m).IsSuccessful && financeActions.Loan == 10_000m && financeActions.Cash == 34_000m,
    "Traders' Union borrowing is incorrect.");
Require(financeActions.RepayTradersUnion(5_000m).IsSuccessful && financeActions.Loan == 5_000m && financeActions.Cash == 29_000m,
    "Traders' Union repayment is incorrect.");
var maxBorrower = new CompanyState("Safe Max Borrower", true, 6, "Xeen", 0m, 0m);
Require(maxBorrower.BorrowFromTradersUnion(maxBorrower.StandardCreditLimit).IsSuccessful &&
        maxBorrower.Loan == 100_000m && maxBorrower.Cash == 100_000m &&
        !maxBorrower.WouldExceedUnionCreditLimit,
    "Borrow Max did not reach the original displayed credit limit.");
Require(!maxBorrower.BorrowFromTradersUnion(1m).IsSuccessful &&
        maxBorrower.Loan == 100_000m && maxBorrower.Cash == 100_000m,
    "A borrowing request above available credit was partially accepted.");
var rejectedFinanceAction = new CompanyState("Rejected Finance Action", true, 6, "Xeen", 1_000m, 5_000m)
    { Loan = 5_000m, Bank = 2_000m };
Require(!rejectedFinanceAction.RepayTradersUnion(2_000m).IsSuccessful &&
        rejectedFinanceAction.Cash == 1_000m && rejectedFinanceAction.Loan == 5_000m &&
        !rejectedFinanceAction.RepayZinn(2_000m).IsSuccessful &&
        rejectedFinanceAction.ZinnLoan == 5_000m &&
        !rejectedFinanceAction.DepositToBank(2_000m).IsSuccessful &&
        !rejectedFinanceAction.WithdrawFromBank(3_000m).IsSuccessful,
    "An unaffordable finance request was silently reduced instead of being refused.");
var exactLimitSession = new GameSession(4, planets, 9093);
var exactLimitCompany = new CompanyState("Exact Limit", true, 6, "Xeen", 0m, 0m)
    { Loan = 100_000m, StandardLoanRate = 0m };
exactLimitSession.Companies.Add(exactLimitCompany);
exactLimitSession.AdvanceWeek();
Require(!exactLimitCompany.IsBankrupt && exactLimitCompany.Loan == exactLimitCompany.StandardCreditLimit,
    "Debt equal to the credit limit was treated as bankruptcy; only debt above the limit is illegal.");
Require(financeActions.RepayZinn(5_000m).IsSuccessful && financeActions.ZinnLoan == 15_000m && financeActions.Cash == 24_000m,
    "Mr. Zinn repayment is incorrect.");
Require(human.InsuranceLevel == 0, "Insurance was not consumed after the next trip.");
var eventHeadings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
var illustratedEvents = 0;
for (var eventSeed = 1; eventSeed <= 250; eventSeed++)
{
    var eventGame = new GameSession(4, planets, eventSeed);
    eventGame.AdvanceWeek();
    var eventCompany = new CompanyState($"Event Check {eventSeed}", false, 6, "Xeen", 100_000m, 100_000m)
    {
        Luck = 95,
        Loan = 50_000m,
        CrewWagesOwed = 10_000m,
        TaxesOwed = 8_000m,
        TariffsOwed = 2_000m
    };
    eventCompany.AddFreeCargo(0, 10);
    eventGame.Companies.Add(eventCompany);
    var eventResult = eventGame.ResolveTravelEvent(eventCompany);
    eventHeadings.Add(eventResult.Heading);
    if (!string.IsNullOrWhiteSpace(eventResult.ImageAsset)) illustratedEvents++;
}
Require(eventHeadings.Contains("No Need To Pay!") && eventHeadings.Contains("Emergency Tax Break") &&
        eventHeadings.Contains("Super Deal") && eventHeadings.Contains("Free Warehouse Space"),
    "The expanded original automatic good-event family is not reachable.");
Require(illustratedEvents > 0, "Travel events are not carrying their original static-art references.");
IReadOnlyList<TravelEventResult>? stackedJourneyEvents = null;
CompanyState? stackedJourneyCompany = null;
for (var eventSeed = 1; eventSeed <= 500 && stackedJourneyEvents is null; eventSeed++)
{
    var stackedGame = new GameSession(4, planets, eventSeed);
    var stackedCompany = new CompanyState("Stacked Journey", true, 6, "Xeen", 0m, 0m)
    {
        CrewSalary = 1_000m,
        TaxesOwed = 14_000m,
        Fuel = 0m
    };
    stackedCompany.CrewWagesOwed = stackedCompany.CrewCount * stackedCompany.CrewSalary * 5m;
    stackedCompany.ApplyEmergencyRefuel();
    stackedGame.Companies.Add(stackedCompany);
    var events = stackedGame.ResolveJourneyEvents(stackedCompany);
    var headings = events.Select(result => result.Heading).ToHashSet(StringComparer.OrdinalIgnoreCase);
    if (headings.Contains("Union Boss") && headings.Contains("Out of Fuel") &&
        headings.Contains("Travel Delayed") && headings.Contains("Tax Audit"))
    {
        stackedJourneyEvents = events;
        stackedJourneyCompany = stackedCompany;
    }
}
Require(stackedJourneyEvents is { Count: >= 5 } && stackedJourneyCompany is not null,
    "A departure could not produce the primary encounter followed by crew, fuel, delay, and tax events.");
Require(stackedJourneyCompany!.CrewWagesOwed == 0m && stackedJourneyCompany.TaxesOwed == 0m &&
        stackedJourneyCompany.TariffsOwed == 0m && stackedJourneyCompany.TravelDelay == 2,
    "The stacked post-departure events did not apply their original consequences exactly once.");
var foundQuietOrdinaryChain = false;
var foundMultipleGoodEvents = false;
var foundQuasoThenOrdinaryEvent = false;
for (var eventSeed = 1; eventSeed <= 2_000 &&
     (!foundQuietOrdinaryChain || !foundMultipleGoodEvents || !foundQuasoThenOrdinaryEvent); eventSeed++)
{
    var chainGame = new GameSession(4, planets, 20_000 + eventSeed);
    chainGame.AdvanceWeek();
    chainGame.InitializeStocks();
    var chainCompany = new CompanyState($"Good Chain {eventSeed}", true, 11, planets[0], 5_000_000m, 100_000m)
    {
        Luck = 95,
        Bank = 500_000m,
        Loan = 50_000m,
        CrewSalary = 2_000m,
        BaseEngineSpeed = 6,
        Passengers = 1
    };
    chainCompany.AddFreeCargo(0, 10);
    foreach (var exchange in planets) chainCompany.Shares[exchange] = 10;
    chainGame.Companies.Add(chainCompany);
    chainGame.Companies.Add(new CompanyState("Chain Rival", false, 2, planets[1], 1_000_000m, 0m));
    var events = chainGame.ResolveJourneyEvents(chainCompany);
    var quasoIndex = events.ToList().FindIndex(result =>
        result.ImageAsset.Equals("QUASO.SWF", StringComparison.OrdinalIgnoreCase));
    foundQuietOrdinaryChain |= events.Count == 0;
    foundMultipleGoodEvents |= events.Count(result =>
        !result.ImageAsset.Equals("QUASO.SWF", StringComparison.OrdinalIgnoreCase) &&
        !result.Heading.Equals("Union Boss", StringComparison.OrdinalIgnoreCase) &&
        !result.Heading.Equals("Out of Fuel", StringComparison.OrdinalIgnoreCase) &&
        !result.Heading.Equals("Tax Audit", StringComparison.OrdinalIgnoreCase)) >= 2;
    foundQuasoThenOrdinaryEvent |= quasoIndex >= 0 && events.Skip(quasoIndex + 1).Any();
}
Require(foundQuietOrdinaryChain,
    "The original sequential event checks could not produce a journey with no ordinary event.");
Require(foundMultipleGoodEvents,
    "The good-event table stopped after its first successful ordinary event.");
Require(foundQuasoThenOrdinaryEvent,
    "Quaso incorrectly replaced the ordinary event chain instead of preceding it.");

var foundMultipleBadEvents = false;
for (var eventSeed = 1; eventSeed <= 2_000 && !foundMultipleBadEvents; eventSeed++)
{
    var chainGame = new GameSession(4, planets, 30_000 + eventSeed);
    chainGame.AdvanceWeek();
    var chainCompany = new CompanyState($"Bad Chain {eventSeed}", true, 6, planets[0], 5_000_000m, 100_000m)
    {
        Luck = 15,
        Bank = 500_000m
    };
    chainCompany.AddFreeCargo(0, 10);
    chainGame.Companies.Add(chainCompany);
    foundMultipleBadEvents = chainGame.ResolveJourneyEvents(chainCompany).Count(result =>
        !result.ImageAsset.Equals("QUASO.SWF", StringComparison.OrdinalIgnoreCase) &&
        !result.Heading.Equals("Union Boss", StringComparison.OrdinalIgnoreCase) &&
        !result.Heading.Equals("Out of Fuel", StringComparison.OrdinalIgnoreCase) &&
        !result.Heading.Equals("Travel Delayed", StringComparison.OrdinalIgnoreCase) &&
        !result.Heading.Equals("Tax Audit", StringComparison.OrdinalIgnoreCase)) >= 2;
}
Require(foundMultipleBadEvents,
    "The bad-event table stopped after its first successful ordinary event.");
var debugHeadings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
for (var debugEvent = 1; debugEvent <= GameSession.DebugTravelEventCount; debugEvent++)
{
    var debugGame = new GameSession(6, planets, 9_000 + debugEvent);
    debugGame.InitializeStocks();
    var debugCompany = new CompanyState("Event Debug Company", true, 11, planets[0], 5_000_000m, 100_000m)
    {
        Bank = 250_000m,
        Loan = 75_000m,
        CrewWagesOwed = 25_000m,
        TaxesOwed = 15_000m,
        TariffsOwed = 10_000m,
        CrewSalary = 2_000m,
        BaseEngineSpeed = 6,
        Passengers = 1
    };
    debugCompany.AddFreeCargo(0, 10);
    foreach (var exchange in planets) debugCompany.Shares[exchange] = 10;
    debugGame.Companies.Add(debugCompany);
    debugGame.Companies.Add(new CompanyState("Debug Rival", false, 2, planets[0], 1_000_000m, 0m));
    var debugResult = debugGame.ResolveDebugTravelEvent(debugCompany, debugEvent);
    debugHeadings.Add(debugResult.Heading);
    Require(!string.IsNullOrWhiteSpace(debugResult.Heading) &&
            !debugResult.Heading.Equals("A Quiet Journey", StringComparison.OrdinalIgnoreCase),
        $"Debug event {debugEvent} did not resolve its forced event slot.");
}
Require(debugHeadings.Contains("Donation Request") && debugHeadings.Contains("Warehouse Fire") &&
        debugHeadings.Contains("Demanding Union") && debugHeadings.Contains("Wrong Destination") &&
        debugHeadings.Contains("Travel Delayed"),
    "The original bad-event families are not all reachable through the debugger.");
Require(GameSession.DebugTravelEventCount == 64,
    "The debugger must expose Quaso, all 46 original good-event slots, and the authentic bad-event slots.");
var donationGame = new GameSession(6, planets, 9048);
var donationCompany = new CompanyState("Royal Donation", true, 11, planets[0], 1_000_000m, 0m)
    { ShipTons = 100 };
donationGame.Companies.Add(donationCompany);
var donationOffer = donationGame.ResolveDebugTravelEvent(donationCompany, 48);
var donationBefore = donationCompany.Cash + donationCompany.Bank - donationCompany.Loan;
var donationAccepted = donationOffer.Choice!.Resolve(true);
var donationPaid = donationBefore - (donationCompany.Cash + donationCompany.Bank - donationCompany.Loan);
Require(donationOffer.Choice.AcceptLabel == "Yes" && donationOffer.Choice.DeclineLabel == "No" &&
        donationPaid is >= 1_500m and <= 5_000m && donationPaid % donationCompany.ShipTons == 0m &&
        donationAccepted.SkipOutcomeScreen,
    "The royal donation must use the original Yes/No flow and a 15..50 roll multiplied by ship tons.");
var indebtedDonationCompany = new CompanyState("Indebted Donation", true, 11, planets[0], 0m, 0m)
    { ShipTons = 100 };
var indebtedDonationGame = new GameSession(6, planets, 9048);
indebtedDonationGame.Companies.Add(indebtedDonationCompany);
var indebtedDonationOffer = indebtedDonationGame.ResolveDebugTravelEvent(indebtedDonationCompany, 48);
Require(indebtedDonationOffer.Message.Contains("Traders' Union steps in and loans you the difference",
            StringComparison.Ordinal) &&
        indebtedDonationOffer.Choice!.Resolve(true).SkipOutcomeScreen && indebtedDonationCompany.Loan is >= 1_500m and <= 5_000m,
    "An unaffordable royal donation must explain and use the automatic Traders' Union loan.");
var refusedDonationCompany = new CompanyState("Refused Donation", true, 11, planets[0], 1_000_000m, 0m)
    { ShipTons = 100, TaxesOwed = 250m };
var refusedDonationGame = new GameSession(6, planets, 9048);
refusedDonationGame.Companies.Add(refusedDonationCompany);
var refusedDonationOffer = refusedDonationGame.ResolveDebugTravelEvent(refusedDonationCompany, 48);
var specialTax = refusedDonationOffer.Choice!.Resolve(false);
Require(specialTax.Heading == "Special Tax" && specialTax.ImageAsset == "TAX1_N.SWF" &&
        specialTax.AudioAsset == "TAX.MP3" && refusedDonationCompany.TaxesOwed is >= 1_750m and <= 5_250m,
    "Refusing the royal donation must add the requested amount to taxes and show the original tax result.");
var rebelsGame = new GameSession(6, planets, 9056);
var rebelsCompany = new CompanyState("Rebels Test", true, 11, planets[0], 5_000_000m, 100_000m);
rebelsCompany.Cargo[0] = new CargoLot { Quantity = 10, AverageCost = 500m };
var rebelsResult = rebelsGame.ResolveDebugTravelEvent(rebelsCompany, 55);
Require(rebelsResult.Heading == "Rebels' Demand" && rebelsResult.ImageAsset == "REBELS.SWF" &&
        rebelsCompany.Cargo.GetValueOrDefault(0)?.Quantity == 10,
    "The rebels event must match its artwork and must not silently turn into the separate bandit cargo event.");
var meteorGame = new GameSession(6, planets, 9057);
var meteorCompany = new CompanyState("Meteor Test", true, 11, planets[0], 5_000_000m, 100_000m);
var meteorResult = meteorGame.ResolveDebugTravelEvent(meteorCompany, 56);
Require(meteorResult.Heading == "Close Escape" && meteorResult.ImageAsset == "METEOR.SWF",
    "The meteor event heading and artwork are inconsistent.");
var defectiveCargoGame = new GameSession(6, planets, 9058);
var defectiveCargoCompany = new CompanyState("Defective Cargo Test", true, 11,
    planets[0], 5_000_000m, 100_000m);
defectiveCargoCompany.Cargo[2] = new CargoLot { Quantity = 9, AverageCost = 500m };
defectiveCargoCompany.Cargo[14] = new CargoLot { Quantity = 25, AverageCost = 2_000m };
var defectiveCargoResult = defectiveCargoGame.ResolveDebugTravelEvent(defectiveCargoCompany, 57);
Require(defectiveCargoCompany.Cargo[2].Quantity == 9 &&
        defectiveCargoCompany.Cargo[14].Quantity == 12 &&
        defectiveCargoResult.Heading.StartsWith("Poor Quality", StringComparison.Ordinal) &&
        defectiveCargoResult.Message.Contains("13 tons", StringComparison.Ordinal),
    "The defective-cargo event did not select the last occupied commodity and discard half rounded up.");
var shortcutCompany = new CompanyState("Shortcut Company", true, 6, "Pyke", 100_000m, 0m)
    { LastPlanet = "Xeen", BaseEngineSpeed = 5 };
var shortcutGame = new GameSession(4, planets, 70043);
TravelEncounterCatalog.PilotShortcut(shortcutCompany);
shortcutGame.RecordTravelTime(shortcutCompany);
var ordinaryShortcutComparison = new CompanyState("Ordinary Company", true, 6, "Pyke", 100_000m, 0m)
    { LastPlanet = "Xeen", BaseEngineSpeed = 5 };
shortcutGame.RecordTravelTime(ordinaryShortcutComparison);
Require(shortcutCompany.TravelTime == Math.Floor(ordinaryShortcutComparison.TravelTime * 0.5d) &&
        shortcutCompany.TravelTimeMultiplier == 1d,
    "The pilot-shortcut event must halve only the current journey's travel time.");
var diversionCompany = new CompanyState("Diversion Company", true, 6, "Pyke", 100_000m, 0m);
var snoz = TravelEncounterCatalog.SnozTransport(diversionCompany, "Mira", 25);
var cashBeforeSnoz = diversionCompany.Cash;
snoz.Choice!.Resolve(true);
Require(diversionCompany.Planet == "Mira" && diversionCompany.Cash == cashBeforeSnoz + 25 * diversionCompany.ShipTons,
    "Snoz must pay the promised fare and immediately redirect the ship.");
var stubbsCompany = new CompanyState("Stubbs Company", true, 6, "Pyke", 100_000m, 0m);
var stubbs = TravelEncounterCatalog.StubbsWaterOffer(stubbsCompany, "Vexx", 25, true);
stubbs.Choice!.Resolve(true);
Require(stubbsCompany.Planet == "Vexx" && stubbsCompany.TravelDelay == 2,
    "Stubbs' navigation-error outcome must redirect and delay the ship.");
game.AdvanceWeek();
Require(human.CrewWagesOwed == human.CrewCount * human.CrewSalary,
    "Weekly crew wages should accrue until explicitly paid.");
Require(human.PayCrew().IsSuccessful && human.CrewWagesOwed == 0,
    "Manual crew payment failed.");
Require(human.PayTaxes().IsSuccessful && human.TaxesOwed == 0 && human.TariffsOwed == 0,
    "Manual tax and tariff payment failed.");
human.StandardLoanRate = 4m;
human.StandardCreditLimit = 150_000m;
human.SavingsRate = 2m;
var facilityAuctionSeed = FindAuctionSeed(shipAuction: false);
Require(new AuctionOffer("Facility", "Xeen", 4_861m, 11).BidPresets ==
        (24_305m, 72_915m, 145_830m),
    "Facility auction Lower/Middle/Upper presets do not equal 5, 15 and 30 times its fee.");
Require(new AuctionOffer("Ship", "Traders' Union", 0m, 2, true).BidPresets ==
        (10_000m, 25_000m, 50_000m),
    "Ship auction Lower/Middle/Upper presets do not match the original fixed bids.");
var facilityAuctionGame = new GameSession(4, planets, facilityAuctionSeed);
var facilityBidder = new CompanyState("Facility Bidder", true, 6, "Xeen", 500_000m, 0m);
var facilityAuctionAi = new CompanyState("Facility Auction AI", false, 8, "Pyke", 100_000m, 0m);
facilityAuctionGame.Companies.Add(facilityBidder);
facilityAuctionGame.Companies.Add(facilityAuctionAi);
AdvanceToFacilityAuctionWeek(facilityAuctionGame);
Require(facilityAuctionGame.CurrentAuction is { IsShipUpgrade: false },
    "Facility auctions did not begin after the ten-week grace period.");
Require(facilityAuctionGame.CurrentAuction!.Bids.Count == 0,
    "Computer auction bids were generated before the humans completed their turns.");
Require(facilityAuctionGame.PlaceAuctionBid(facilityBidder, 400_000m).IsSuccessful,
    "Facility auction bid failed.");
facilityAuctionGame.AdvanceWeek();
Require(facilityAuctionGame.Facilities.Any(facility => facility.OwnerName == facilityBidder.Name),
    "Facility auction was not resolved.");
Require(facilityAuctionGame.PendingAuctionResult?.Message.Contains(facilityBidder.Name) == true &&
        facilityAuctionGame.PendingAuctionResult.Message.Contains(facilityAuctionAi.Name) &&
        facilityAuctionGame.PendingAuctionResult.AudioAsset == "SHIP6.MP3" &&
        facilityAuctionGame.HasPendingAuctionResult(facilityBidder),
    "The auction did not queue the full result with the winning ship's music.");
var auctionResultSavePath = Path.Combine(AppContext.BaseDirectory, "auction-result-save.json");
GameSaveService.Save(facilityAuctionGame, auctionResultSavePath);
var loadedAuctionResult = GameSaveService.Load(auctionResultSavePath);
Require(loadedAuctionResult?.PendingAuctionResult?.Message.Contains(facilityBidder.Name) == true &&
        loadedAuctionResult.PendingAuctionResult.AudioAsset == "SHIP6.MP3",
    "A pending auction-result notification was lost when the autosave was reloaded.");
File.Delete(auctionResultSavePath);
facilityAuctionGame.AcknowledgeAuctionResult(facilityBidder);
Require(facilityAuctionGame.PendingAuctionResult is null,
    "The auction-result notice did not clear after every human player acknowledged it.");
var ownedFacility = facilityAuctionGame.Facilities.Single(facility => facility.OwnerName == facilityBidder.Name);
ai.Planet = ownedFacility.Planet;
var ownerCashBeforeFee = facilityBidder.Cash;
Require(facilityAuctionGame.ApplyFacilityFees(ai) == ownedFacility.Fee &&
        facilityBidder.Cash == ownerCashBeforeFee &&
        ownedFacility.Revenue == ownedFacility.Fee,
    "Facility landing fee was not accrued for its owner.");
facilityBidder.Planet = ownedFacility.Planet;
Require(facilityAuctionGame.ApplyFacilityFees(facilityBidder) == 0m &&
        facilityBidder.Cash == ownerCashBeforeFee + ownedFacility.Fee &&
        ownedFacility.Revenue == 0m && facilityBidder.PendingFacilityRevenue == ownedFacility.Fee,
    "Facility revenue was not collected when its owner landed.");
var cashlessVisitor = new CompanyState("Cashless Visitor", true, 1, ownedFacility.Planet, 0m, 0m);
facilityAuctionGame.Companies.Add(cashlessVisitor);
Require(facilityAuctionGame.ApplyFacilityFees(cashlessVisitor) == ownedFacility.Fee &&
        cashlessVisitor.PendingFacilityFees == ownedFacility.Fee &&
        cashlessVisitor.Loan == ownedFacility.Fee && ownedFacility.Revenue == ownedFacility.Fee,
    "Combined facility fees were not charged in full, queued for the arrival notice, and credited to the owner.");
human.LastPlanet = "Pyke";
human.TravelTime = 17d;
human.TravelDelay = 2;
human.AutomatedCrewPositions = 1;
human.CrewCapacityBonus = 2;
human.PlannedDestination = "Mira";
human.NextTicketPrice = 4_000m;
human.RecordPlanetVisit("Mira");
human.RecordPlanetVisit("Mira");
human.AiPassengerExperiences["Mira"] = new AiPassengerExperience
{
    Visits = 3,
    LastPassengers = 7,
    LastAdvertising = 5,
    LastTicketPrice = 4_250m,
    LastNetProfit = 12_000m,
    HasBestResult = true,
    BestPassengers = 8,
    BestAdvertising = 6,
    BestTicketPrice = 4_000m,
    BestNetProfit = 18_000m
};
human.RecordWarehouseFire("Mira", 42_000m, game.Week, insured: false);
human.AdvertisingLightOn = true;
human.ClearExternalNotice();
human.QueueExternalNotice("Sabotage Report", "A persisted sabotage notice.", "HAPA.SWF", "HAPA.MP3");
human.PendingTurnNotices.Clear();
human.PendingTurnNotices.Add(new TurnNotice("Gizzy Shipping: Adoption", "Gizzy adopts Spike.", "OP1.PNG", "GOOD2.MP3", true));
game.AiEventVisibility = AiEventVisibility.Full;
game.InitializeTurnOrder();
game.Markets["Xeen"].Listings[0].Price = 999m;
game.Markets["Xeen"].Listings[^1].Price = 1m;
var smokeSavePath = Path.Combine(AppContext.BaseDirectory, "smoke-autosave.json");
GameSaveService.Save(game, smokeSavePath);
var loaded = GameSaveService.Load(smokeSavePath);
Require(loaded is not null, "Autosave could not be loaded.");
Require(loaded!.Week == game.Week, "Saved week did not round-trip.");
Require(loaded.AiEventVisibility == AiEventVisibility.Full, "AI event-report visibility did not round-trip.");
Require(loaded.Markets["Xeen"].Listings[0].Price == CommodityCatalog.All[0].MaximumPrice &&
        loaded.Markets["Xeen"].Listings[^1].Price ==
        PlanetMarket.MinimumPrice(CommodityCatalog.All.Length - 1, loaded.Difficulty),
    "Legacy marketplace prices were not migrated into the original displayed ranges.");
Require(loaded.Companies.Count == 2, "Saved companies did not round-trip.");
var loadedHuman = loaded.Companies.Single(company => company.IsHuman);
Require(loadedHuman.Turbocharges == 1 && loadedHuman.BaseEngineSpeed == 5, "Engine state did not round-trip.");
Require(loadedHuman.Loan == human.Loan && loadedHuman.ZinnLoan == human.ZinnLoan,
    "Normal and Zinn loans did not round-trip independently.");
Require(loadedHuman.ShipValue == human.ShipValue, "Ship value did not round-trip.");
Require(loadedHuman.ShipTons == human.ShipTons, "Ship mass did not round-trip.");
Require(loadedHuman.CrewSalary == 1_500m && loadedHuman.WarehouseCapacity == 50,
    "Original player defaults did not round-trip.");
Require(loadedHuman.NetWorthHistory.SequenceEqual(human.NetWorthHistory),
    "Weekly company net-worth history did not round-trip.");
Require(loadedHuman.CommodityProfitThisWeek == human.CommodityProfitThisWeek,
    "Realized weekly commodity profit did not round-trip.");
Require(loadedHuman.LastPlanet == "Pyke" && loadedHuman.TravelTime == 17d && loadedHuman.TravelDelay == 2 &&
        loadedHuman.AutomatedCrewPositions == 1 && loadedHuman.CrewCapacityBonus == 2 &&
        loadedHuman.CrewCount == human.CrewCount,
    "Arrival timing, delay, crew expansion, or persistent crew automation did not round-trip.");
Require(loadedHuman.PlannedDestination == "Mira", "Planned destination did not round-trip.");
Require(loadedHuman.NextTicketPrice == 4_000m, "The next-planet ticket price did not round-trip.");
Require(loadedHuman.PlanetVisitCounts.GetValueOrDefault("Mira") == 2 &&
        loadedHuman.AdvertisingLightOn,
    "Planet-visit auction preferences or the advertising lamp did not round-trip.");
Require(loadedHuman.AiPassengerExperiences.TryGetValue("Mira", out var loadedPassengerExperience) &&
        loadedPassengerExperience.Visits == 3 && loadedPassengerExperience.LastTicketPrice == 4_250m &&
        loadedPassengerExperience.BestAdvertising == 6 && loadedPassengerExperience.BestNetProfit == 18_000m,
    "Destination-specific AI passenger results did not round-trip.");
Require(loadedHuman.AiWarehouseExperiences.TryGetValue("Mira", out var loadedWarehouseExperience) &&
        loadedWarehouseExperience.FireCount == 1 && loadedWarehouseExperience.InsuredFireCount == 0 &&
        loadedWarehouseExperience.ActualLoss == 42_000m && loadedWarehouseExperience.LastFireWeek == game.Week,
    "AI warehouse fire experience did not round-trip.");
Require(loadedHuman.PendingExternalMessage == "A persisted sabotage notice." &&
        loadedHuman.PendingTurnNotices.Count == 1 && loadedHuman.PendingTurnNotices[0].ImageAsset == "OP1.PNG" &&
        loadedHuman.PendingTurnNotices[0].UseCompanyAnnouncement,
    "Pending sabotage and AI-event splash notices did not survive saving and loading.");
Require(loaded.TurnOrder.SequenceEqual(game.TurnOrder) && loaded.ActiveTurnIndex == game.ActiveTurnIndex,
    "The shared human/AI turn order did not round-trip.");
Require(loadedHuman.CargoCapacityBonus == 10 && loadedHuman.PassengerCapacityBonus == 1 &&
        loadedHuman.FuelCapacityBonus == 5, "Xeen capacity upgrades did not round-trip.");
Require(loadedHuman.ZinnRate == human.ZinnRate && loadedHuman.ZinnCreditLimit == human.ZinnCreditLimit,
    "Zinn financing terms did not round-trip.");
Require(loadedHuman.StandardLoanRate == human.StandardLoanRate &&
        loadedHuman.StandardCreditLimit == human.StandardCreditLimit &&
        loadedHuman.SavingsRate == human.SavingsRate, "Stye banking terms did not round-trip.");
Require(loadedHuman.InsurancePriceRange == human.InsurancePriceRange,
    "Insurance price range did not round-trip.");
Require(loadedHuman.Shortcuts.GetValueOrDefault("fuel"), "Shortcut options did not round-trip.");
Require(loadedHuman.PreferredPassengerAdvertising == human.PreferredPassengerAdvertising &&
        loadedHuman.PreferredCommodityAdvertising == human.PreferredCommodityAdvertising,
    "Preferred quick-advertising choices did not round-trip.");
Require(loadedHuman.Warehouses.ContainsKey("Xeen"), "Warehouse state did not round-trip.");
Require(game.SharePrices.All(pair => loaded.SharePrices.TryGetValue(pair.Key, out var loadedPrice) && loadedPrice == pair.Value),
    "Stock prices did not round-trip.");
Require(game.SharePriceHistory.All(pair => loaded.SharePriceHistory.TryGetValue(pair.Key, out var loadedHistory) &&
        loadedHistory.SequenceEqual(pair.Value)), "Stock price history did not round-trip.");
Require(loadedHuman.ShareAverageCosts.GetValueOrDefault(human.Planet) ==
        human.ShareAverageCosts.GetValueOrDefault(human.Planet), "Average share purchase price did not round-trip.");
Require(loaded.Facilities.Count == game.Facilities.Count, "Facility ownership did not round-trip.");
Require(loaded.WeatherPlanet == game.WeatherPlanet && loaded.WeatherCode == game.WeatherCode,
    "Weather state did not round-trip.");
Require(loaded.LastTurnNews.SequenceEqual(game.LastTurnNews), "Weekly news did not round-trip.");

var visitTrackingGame = new GameSession(4, planets, 9911);
var visitTrackingCompany = new CompanyState("Visit Tracking", false, 6, "Xeen", 100_000m, 0m)
    { LastPlanet = "Xeen", Planet = "Pyke" };
visitTrackingGame.RecordTravelTime(visitTrackingCompany);
visitTrackingCompany.LastPlanet = "Zile";
visitTrackingCompany.Planet = "Pyke";
visitTrackingGame.RecordTravelTime(visitTrackingCompany);
Require(visitTrackingCompany.PlanetVisitCounts.GetValueOrDefault("Pyke") == 2 &&
        visitTrackingCompany.FacilityAuctionVisitMultiplier("Pyke") == 1.125m,
    "Actual arrivals did not build the persistent repeat-visit facility preference.");

var populateAiBids = typeof(GameSession).GetMethod("PopulateAiAuctionBids",
    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
    ?? throw new InvalidOperationException("AI auction bidder was not found.");
var aggressiveBidGame = new GameSession(4, planets, 7711);
var aggressiveBidAi = new CompanyState("Trading Corp. IV", false, 6, "Xeen", 100_000m, 0m);
aggressiveBidGame.Companies.Add(aggressiveBidAi);
var aggressiveShipOffer = new AuctionOffer("Ship", "Traders' Union", 0m, 11, true);
populateAiBids.Invoke(aggressiveBidGame, new object[] { aggressiveShipOffer, new Random(775) });
Require(aggressiveShipOffer.Bids[aggressiveBidAi.Name] > aggressiveBidAi.Cash * 0.08m,
    "The original-style AI ship bid remained trapped inside the old one-to-eight-percent cash range.");

var frequentVisitorGame = new GameSession(4, planets, 7712);
var frequentVisitor = new CompanyState("Trading Corp. IV", false, 6, "Xeen", 5_000_000m, 0m);
frequentVisitorGame.Companies.Add(frequentVisitor);
var ordinaryFacilityOffer = new AuctionOffer("Freight Terminal", "Pyke", 4_000m, 11);
populateAiBids.Invoke(frequentVisitorGame, new object[] { ordinaryFacilityOffer, new Random(881) });
var ordinaryFacilityBid = ordinaryFacilityOffer.Bids[frequentVisitor.Name];
frequentVisitor.PlanetVisitCounts["Pyke"] = 13;
var preferredFacilityOffer = new AuctionOffer("Freight Terminal", "Pyke", 4_000m, 11);
populateAiBids.Invoke(frequentVisitorGame, new object[] { preferredFacilityOffer, new Random(881) });
Require(preferredFacilityOffer.Bids[frequentVisitor.Name] ==
        GameMath.WholeKubars(ordinaryFacilityBid * 2.5m),
    "Frequent AI visits did not raise the facility bid to the configured 2.5-times preference cap.");

var shipAuctionGame = new GameSession(4, planets, FindAuctionSeed(shipAuction: true));
var shipBidder = new CompanyState("Ship Bidder", true, 6, "Xeen", 1_000_000m, 130_000m);
shipAuctionGame.Companies.Add(shipBidder);
AdvanceToFacilityAuctionWeek(shipAuctionGame);
Require(shipAuctionGame.CurrentAuction?.IsShipUpgrade == true,
    "Ship auctions did not begin after the ten-week grace period.");
Require(shipAuctionGame.PlaceAuctionBid(shipBidder, 100_000m).IsSuccessful, "Ship auction bid failed.");
shipAuctionGame.AdvanceWeek();
Require(shipBidder.ShipTons == 600 && shipBidder.CargoCapacity == 150 &&
        shipBidder.PassengerCapacity == 12 && shipBidder.FuelCapacity == 55 &&
        shipBidder.BaseEngineSpeed == 5 && shipBidder.CrewCount == 6 &&
        shipBidder.InsurancePriceRange == 21,
    "Winning the ship auction did not apply the Cerebralis 200-ton expansion package.");

TravelEncounterCatalog.ApplyShipExpansion(shipBidder);
Require(shipBidder.ShipTons == 800 && shipBidder.CargoCapacity == 200 &&
        shipBidder.PassengerCapacity == 16 && shipBidder.FuelCapacity == 70 &&
        shipBidder.CrewCount == 8 && shipBidder.InsurancePriceRange == 27,
    "A repeated ship expansion did not apply the full model-specific package again.");

var auctionCreditGame = new GameSession(4, planets, FindAuctionSeed(shipAuction: false));
var financedBidder = new CompanyState("Financed Bidder", true, 6, "Xeen", 10_000m, 0m)
    { Bank = 20_000m, Loan = 50_000m };
auctionCreditGame.Companies.Add(financedBidder);
AdvanceToFacilityAuctionWeek(auctionCreditGame);
financedBidder.Cash = 10_000m;
financedBidder.Bank = 20_000m;
financedBidder.Loan = 50_000m;
Require(!auctionCreditGame.PlaceAuctionBid(financedBidder, 80_001m).IsSuccessful,
    "An auction bid above cash, savings and remaining credit was accepted.");
Require(auctionCreditGame.PlaceAuctionBid(financedBidder, 60_000m).IsSuccessful,
    "Auction bids cannot use the original cash, savings and available-credit total.");
Require(auctionCreditGame.CurrentAuction!.Bids[financedBidder.Name] == 60_000m,
    "The auction silently changed the bidder's entered amount.");
financedBidder.Cash = 0m;
financedBidder.Bank = 0m;
auctionCreditGame.AdvanceWeek();
Require(financedBidder.Cash == 0m && financedBidder.Bank == 0m && financedBidder.Loan >= 110_000m &&
        auctionCreditGame.Facilities.Any(facility => facility.OwnerName == financedBidder.Name),
    "A confirmed winning bid was not honored through Traders' Union financing after liquid funds changed.");

var tiedAuctionGame = new GameSession(4, planets, FindAuctionSeed(shipAuction: false, humanPlayers: 2));
var tiedOne = new CompanyState("Tie One", true, 6, "Xeen", 100_000m, 0m);
var tiedTwo = new CompanyState("Tie Two", true, 6, "Xeen", 100_000m, 0m);
tiedAuctionGame.Companies.Add(tiedOne);
tiedAuctionGame.Companies.Add(tiedTwo);
AdvanceToFacilityAuctionWeek(tiedAuctionGame);
var tiedOfferName = tiedAuctionGame.CurrentAuction!.Name;
Require(tiedAuctionGame.PlaceAuctionBid(tiedOne, 50_000m).IsSuccessful,
    "The first tied auction bid was not accepted.");
Require(!tiedAuctionGame.PlaceAuctionBid(tiedTwo, 50_000m).IsSuccessful &&
        !tiedAuctionGame.CurrentAuction.Bids.ContainsKey(tiedTwo.Name),
    "A later player could submit an auction bid during the previous player's turn.");
Require(!tiedAuctionGame.AdvanceScheduledTurnsAfterHuman(tiedOne) &&
        ReferenceEquals(tiedAuctionGame.CurrentTurnCompany, tiedTwo),
    "The auction did not hand off to the second identified human bidder.");
Require(tiedAuctionGame.PlaceAuctionBid(tiedTwo, 50_000m).IsSuccessful,
    "Tied auction bids were not accepted.");
Require(tiedAuctionGame.AdvanceScheduledTurnsAfterHuman(tiedTwo),
    "The auction resolved before every human player had completed the round.");
Require(tiedAuctionGame.CurrentAuction?.Name == tiedOfferName && tiedAuctionGame.CurrentAuction.Bids.Count == 0,
    "A tied auction was not automatically repeated with fresh bids.");
Require(tiedAuctionGame.PendingAuctionResult is not null &&
        tiedAuctionGame.HasPendingAuctionResult(tiedOne) &&
        tiedAuctionGame.HasPendingAuctionResult(tiedTwo),
    "The tied result was not queued for both players at the start of the following week.");
tiedAuctionGame.AcknowledgeAuctionResult(tiedOne);
Require(tiedAuctionGame.PendingAuctionResult is not null &&
        !tiedAuctionGame.HasPendingAuctionResult(tiedOne) &&
        tiedAuctionGame.HasPendingAuctionResult(tiedTwo),
    "One player's auction-result acknowledgement incorrectly consumed the other player's notice.");
tiedAuctionGame.AcknowledgeAuctionResult(tiedTwo);
Require(tiedAuctionGame.PendingAuctionResult is null,
    "The auction result remained after every human player acknowledged it.");

var modTestRoot = Path.Combine(Path.GetTempPath(), "OpenTradeEngine", "mod-smoke-" + Guid.NewGuid().ToString("N"));
var modTestFolder = Path.Combine(modTestRoot, "SmokeMod");
Directory.CreateDirectory(Path.Combine(modTestFolder, "Planets"));
Directory.CreateDirectory(Path.Combine(modTestFolder, "Events"));
File.WriteAllText(Path.Combine(modTestFolder, "mod.json"),
    """
    { "name": "Smoke Mod", "version": "1.0.0", "enabled": true }
    """);
File.WriteAllText(Path.Combine(modTestFolder, "Planets", "modora.json"),
    """
    {
      "name": "Modora",
      "description": "a world loaded from a content mod.",
      "subtitle": "The Modded World"
    }
    """);
File.WriteAllText(Path.Combine(modTestFolder, "Events", "visitor.json"),
    """
    {
      "id": "smoke.visitor",
      "heading": "Mod Visitor",
      "message": "A mod visitor greets {company} on {planet}.",
      "kind": "Either",
      "chancePercent": 100,
      "minWeek": 2,
      "planet": "Modora"
    }
    """);
ModCatalog.Reload(modTestRoot);
Require(ModCatalog.Mods.Count == 1 && ModCatalog.Planets.Count == 1 && ModCatalog.Events.Count == 1 &&
        ModCatalog.Errors.Count == 0,
    "The executable-side mod folder did not load its manifest, planet and event definitions.");
Require(PlanetCatalog.All.Any(planet => planet.Name == "Modora") &&
        PlanetCatalog.Describe("Modora").Contains("content mod", StringComparison.Ordinal),
    "A mod planet was not merged into the selectable planet catalog.");
var modEventGame = new GameSession(4, new[] { "Modora", "Xeen" }, 7713);
var modEventCompany = new CompanyState("Mod Tester", true, 6, "Modora", 100_000m, 0m);
modEventGame.Companies.Add(modEventCompany);
modEventGame.AdvanceWeek();
var modJourney = modEventGame.ResolveJourneyEvents(modEventCompany);
Require(modJourney.Any(travelEvent => travelEvent.Heading == "Mod Visitor" &&
                                     travelEvent.Message.Contains("Mod Tester", StringComparison.Ordinal)),
    "A loaded mod event did not participate in the normal multi-event journey sequence.");
var chaosDefinition = new ModEventDefinition
{
    Id = "smoke.chaos-monk",
    Heading = "The Chaos Monk",
    Message = "The Chaos Monk requests {fee} kubars.",
    Kind = ModEventKind.Good,
    Effect = "ChaosMonkSabotage",
    FeePerShipTon = 5m,
    MinWeek = 11,
    SourceDirectory = modTestFolder
};
var chaosFailureGame = new GameSession(4, new[] { "Modora", "Xeen" }, 7714);
while (chaosFailureGame.Week < 11) chaosFailureGame.AdvanceWeek();
var chaosFailureBuyer = new CompanyState("Chaos Buyer", true, 6, "Modora", 100_000m, 0m) { Luck = 70 };
var chaosFailureLeader = new CompanyState("Chaos Leader", true, 7, "Xeen", 500_000m, 0m) { Luck = 70 };
chaosFailureGame.Companies.Add(chaosFailureBuyer);
chaosFailureGame.Companies.Add(chaosFailureLeader);
Require(chaosDefinition.IsEligible(chaosFailureGame, chaosFailureBuyer, 11, goodChain: true) &&
        !chaosDefinition.IsEligible(chaosFailureGame, chaosFailureLeader, 11, goodChain: true),
    "The Chaos Monk was not restricted from the highest-net-worth active company.");
var failureSeed = FindChaosSeed(chaosFailureBuyer.Name, roll => roll == 1);
var failureOffer = chaosDefinition.Apply(chaosFailureGame, chaosFailureBuyer, failureSeed);
var failureResult = failureOffer.Choice!.Resolve(true);
Require(chaosFailureBuyer.Cash == 98_000m && chaosFailureBuyer.Luck == CompanyState.MinimumLuck &&
        chaosFailureLeader.Luck == 70 && failureResult.SuppressAiEventNotice,
    "The one-in-twelve Chaos Monk backfire did not charge the cheap fee and curse only its buyer.");

var chaosEveryoneGame = new GameSession(4, new[] { "Modora", "Xeen" }, 7715);
while (chaosEveryoneGame.Week < 11) chaosEveryoneGame.AdvanceWeek();
var chaosEveryoneBuyer = new CompanyState("Everyone Buyer", true, 6, "Modora", 100_000m, 0m) { Luck = 80 };
var chaosEveryoneLeader = new CompanyState("Everyone Leader", true, 7, "Xeen", 500_000m, 0m) { Luck = 80 };
var chaosEveryoneAi = new CompanyState("Gizzy Shipping", false, 2, "Xeen", 200_000m, 0m) { Luck = 80 };
chaosEveryoneGame.Companies.Add(chaosEveryoneBuyer);
chaosEveryoneGame.Companies.Add(chaosEveryoneLeader);
chaosEveryoneGame.Companies.Add(chaosEveryoneAi);
var everyoneSeed = FindChaosSeed(chaosEveryoneBuyer.Name, roll => roll is 2 or 3);
var everyoneResult = chaosDefinition.Apply(chaosEveryoneGame, chaosEveryoneBuyer, everyoneSeed).Choice!.Resolve(true);
Require(chaosEveryoneBuyer.Luck < 80 && chaosEveryoneLeader.Luck < 80 && chaosEveryoneAi.Luck < 80,
    "The one-in-six Chaos Monk outcome did not reduce the buyer's luck along with every opponent.");
Require(chaosEveryoneBuyer.PendingTurnNotices.Count == 1 &&
        chaosEveryoneLeader.PendingTurnNotices.Count == 1 &&
        chaosEveryoneAi.PendingTurnNotices.Count == 0 &&
        chaosEveryoneBuyer.PendingTurnNotices[0].ImageAsset == "SOOTH_N.SWF" &&
        !chaosEveryoneBuyer.PendingTurnNotices[0].UseCompanyAnnouncement &&
        everyoneResult.SuppressAiEventNotice,
    "Chaos Monk victims did not receive private Ooom warnings or leaked a public/AI event announcement.");

var chaosSubsetGame = new GameSession(4, new[] { "Modora", "Xeen" }, 7716);
while (chaosSubsetGame.Week < 11) chaosSubsetGame.AdvanceWeek();
var chaosSubsetBuyer = new CompanyState("Subset Buyer", true, 6, "Modora", 100_000m, 0m) { Luck = 70 };
var chaosSubsetLeader = new CompanyState("Subset Leader", true, 7, "Xeen", 500_000m, 0m) { Luck = 70 };
var chaosSubsetRival = new CompanyState("Subset Rival", true, 2, "Xeen", 200_000m, 0m) { Luck = 70 };
chaosSubsetGame.Companies.Add(chaosSubsetBuyer);
chaosSubsetGame.Companies.Add(chaosSubsetLeader);
chaosSubsetGame.Companies.Add(chaosSubsetRival);
var subsetSeed = FindChaosSeed(chaosSubsetBuyer.Name, roll => roll >= 4);
chaosDefinition.Apply(chaosSubsetGame, chaosSubsetBuyer, subsetSeed).Choice!.Resolve(true);
Require(chaosSubsetBuyer.Luck == 70 &&
        new[] { chaosSubsetLeader, chaosSubsetRival }.Any(company => company.Luck < 70),
    "The normal Chaos Monk result did not exclude its buyer and reduce a non-empty random opponent subset.");
ModCatalog.SetEnabled(false, modTestRoot);
Require(!ModCatalog.Enabled && ModCatalog.Mods.Count == 0 && ModCatalog.Planets.Count == 0 &&
        ModCatalog.Events.Count == 0,
    "Disabling mods on the launcher did not unload every mod content type.");
ModCatalog.SetEnabled(true, modTestRoot);
Require(ModCatalog.Enabled && ModCatalog.Mods.Count == 1 && ModCatalog.Planets.Count == 1 &&
        ModCatalog.Events.Count == 1,
    "Re-enabling mods did not reload the executable-side Mods folder.");
Directory.Delete(modTestRoot, recursive: true);
ModCatalog.Reload();

var gameplayLogRoot = Path.Combine(Path.GetTempPath(), "OpenTradeEngine", "log-smoke-" + Guid.NewGuid().ToString("N"));
GameplayLogger.Configure(true, 1, gameplayLogRoot);
var loggedSession = new GameSession(4, new[] { "Bass", "Pyke" }, 88191);
var loggedHuman = new CompanyState("Logged Human", true, 6, "Bass", 100_000m, 0m);
var loggedAi = new CompanyState("Gizzy Shipping", false, 2, "Pyke", 100_000m, 0m);
loggedSession.Companies.Add(loggedHuman);
loggedSession.Companies.Add(loggedAi);
GameplayLogger.StartSession(loggedSession, "smoke test");
for (var index = 0; index < 700; index++)
    GameplayLogger.Log("ROTATION TEST", loggedAi.Name, $"entry={index}; payload={new string('x', 2_048)}");
loggedSession.Markets["Bass"].Listings[0].Quantity = 1;
loggedSession.Markets["Bass"].Listings[0].Price = 100m;
Require(loggedHuman.Buy(loggedSession.Markets["Bass"], 0, 1).IsSuccessful,
    "The semantic gameplay-log smoke action failed.");
GameplayLogger.LogCompanyState("FINAL STATE", loggedAi, "rotation survived");
GameplayLogger.Shutdown();
var gameplayLogFiles = Directory.GetFiles(gameplayLogRoot, "*.log");
var gameplayLogBytes = gameplayLogFiles.Sum(path => new FileInfo(path).Length);
var gameplayLogText = string.Join('\n', gameplayLogFiles.Select(File.ReadAllText));
Require(gameplayLogFiles.Length > 0 && gameplayLogBytes <= 1_048_576 + 4_096 &&
        gameplayLogText.Contains("rotation survived", StringComparison.Ordinal) &&
        gameplayLogText.Contains("[Action ", StringComparison.Ordinal) &&
        gameplayLogText.Contains("action=BUY COMMODITY", StringComparison.Ordinal) &&
        !gameplayLogText.Contains("PLAYER INPUT", StringComparison.Ordinal),
    "The plain-text gameplay logger did not rotate, retain its newest action, or respect its storage limit.");
Directory.Delete(gameplayLogRoot, recursive: true);

Console.WriteLine("OpenTradeEngine simulation smoke test passed.");

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static int FindChaosSeed(string companyName, Func<int, bool> predicate)
{
    for (var seed = 0; seed < 100_000; seed++)
    {
        var random = new Random(GameMath.StableHash(seed, companyName, "Chaos Monk sabotage"));
        if (predicate(random.Next(1, 13))) return seed;
    }
    throw new InvalidOperationException("Could not find a deterministic Chaos Monk outcome seed.");
}

static int FindAuctionSeed(bool shipAuction, int humanPlayers = 1)
{
    _ = humanPlayers; // Auction frequency no longer changes with human-player count.
    for (var seed = 0; seed < 100_000; seed++)
    {
    if (shipAuction)
    {
            var openingRandom = new Random(GameMath.StableHash(seed, "11", "auction schedule"));
            if (openingRandom.Next(4) == 0 && openingRandom.Next(4) == 0) return seed;
            continue;
    }

        var random = new Random(GameMath.StableHash(seed, "11", "auction schedule"));
        if (random.Next(4) == 0 && random.Next(4) != 0) return seed;
    }
    throw new InvalidOperationException("Could not find a deterministic auction test seed.");
}

static void AdvanceToFacilityAuctionWeek(GameSession game)
{
    while (game.Week < 11)
    {
        game.AdvanceWeek();
        Require(game.Week > 10 || game.CurrentAuction is null,
            "An auction was created during the ten-week opening grace period.");
    }
}
