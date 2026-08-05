using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenTradeEngine;

/// <summary>
/// Computer-player orchestration and decision making. This is a partial of
/// GameSession so AI uses the exact same markets, finance rules and actions as
/// human companies without exposing those internals as a second public API.
/// </summary>
public sealed partial class GameSession
{
    private const int AutomaticMaximumCommodityAdvertisingCapacity = 120;

    public void RunAiTurns()
    {
        LastTurnNews.Clear();
        foreach (var company in Companies.Where(company => !company.IsHuman))
            RunAiTurn(company);
    }

    public void RunAiTurn(CompanyState company)
    {
        if (company.IsHuman || company.IsBankrupt) return;
        GameplayLogger.LogCompanyState("AI TURN", company, "BEGIN");
        var profile = AiOpponentCatalog.ForCompany(company.Name);
        var arrivalPlanet = company.Planet;
        var localMarket = Markets[arrivalPlanet];

        if (!TryRescueAiCredit(company, localMarket))
        {
            company.IsBankrupt = true;
            LastTurnNews.Add($"Headline News: {company.Name} has gone bankrupt because it could not bring its debt back within its credit limits.");
            GameplayLogger.LogCompanyState("AI BANKRUPTCY", company,
                "Credit rescue failed; company removed from play");
            return;
        }
        GameplayLogger.LogCompanyState("AI CREDIT", company, "Credit check/rescue completed");

        var passengerRandom = new Random(GameMath.StableHash(Seed, Week.ToString(), company.Name, "passengers"));
        var arrivedFare = company.TicketPrice;
        var arrivedPassengerAdvertising = company.PassengerAdvertising;
        var arrivedPassengers = company.GeneratePassengers(passengerRandom);
        company.RecordAiPassengerResult(arrivalPlanet, arrivedPassengers,
            arrivedPassengerAdvertising, arrivedFare);
        GameplayLogger.Log("AI PASSENGERS", company.Name,
            $"planet={arrivalPlanet}; fare={arrivedFare:0}; advertising={arrivedPassengerAdvertising}; " +
            $"boarded={arrivedPassengers}; capacity={company.PassengerCapacity}; " +
            $"gross={arrivedPassengers * arrivedFare:0}");
        RunAiPlanetSpecial(company);
        GameplayLogger.LogCompanyState("AI SPECIAL", company, "Planet-special decision completed");
        RunAiStockTrade(company);
        GameplayLogger.LogCompanyState("AI STOCKS", company, "Stock-market decision completed");
        var liquidatedCommodities = LiquidateAiCargoForBetterTrades(company, localMarket);
        LiquidateProfitableWarehouseCargo(company, localMarket);
        GameplayLogger.LogCompanyState("AI CARGO", company,
            $"Liquidation completed; excluded=[{string.Join(',', liquidatedCommodities)}]");
        TryRescueAiCredit(company, localMarket);
        // Capture liquid wealth before working-capital borrowing. Warehouse
        // speculation may use genuine surplus cash/savings, never new credit.
        var preBorrowLiquidFunds = company.Cash + company.Bank;
        EnsureAiWorkingCapital(company, profile, localMarket);
        GameplayLogger.LogCompanyState("AI FINANCE", company, "Working capital prepared");

        var plan = BuildAiTradePlan(company, profile, 0m, insured: false, liquidatedCommodities);
        var projectedCargoValue = plan.Purchases.Sum(purchase => purchase.Quantity * purchase.Price);
        var insure = ShouldAiBuyInsurance(company, profile, plan.Destination, projectedCargoValue);
        if (insure)
        {
            plan = BuildAiTradePlan(company, profile, company.InsuranceCost, insured: true,
                liquidatedCommodities);
            projectedCargoValue = plan.Purchases.Sum(purchase => purchase.Quantity * purchase.Price);
            insure = ShouldAiBuyInsurance(company, profile, plan.Destination, projectedCargoValue);
        }
        GameplayLogger.Log("AI PLAN", company.Name,
            $"destination={plan.Destination}; expectedProfit={plan.ExpectedProfit:0}; " +
            $"fuelRequired={plan.FuelRequired:0.###}; projectedCargoValue={projectedCargoValue:0}; " +
            $"insuranceChosen={insure}; purchases=[{string.Join(',', plan.Purchases.Select(purchase =>
                $"{purchase.CommodityIndex}:{purchase.Quantity}@{purchase.Price:0}"))}]");

        var completedPurchases = new List<ArrivalPurchase>();
        foreach (var purchase in plan.Purchases)
        {
            var result = company.Buy(localMarket, purchase.CommodityIndex, purchase.Quantity);
            GameplayLogger.Log("AI MARKET", company.Name,
                $"buy commodity={purchase.CommodityIndex}; quantity={purchase.Quantity}; " +
                $"price={purchase.Price:0}; success={result.IsSuccessful}; result={result.Message}");
            if (!result.IsSuccessful) continue;
            completedPurchases.Add(new ArrivalPurchase(purchase.CommodityIndex, purchase.Quantity));
            LastTurnNews.Add($"{company.Name} bought {purchase.Quantity} tons of " +
                             $"{CommodityCatalog.All[purchase.CommodityIndex].Name} on {company.Planet}.");
        }

        // Planned journey cargo gets first claim on the shared market and the
        // ship's hold. Only then may genuine pre-existing surplus be used for
        // long-term warehouse speculation.
        StockpileCheapWarehouseCargo(company, profile, localMarket, plan,
            preBorrowLiquidFunds, insure ? company.InsuranceCost : 0m);

        BuyAiFuelForJourney(company, localMarket, plan.FuelRequired);
        GameplayLogger.LogCompanyState("AI FUEL", company,
            $"Fuel purchase completed for required={plan.FuelRequired:0.###}");
        if (insure && company.InsuranceLevel == 0)
        {
            var insuranceResult = company.SetInsurance(1);
            GameplayLogger.Log("AI INSURANCE", company.Name,
                $"success={insuranceResult.IsSuccessful}; result={insuranceResult.Message}");
        }

        var (passengerAdvertising, nextTicketPrice) =
            ChooseAiPassengerPlan(company, profile, plan.Destination);
        var commodityAdvertising = ChooseAiCommodityAdvertising(company, profile, plan.ExpectedProfit);
        var preserveMaximumCommodityAdvertising = CanSafelyFundAutomaticCommodityAdvertising(company, profile);
        while (company.AdvertisingCost(passengerAdvertising) + company.AdvertisingCost(commodityAdvertising) > company.Cash)
        {
            if (preserveMaximumCommodityAdvertising && passengerAdvertising > 0) passengerAdvertising--;
            else if (commodityAdvertising >= passengerAdvertising && commodityAdvertising > 0) commodityAdvertising--;
            else if (passengerAdvertising > 0) passengerAdvertising--;
            else break;
        }
        var advertisingResult = company.SetAdvertisingCampaign(passengerAdvertising, commodityAdvertising);
        var ticketResult = company.SetNextTicketPrice(nextTicketPrice);
        GameplayLogger.Log("AI ADVERTISING", company.Name,
            $"passengerLevel={passengerAdvertising}; commodityLevel={commodityAdvertising}; " +
            $"fare={nextTicketPrice:0}; advertisingSuccess={advertisingResult.IsSuccessful}; " +
            $"ticketSuccess={ticketResult.IsSuccessful}; advertisingResult={advertisingResult.Message}; " +
            $"ticketResult={ticketResult.Message}");

        company.PlannedDestination = plan.Destination;
        RecordArrivalReport(company, arrivalPlanet, completedPurchases);
        company.Fuel -= plan.FuelRequired;
        if (company.Fuel < 0m) company.ApplyEmergencyRefuel();
        company.LastPlanet = company.Planet;
        company.Planet = plan.Destination;
        company.PlannedDestination = string.Empty;
        var facilityFees = ApplyFacilityFees(company);
        GameplayLogger.LogCompanyState("AI TURN", company,
            $"END; travelled={arrivalPlanet}->{plan.Destination}; fuelUsed={plan.FuelRequired:0.###}; " +
            $"facilityFees={facilityFees:0}");
    }

    private void QueueAcceptedAiOfferNotice(
        CompanyState company, string offerHeading, TravelEventResult result)
    {
        if (IsSuccessfulSabotage(result)) return;

        var opponent = AiOpponentCatalog.ForCompany(company.Name);
        var caughtByPolice = result.Heading.Contains("Caught", StringComparison.OrdinalIgnoreCase) ||
                             result.Message.Contains("police", StringComparison.OrdinalIgnoreCase);
        var publicMessage = caughtByPolice
            ? $"{company.Name} was caught by the Imperial Police while attempting an illegal deal."
            : $"{company.Name} accepted an offer concerning {offerHeading}.";
        foreach (var human in Companies.Where(candidate => candidate.IsHuman && !candidate.IsBankrupt))
            human.PendingTurnNotices.Add(new TurnNotice(
                caughtByPolice ? "Caught By The Police" : $"{company.Name} Accepts an Offer",
                publicMessage,
                $"OP{opponent.Number}.PNG", result.AudioAsset));
    }

    private void QueueFullAiEventNotice(
        CompanyState company, TravelEventResult presentedEvent, TravelEventResult outcome)
    {
        if (presentedEvent.Heading.Equals("A Quiet Journey", StringComparison.OrdinalIgnoreCase) ||
            IsSuccessfulSabotage(outcome)) return;

        var opponent = AiOpponentCatalog.ForCompany(company.Name);
        var choiceContext = presentedEvent.Choice is null
            ? string.Empty
            : $"{company.Name} encountered {presentedEvent.Heading}.\n\n";
        var notice = new TurnNotice($"{company.Name}: {outcome.Heading}",
            choiceContext + outcome.Message, $"OP{opponent.Number}.PNG", outcome.AudioAsset);
        foreach (var human in Companies.Where(candidate => candidate.IsHuman && !candidate.IsBankrupt))
            human.PendingTurnNotices.Add(notice);
    }

    private static bool IsSuccessfulSabotage(TravelEventResult result) => result.IsGood &&
        (result.Heading.Equals("Sabotage Complete", StringComparison.OrdinalIgnoreCase) ||
         result.Heading.Equals("Competitors Attacked", StringComparison.OrdinalIgnoreCase));

    private bool TryRescueAiCredit(CompanyState company, PlanetMarket localMarket)
    {
        company.ProtectCreditLimitsBeforeTravel();
        if (!company.WouldExceedAnyCreditLimit) return true;

        foreach (var cargo in company.Cargo
                     .OrderByDescending(pair => localMarket.Listings[pair.Key].Price)
                     .ToArray())
        {
            var price = localMarket.Listings[cargo.Key].Price;
            if (price <= 0m) continue;
            var quantity = Math.Min(cargo.Value.Quantity,
                Math.Max(1, (int)decimal.Ceiling(company.RequiredCreditPayment / price)));
            var result = company.Sell(localMarket, cargo.Key, quantity);
            if (result.IsSuccessful)
                LastTurnNews.Add($"{company.Name} liquidated {quantity:N0} tons of " +
                                 $"{CommodityCatalog.All[cargo.Key].Name} to satisfy its creditors.");
            company.ProtectCreditLimitsBeforeTravel();
            if (!company.WouldExceedAnyCreditLimit) return true;
        }

        foreach (var holding in company.Shares
                     .Where(pair => pair.Value > 0 && SharePrices.GetValueOrDefault(pair.Key) > 0m)
                     .OrderByDescending(pair =>
                         pair.Key.Equals(company.Planet, StringComparison.OrdinalIgnoreCase))
                     .ThenByDescending(pair => SharePrices[pair.Key] * pair.Value)
                     .ToArray())
        {
            var price = SharePrices[holding.Key];
            var netPerShare = price - GameMath.WholeKubars(price * 0.01m);
            if (netPerShare <= 0m) continue;
            var quantity = Math.Min(holding.Value,
                Math.Max(1, (int)decimal.Ceiling(company.RequiredCreditPayment / netPerShare)));
            var result = company.SellShares(holding.Key, price, quantity);
            if (result.IsSuccessful)
                LastTurnNews.Add($"{company.Name}'s broker liquidated {quantity:N0} shares on the " +
                                 $"{holding.Key} Exchange to satisfy its creditors.");
            company.ProtectCreditLimitsBeforeTravel();
            if (!company.WouldExceedAnyCreditLimit) return true;
        }

        return !company.WouldExceedAnyCreditLimit;
    }

    private void RunAiStockTrade(CompanyState company)
    {
        InitializeStocks();
        if (!IsExchangeOpen(company.Planet) || !SharePrices.TryGetValue(company.Planet, out var localPrice))
        {
            GameplayLogger.Log("AI STOCK DECISION", company.Name,
                $"planet={company.Planet}; decision=skip; reason=exchange unavailable");
            return;
        }
        var trend = Math.Clamp(StockTrends.GetValueOrDefault(company.Planet, 50), 0, 100);

        // The chart's most recent movement is not itself a buy/sell signal.
        // Gazillionaire carries a hidden bullish/bearish stockTrend value:
        // 0..40 sell, 41..60 hold, and 61..100 buy. Bass reports that value
        // but does not manufacture a bullish recommendation.
        if (trend <= 40)
        {
            var owned = company.Shares.GetValueOrDefault(company.Planet);
            if (owned > 0)
            {
                var result = company.SellShares(company.Planet, localPrice, owned);
                GameplayLogger.Log("AI STOCK DECISION", company.Name,
                    $"trend={trend}; decision=sell; shares={owned}; price={localPrice:0}; " +
                    $"success={result.IsSuccessful}; result={result.Message}");
                if (result.IsSuccessful)
                    LastTurnNews.Add($"{company.Name} sold {owned:N0} shares on the {company.Planet} Exchange.");
            }
            return;
        }
        if (trend <= 60)
        {
            GameplayLogger.Log("AI STOCK DECISION", company.Name,
                $"trend={trend}; decision=hold; price={localPrice:0}");
            return;
        }

        // There is no artificial player-only purchase ceiling. The AI instead
        // makes a strategic choice to retain fuel, payroll, debt and trading
        // capital before investing a personality-dependent share of the excess.
        var localMarket = Markets[company.Planet];
        var cheapestCargo = localMarket.Listings.Where(listing => listing.Quantity > 0 && listing.Price > 0)
            .Select(listing => listing.Price).DefaultIfEmpty(0m).Min();
        var operatingReserve = Math.Max(25_000m,
            company.CrewCount * company.CrewSalary + company.CrewWagesOwed + company.TaxesOwed +
            company.TariffsOwed + company.FuelCapacity * localMarket.FuelPrice +
            company.CargoCapacity * cheapestCargo);
        var excess = Math.Max(0m, company.Cash - operatingReserve);
        var profile = AiOpponentCatalog.ForCompany(company.Name);
        var investmentShare = Math.Clamp(0.35m * profile.RiskTolerance, 0.15m, 0.65m);
        var budget = GameMath.WholeKubars(excess * investmentShare);
        var costPerShare = localPrice * 1.01m;
        var shares = costPerShare <= 0m ? 0 : Math.Min(
            (int)(budget / costPerShare), company.MaximumStockPurchaseShares(localPrice));
        if (shares > 0)
        {
            var result = company.BuyShares(company.Planet, localPrice, shares);
            GameplayLogger.Log("AI STOCK DECISION", company.Name,
                $"trend={trend}; decision=buy; budget={budget:0}; shares={shares}; price={localPrice:0}; " +
                $"success={result.IsSuccessful}; result={result.Message}");
            if (result.IsSuccessful)
                LastTurnNews.Add($"{company.Name} bought {shares:N0} shares on the {company.Planet} Exchange.");
        }
        else GameplayLogger.Log("AI STOCK DECISION", company.Name,
            $"trend={trend}; decision=skip buy; budget={budget:0}; operatingReserve={operatingReserve:0}");
    }

    private void RunAiPlanetSpecial(CompanyState company)
    {
        if (company.LastSpecialWeek == Week) return;
        var roll = Math.Abs(GameMath.StableHash(Seed, Week.ToString(), company.Name, "AI planet special"));
        TradeResult? result = null;
        if (company.Planet.Equals("Xeen", StringComparison.OrdinalIgnoreCase))
        {
            var offer = roll % 4;
            var costA = Math.Abs(GameMath.StableHash(Seed, company.Name, "mechanic A")) % 100 + 1;
            var costB = Math.Abs(GameMath.StableHash(Seed, Week.ToString(), "mechanic B")) % 100 + 1;
            var cost = (decimal)(costA * costB * 6);
            result = offer switch
            {
                0 => company.TurbochargeAtCost(cost),
                1 => company.ExpandCargoBay(cost),
                2 => company.AddPassengerSeat(cost),
                _ => company.ExpandFuelTank(cost)
            };
        }
        else if (company.Planet.Equals("Pyke", StringComparison.OrdinalIgnoreCase) && company.BaseEngineSpeed < 10)
            result = ResolvePykeEnginePurchase(company);
        else if (company.Planet.Equals("Mira", StringComparison.OrdinalIgnoreCase))
            result = ResolveMiraBlessing(company, roll % 30 + 1);
        else if (company.Planet.Equals("Stye", StringComparison.OrdinalIgnoreCase))
            result = ResolveStyeAssistance(company, roll % 31 + 1);
        else if (company.Planet.Equals("Loro", StringComparison.OrdinalIgnoreCase))
        {
            var cost = company.CrewCount * 500m;
            if (company.TryPayFromCashAndBank(cost))
            {
                company.Luck = Math.Min(CompanyState.MaximumLuck, company.Luck + 10);
                result = TradeResult.Success("The crew received shore leave.");
            }
        }
        else if (company.Planet.Equals("Zile", StringComparison.OrdinalIgnoreCase))
            result = company.RequestZinnFavor(roll % 2 == 0);
        else if (company.Planet.Equals("Frac", StringComparison.OrdinalIgnoreCase))
        {
            result = company.AdjustInsurancePriceRange(roll % 3 == 0 ? -5 : roll % 3 == 1 ? 5 : 0);
            company.InsuranceCost = GenerateInsuranceQuote(company);
        }
        else if (company.Planet.Equals("Tilo", StringComparison.OrdinalIgnoreCase))
            result = company.Gamble(GameMath.WholeKubars(company.Cash * 0.05m), roll % 2 == 0);
        else if (company.Planet.Equals("Queg", StringComparison.OrdinalIgnoreCase))
        {
            var commodity = roll % CommodityCatalog.All.Length;
            var market = Markets[company.Planet];
            var quantity = Math.Min(company.CargoFree, roll % 20 + 5);
            result = company.BuySpecialCargo(commodity, quantity,
                Math.Max(1m, GameMath.WholeKubars(market.Listings[commodity].Price * 0.55m)));
        }
        else if (company.Planet.Equals("Ooom", StringComparison.OrdinalIgnoreCase))
        {
            var awardWindfall = Math.Abs((long)GameMath.StableHash(
                Seed, Week.ToString(), company.Name, "Ooom windfall")) % 2L == 0L;
            result = ResolveOoomFortune(company, awardWindfall);
        }
        else if (company.Planet.Equals("Hork", StringComparison.OrdinalIgnoreCase))
        {
            var level = roll % 5;
            if (level < 4)
            {
                company.CommodityAdvertising = Math.Max(company.CommodityAdvertising, level + 1);
                company.PassengerAdvertising = Math.Max(company.PassengerAdvertising, level + 1);
            }
            else company.Luck = Math.Max(CompanyState.MinimumLuck, company.Luck - 10);
            result = TradeResult.Success("The company attended a Hork media event.");
        }
        else if (company.Planet.Equals("Bass", StringComparison.OrdinalIgnoreCase))
        {
            // Bass provides information rather than a direct stat bonus. AI
            // companies use the same weekly broker consultation before their
            // stock-market decision immediately following this method.
            result = TradeResult.Success("The company consulted its Bass stock broker.");
        }
        else if (company.Planet.Equals("Nosh", StringComparison.OrdinalIgnoreCase))
        {
            var market = Markets[company.Planet];
            var companyRoll = Math.Abs(GameMath.StableHash(Seed, company.Name, "Nosh wholesaler")) % 100 + 1;
            var weeklyRoll = Math.Abs(GameMath.StableHash(Seed, Week.ToString(), "Nosh discount")) % 10 + 1;
            var price = GameMath.WholeKubars(market.FuelPrice * (1m - (weeklyRoll + companyRoll / 2m) / 100m));
            result = company.BuyFuelAtPrice(company.FuelCapacity - company.Fuel, price);
        }
        else if (company.Planet.Equals("Vexx", StringComparison.OrdinalIgnoreCase))
            result = ResolveVexxPetition(company, roll % 30 + 1);

        if (result is not null && result.IsSuccessful)
        {
            company.LastSpecialWeek = Week;
            LastTurnNews.Add($"{company.Name} used the special service on {company.Planet}.");
        }
        GameplayLogger.Log("AI SPECIAL DECISION", company.Name,
            $"planet={company.Planet}; roll={roll}; attempted={result is not null}; " +
            $"success={result?.IsSuccessful ?? false}; result={result?.Message ?? "No special action"}");
    }

    private AiTradePlan BuildAiTradePlan(
        CompanyState company, AiOpponentProfile profile, decimal additionalReserve, bool insured,
        IReadOnlySet<int>? excludedPurchases = null)
    {
        var local = Markets[company.Planet];
        AiTradePlan? best = null;
        foreach (var destination in Planets.Where(planet =>
                     !planet.Equals(company.Planet, StringComparison.OrdinalIgnoreCase)))
        {
            var fuelRequired = TravelRules.FuelCost(company.Planet, destination, company, Planets, Week);
            var fuelToBuy = Math.Max(0m, fuelRequired - company.Fuel);
            var fuelPurchaseCost = fuelToBuy * local.FuelPrice;
            var landingFees = Facilities.Where(facility =>
                    facility.Planet.Equals(destination, StringComparison.OrdinalIgnoreCase) &&
                    !facility.OwnerName.Equals(company.Name, StringComparison.OrdinalIgnoreCase))
                .Sum(facility => facility.Fee);
            var commodityAdvertisingReserve = CanSafelyFundAutomaticCommodityAdvertising(company, profile)
                ? company.AdvertisingCost(6)
                : 0m;
            var mandatoryReserve = AiOperatingReserve(company, profile) + additionalReserve +
                                   commodityAdvertisingReserve +
                                   fuelPurchaseCost + landingFees;
            var cashBudget = Math.Max(0m, company.Cash - mandatoryReserve);
            var cargoSpace = company.CargoFree;
            var purchases = new List<AiCargoPurchase>();
            // Cargo already aboard is part of the routing decision. Without
            // this, an AI could carry an unprofitable lot forever while routing
            // solely for whatever it might buy next.
            var expectedProfit = company.Cargo.Sum(cargo =>
                cargo.Value.Quantity *
                (Markets[destination].Listings[cargo.Key].Price *
                 (100m - company.ExportTariffRate) / 100m - cargo.Value.AverageCost)) -
                fuelPurchaseCost - landingFees;
            if (company.Warehouses.TryGetValue(destination, out var destinationWarehouse))
            {
                var exportFactor = (100m - company.ExportTariffRate) / 100m;
                expectedProfit += destinationWarehouse.Sum(stored =>
                {
                    var netSale = Markets[destination].Listings[stored.Key].Price * exportFactor;
                    return netSale >= stored.Value.AverageCost * 1.20m
                        ? stored.Value.Quantity * (netSale - stored.Value.AverageCost)
                        : 0m;
                });
            }

            var opportunities = Enumerable.Range(0, CommodityCatalog.All.Length)
                .Where(commodity => excludedPurchases is null || !excludedPurchases.Contains(commodity))
                .Select(commodity =>
                {
                    var listing = local.Listings[commodity];
                    var destinationPrice = Markets[destination].Listings[commodity].Price;
                    var netPerTon = destinationPrice * (100m - company.ExportTariffRate) / 100m -
                                    listing.Price * (100m + company.ImportTariffRate) / 100m;
                    return new
                    {
                        Commodity = commodity,
                        Listing = listing,
                        Available = company.AccessibleCommodityQuantity(local, commodity),
                        NetPerTon = netPerTon
                    };
                })
                .Where(item => item.Available > 0 && item.Listing.Price > 0m && item.NetPerTon > 0m)
                .OrderByDescending(item => item.NetPerTon)
                .ThenBy(item => item.Commodity);

            foreach (var opportunity in opportunities)
            {
                if (cargoSpace <= 0 || cashBudget < opportunity.Listing.Price) break;
                var affordable = (int)decimal.Floor(cashBudget / opportunity.Listing.Price);
                var quantity = Math.Min(cargoSpace, Math.Min(opportunity.Available, affordable));
                if (quantity <= 0) continue;
                purchases.Add(new AiCargoPurchase(opportunity.Commodity, quantity, opportunity.Listing.Price));
                cargoSpace -= quantity;
                cashBudget -= quantity * opportunity.Listing.Price;
                expectedProfit += quantity * opportunity.NetPerTon;
            }

            if (!insured)
            {
                var cargoValue = purchases.Sum(purchase => purchase.Quantity * purchase.Price);
                var expectedLoss = ExpectedAiUninsuredLoss(company, destination, cargoValue);
                expectedProfit -= expectedLoss / Math.Max(0.25m, profile.RiskTolerance);
            }

            var candidate = new AiTradePlan(destination, purchases, fuelRequired, expectedProfit);
            GameplayLogger.Log("AI PLAN CANDIDATE", company.Name,
                $"destination={destination}; insured={insured}; reserve={mandatoryReserve:0}; " +
                $"fuelRequired={fuelRequired:0.###}; fuelCost={fuelPurchaseCost:0}; landingFees={landingFees:0}; " +
                $"expectedProfit={expectedProfit:0}; purchases=[{string.Join(',', purchases.Select(purchase =>
                    $"{purchase.CommodityIndex}:{purchase.Quantity}@{purchase.Price:0}"))}]");
            if (best is null || candidate.ExpectedProfit > best.Value.ExpectedProfit)
                best = candidate;
        }

        if (best is not null) return best.Value;
        return new AiTradePlan(company.Planet, [], 0m, 0m);
    }

    private HashSet<int> LiquidateAiCargoForBetterTrades(CompanyState company, PlanetMarket localMarket)
    {
        var liquidated = new HashSet<int>();
        var exportFactor = (100m - company.ExportTariffRate) / 100m;
        var importFactor = (100m + company.ImportTariffRate) / 100m;
        var replacements = Enumerable.Range(0, CommodityCatalog.All.Length)
            .SelectMany(commodity => Planets
                .Where(destination => !destination.Equals(company.Planet, StringComparison.OrdinalIgnoreCase))
                .Select(destination =>
                {
                    var listing = localMarket.Listings[commodity];
                    var profit = Markets[destination].Listings[commodity].Price * exportFactor -
                                 listing.Price * importFactor;
                    return new { Commodity = commodity, listing.Quantity, listing.Price, Profit = profit };
                }))
            .Where(item => item.Quantity > 0 && item.Profit > 0m)
            .OrderByDescending(item => item.Profit)
            .ToArray();

        foreach (var cargo in company.Cargo.ToArray())
        {
            var localSale = localMarket.Listings[cargo.Key].Price * exportFactor;
            var bestFutureSale = Planets
                .Where(destination => !destination.Equals(company.Planet, StringComparison.OrdinalIgnoreCase))
                .Select(destination => Markets[destination].Listings[cargo.Key].Price * exportFactor)
                .DefaultIfEmpty(localSale)
                .Max();

            // Selling at the current stop is always sensible if it is already
            // the cargo's best market. Otherwise compare the value sacrificed
            // by selling here with the profit available from replacing it.
            var directCashOut = localSale >= bestFutureSale;
            var replacement = replacements.FirstOrDefault(item =>
                item.Profit > bestFutureSale - localSale &&
                company.Cash + localMarket.Listings[cargo.Key].Price + company.AvailableSafeUnionCredit >= item.Price);
            if (!directCashOut && replacement is null) continue;

            var quantity = directCashOut
                ? cargo.Value.Quantity
                : Math.Min(cargo.Value.Quantity, replacement!.Quantity);
            if (quantity <= 0) continue;
            var soldBelowCost = localSale < cargo.Value.AverageCost;
            var result = company.Sell(localMarket, cargo.Key, quantity);
            if (result.IsSuccessful)
            {
                liquidated.Add(cargo.Key);
                var reason = soldBelowCost && replacement is not null
                    ? " to free capital and space for a stronger trade"
                    : string.Empty;
                LastTurnNews.Add($"{company.Name} sold {quantity} tons of " +
                                 $"{CommodityCatalog.All[cargo.Key].Name} on {company.Planet}{reason}.");
            }
        }
        return liquidated;
    }

    private void LiquidateProfitableWarehouseCargo(CompanyState company, PlanetMarket localMarket)
    {
        if (!company.Warehouses.TryGetValue(company.Planet, out var warehouse) || warehouse.Count == 0)
            return;
        var exportFactor = (100m - company.ExportTariffRate) / 100m;
        foreach (var stored in warehouse.ToArray())
        {
            while (stored.Value.Quantity > 0)
            {
                var netSale = localMarket.Listings[stored.Key].Price * exportFactor;
                if (netSale < stored.Value.AverageCost * 1.20m || company.CargoFree <= 0) break;
                var quantity = Math.Min(stored.Value.Quantity, company.CargoFree);
                var retrieved = company.RetrieveCargo(company.Planet, stored.Key, quantity);
                if (!retrieved.IsSuccessful) break;
                var sold = company.Sell(localMarket, stored.Key, quantity);
                GameplayLogger.Log("AI WAREHOUSE SELL", company.Name,
                    $"planet={company.Planet}; commodity={stored.Key}; quantity={quantity}; " +
                    $"storedCost={stored.Value.AverageCost:0}; marketPrice={localMarket.Listings[stored.Key].Price:0}; " +
                    $"success={sold.IsSuccessful}");
                if (!sold.IsSuccessful) break;
                LastTurnNews.Add($"{company.Name} sold {quantity:N0} tons of stored " +
                                 $"{CommodityCatalog.All[stored.Key].Name} on {company.Planet}.");
            }
        }
    }

    private void StockpileCheapWarehouseCargo(CompanyState company, AiOpponentProfile profile,
        PlanetMarket localMarket, AiTradePlan plan, decimal preBorrowLiquidFunds, decimal insuranceReserve)
    {
        company.Warehouses.TryAdd(company.Planet, []);
        var warehouse = company.Warehouses[company.Planet];
        var warehouseFree = company.WarehouseCapacity - warehouse.Values.Sum(lot => lot.Quantity);
        if (warehouseFree <= 0) return;

        var plannedCargoCost = plan.Purchases.Sum(purchase => purchase.Quantity * purchase.Price);
        var fuelToBuy = Math.Max(0m, plan.FuelRequired - company.Fuel);
        var fuelCost = fuelToBuy * localMarket.FuelPrice;
        var landingFees = Facilities.Where(facility =>
                facility.Planet.Equals(plan.Destination, StringComparison.OrdinalIgnoreCase) &&
                !facility.OwnerName.Equals(company.Name, StringComparison.OrdinalIgnoreCase))
            .Sum(facility => facility.Fee);
        var advertisingReserve = company.AdvertisingCost(6) * 2m;
        var protectedFunds = AiOperatingReserve(company, profile) + plannedCargoCost + fuelCost +
                             landingFees + insuranceReserve + advertisingReserve;
        var surplus = Math.Max(0m, preBorrowLiquidFunds - protectedFunds);
        if (surplus <= 0m)
        {
            GameplayLogger.Log("AI WAREHOUSE", company.Name,
                $"planet={company.Planet}; decision=skip; genuineSurplus=false; protected={protectedFunds:0}; " +
                $"preBorrowLiquid={preBorrowLiquidFunds:0}");
            return;
        }

        var highestCommodityValue = CommodityCatalog.All.Max(commodity => commodity.MaximumPrice);
        var visits = company.PlanetVisitCounts.GetValueOrDefault(company.Planet);
        var visitAffinity = Math.Clamp(visits / 5m, 0m, 1m);
        var firePenalty = WarehouseFireAversion(company, company.Planet);
        var importFactor = (100m + company.ImportTariffRate) / 100m;

        var candidates = Enumerable.Range(0, CommodityCatalog.All.Length)
            .Select(commodity =>
            {
                var definition = CommodityCatalog.All[commodity];
                var listing = localMarket.Listings[commodity];
                var minimum = PlanetMarket.MinimumPrice(commodity, Level);
                var range = Math.Max(1m, definition.MaximumPrice - minimum);
                var cheapness = Math.Clamp((definition.MaximumPrice - listing.Price) / range, 0m, 1m);
                var value = definition.MaximumPrice / highestCommodityValue;
                var willingness = 0.55m * cheapness + 0.25m * value + 0.20m * visitAffinity - firePenalty +
                                  (profile.RiskTolerance - 1m) * 0.05m;
                var available = company.AccessibleCommodityQuantity(localMarket, commodity);
                return new { Commodity = commodity, Listing = listing, Cheapness = cheapness,
                    Willingness = willingness, Available = available };
            })
            .Where(candidate => candidate.Available > 0 && candidate.Cheapness >= 0.75m &&
                                candidate.Willingness >= 0.70m)
            .OrderByDescending(candidate => candidate.Willingness)
            .ThenByDescending(candidate => candidate.Commodity)
            .ToArray();

        foreach (var candidate in candidates)
        {
            if (warehouseFree <= 0 || surplus <= 0m) break;
            var effectivePrice = candidate.Listing.Price * importFactor;
            if (effectivePrice <= 0m) continue;
            var alreadyStored = warehouse.GetValueOrDefault(candidate.Commodity)?.Quantity ?? 0;
            var commodityLimit = Math.Max(1, company.WarehouseCapacity * 3 / 5);
            var quantity = Math.Min(candidate.Available,
                Math.Min(warehouseFree, Math.Min(commodityLimit - alreadyStored,
                    (int)decimal.Floor(surplus / effectivePrice))));
            if (quantity <= 0) continue;

            var grossCost = candidate.Listing.Price * quantity;
            var cashShortfall = Math.Max(0m, grossCost - company.Cash);
            if (cashShortfall > 0m)
                company.WithdrawFromBank(Math.Min(cashShortfall, company.Bank));
            var bought = company.Buy(localMarket, candidate.Commodity, quantity);
            if (!bought.IsSuccessful) continue;
            var stored = company.StoreCargo(company.Planet, candidate.Commodity, quantity);
            if (!stored.IsSuccessful) continue;
            surplus -= effectivePrice * quantity;
            warehouseFree -= quantity;
            GameplayLogger.Log("AI WAREHOUSE BUY", company.Name,
                $"planet={company.Planet}; commodity={candidate.Commodity}; quantity={quantity}; " +
                $"price={candidate.Listing.Price:0}; cheapness={candidate.Cheapness:0.000}; " +
                $"willingness={candidate.Willingness:0.000}; firePenalty={firePenalty:0.000}; surplusRemaining={surplus:0}");
            LastTurnNews.Add($"{company.Name} stored {quantity:N0} tons of " +
                             $"{CommodityCatalog.All[candidate.Commodity].Name} on {company.Planet}.");
        }
    }

    private decimal WarehouseFireAversion(CompanyState company, string planet)
    {
        decimal penalty = 0m;
        foreach (var pair in company.AiWarehouseExperiences)
        {
            var experience = pair.Value;
            var uninsuredFires = Math.Max(0, experience.FireCount - experience.InsuredFireCount);
            var age = Math.Max(0, Week - experience.LastFireWeek);
            var decay = Math.Max(0m, 1m - age / 40m);
            var local = pair.Key.Equals(planet, StringComparison.OrdinalIgnoreCase);
            var basePenalty = local
                ? uninsuredFires * 0.55m + experience.InsuredFireCount * 0.10m
                : uninsuredFires * 0.20m + experience.InsuredFireCount * 0.04m;
            var lossSeverity = experience.ActualLoss <= 0m ? 0m : Math.Min(0.20m,
                experience.ActualLoss / Math.Max(1m, Math.Abs(NetWorthOf(company)) + experience.ActualLoss));
            penalty += (basePenalty + (local ? lossSeverity : lossSeverity * 0.35m)) * decay;
        }
        return Math.Min(1m, penalty);
    }

    private static decimal AiOperatingReserve(CompanyState company, AiOpponentProfile profile)
    {
        var debtInterest = GameMath.WholeKubars(company.Loan * company.StandardLoanRate / 100m) +
                           GameMath.WholeKubars(company.ZinnLoan * company.ZinnRate / 100m);
        var crewRedThreshold = company.CrewCount * company.CrewSalary * 4m;
        var crewPayment = company.CrewWagesOwed >= crewRedThreshold
            ? company.CrewWagesOwed
            : 0m;
        var taxesOwed = company.TaxesOwed + company.TariffsOwed;
        var taxPayment = company.IsTaxAuditRisk
            ? taxesOwed
            : 0m;
        var obligations = crewPayment + taxPayment + debtInterest;
        return GameMath.WholeKubars(obligations * profile.ReserveMultiplier);
    }

    private void EnsureAiWorkingCapital(
        CompanyState company, AiOpponentProfile profile, PlanetMarket localMarket)
    {
        var cheapestProfitableCargo = localMarket.Listings
            .Select((listing, commodity) => new { Listing = listing, Commodity = commodity })
            .Where(item => item.Listing.Quantity > 0 && item.Listing.Price > 0m &&
                           Planets.Any(destination =>
                               !destination.Equals(company.Planet, StringComparison.OrdinalIgnoreCase) &&
                               Markets[destination].Listings[item.Commodity].Price > item.Listing.Price))
            .Select(item => item.Listing.Price)
            .DefaultIfEmpty(0m)
            .Min();
        var cargoCapital = cheapestProfitableCargo <= 0m
            ? 0m
            : Math.Min(75_000m, cheapestProfitableCargo * company.CargoCapacity);
        // Advertising is deliberately not borrowed as working capital. Large
        // cargo ships reserve level 6 from cash they already have, but do not
        // spiral into debt merely to keep an advertising campaign running.
        var targetCash = AiOperatingReserve(company, profile) + cargoCapital;
        var shortfall = Math.Max(0m, targetCash - company.Cash);
        if (shortfall <= 0m) return;

        var savingsWithdrawal = Math.Min(shortfall, company.Bank);
        if (savingsWithdrawal > 0m)
        {
            company.WithdrawFromBank(savingsWithdrawal);
            shortfall -= savingsWithdrawal;
        }
        if (shortfall > 0m)
            company.BorrowFromTradersUnion(Math.Min(shortfall, company.AvailableSafeUnionCredit));
    }

    private void BuyAiFuelForJourney(
        CompanyState company, PlanetMarket localMarket, decimal fuelRequired)
    {
        var requiredPurchase = Math.Max(0m, fuelRequired - company.Fuel);
        if (requiredPurchase > 0m)
        {
            EnsureAiCash(company, requiredPurchase * localMarket.FuelPrice);
            company.BuyFuel(localMarket, requiredPurchase);
        }

        // Fill the remaining tank only at a genuinely cheap stop: the local
        // price must fall within the cheapest third of active planet prices.
        // Optional fuel never creates debt or displaces the journey purchase.
        var orderedPrices = Markets.Values.Select(market => market.FuelPrice).Order().ToArray();
        if (orderedPrices.Length == 0) return;
        var cheapCutoff = orderedPrices[(orderedPrices.Length - 1) / 3];
        if (localMarket.FuelPrice > cheapCutoff || company.Fuel >= company.FuelCapacity) return;

        var affordable = localMarket.FuelPrice <= 0m
            ? company.FuelCapacity - company.Fuel
            : decimal.Floor(company.Cash / localMarket.FuelPrice * 10m) / 10m;
        var extra = Math.Min(company.FuelCapacity - company.Fuel, affordable);
        if (extra > 0m) company.BuyFuel(localMarket, extra);
    }

    private static void EnsureAiCash(CompanyState company, decimal requiredCash)
    {
        var shortfall = Math.Max(0m, requiredCash - company.Cash);
        var savingsWithdrawal = Math.Min(shortfall, company.Bank);
        if (savingsWithdrawal > 0m)
        {
            company.WithdrawFromBank(savingsWithdrawal);
            shortfall -= savingsWithdrawal;
        }
        if (shortfall > 0m)
            company.BorrowFromTradersUnion(Math.Min(shortfall, company.AvailableSafeUnionCredit));
    }

    private bool ShouldAiBuyInsurance(
        CompanyState company, AiOpponentProfile profile, string destination, decimal cargoValue)
    {
        if (company.InsuranceLevel > 0) return false;
        if (company.InsuranceCost <= 0m) company.InsuranceCost = GenerateInsuranceQuote(company);
        if (company.Cash < company.InsuranceCost) return false;
        var expectedUninsuredLoss = ExpectedAiUninsuredLoss(company, destination, cargoValue);
        return company.InsuranceCost <= expectedUninsuredLoss * profile.InsuranceAppetite;
    }

    private decimal ExpectedAiUninsuredLoss(
        CompanyState company, string destination, decimal cargoValue)
    {
        var badEventChance = 1m - (decimal)Math.Clamp(
            company.Luck / 100d,
            CompanyState.MinimumLuck / 100d,
            CompanyState.MaximumLuck / 100d);
        if (destination.Equals(WeatherPlanet, StringComparison.OrdinalIgnoreCase) &&
            (WeatherCode <= 10 || WeatherCode is >= 61 and <= 70))
            badEventChance = Math.Min(0.85m, badEventChance + 0.25m);
        var cargoExposure = cargoValue * 0.35m;
        var likelyShipDamage = company.ShipTons * 50m;
        return badEventChance * (cargoExposure + likelyShipDamage);
    }

    private static (int Advertising, decimal TicketPrice) ChooseAiPassengerPlan(
        CompanyState company, AiOpponentProfile profile, string destination)
    {
        var passengerSpecialist = company.PassengerCapacity >= 12;
        company.AiPassengerExperiences.TryGetValue(destination, out var experience);

        // Passenger specialists such as the Worm Shuttle begin aggressively;
        // they do not need to lose several journeys discovering that their
        // large cabin can justify maximum advertising and a premium fare.
        var specialistHasTestedCampaign = experience is { HasBestResult: true } &&
                                          (experience.BestAdvertising > 0 ||
                                           experience.BestTicketPrice >= 3_000m);
        if (passengerSpecialist && !specialistHasTestedCampaign &&
            company.Cash >= company.AdvertisingCost(6))
            return (6, 4_000m);

        if (experience is { HasBestResult: true })
        {
            var advertising = experience.BestAdvertising;
            var fare = experience.BestTicketPrice;
            var occupancy = company.PassengerCapacity == 0
                ? 0m
                : experience.BestPassengers / (decimal)company.PassengerCapacity;

            // Reuse the most profitable result, but regularly probe one nearby
            // price or advertising level. Successful probes become the new
            // remembered maximum on the next visit to this destination.
            switch (experience.Visits % 5)
            {
                case 1:
                    fare += occupancy >= 0.70m ? 500m : -500m;
                    break;
                case 2:
                    advertising += occupancy < 0.80m ? 1 : -1;
                    break;
                case 3:
                    fare += occupancy >= 0.90m ? 1_000m : 250m;
                    break;
                case 4:
                    advertising += occupancy < 0.55m ? 1 : -1;
                    fare += occupancy < 0.55m ? -250m : 0m;
                    break;
            }

            var minimumFare = passengerSpecialist ? 3_500m : 100m;
            var minimumAdvertising = passengerSpecialist ? 4 : 0;
            return (Math.Clamp(advertising, minimumAdvertising, 6),
                Math.Clamp(fare, minimumFare, 10_000m));
        }

        var bestLevel = 0;
        var bestFare = 1_000m;
        var bestValue = decimal.MinValue;
        for (var fare = 1_000m; fare <= 10_000m; fare += 250m)
        {
            for (var level = 0; level <= 6; level++)
            {
                var value = company.ExpectedPassengerDemand(level, fare) * fare *
                            (100m - company.PassengerTaxRate) / 100m * profile.PassengerFocus -
                            company.AdvertisingCost(level);
                if (value <= bestValue) continue;
                bestValue = value;
                bestLevel = level;
                bestFare = fare;
            }
        }
        return (bestLevel, bestFare);
    }

    private static int ChooseAiCommodityAdvertising(
        CompanyState company, AiOpponentProfile profile, decimal expectedTradeProfit)
    {
        if (CanSafelyFundAutomaticCommodityAdvertising(company, profile)) return 6;

        // Commodity advertising reflects the ship model's intended trading role.
        // Current hold usage and later cargo-capacity upgrades must not make an AI
        // abruptly change its advertising personality.
        var capacityWeight = Math.Clamp(company.BaseCargoCapacity / 100m, 0.5m, 2m);
        var budget = Math.Max(0m, expectedTradeProfit) * 0.10m * capacityWeight * profile.CommodityFocus;
        var level = 0;
        for (var candidate = 1; candidate <= 6; candidate++)
        {
            if (company.AdvertisingCost(candidate) > budget) break;
            level = candidate;
        }
        return level;
    }

    private static bool UsesAutomaticMaximumCommodityAdvertising(CompanyState company) =>
        company.BaseCargoCapacity >= AutomaticMaximumCommodityAdvertisingCapacity;

    private static bool CanSafelyFundAutomaticCommodityAdvertising(
        CompanyState company, AiOpponentProfile profile) =>
        UsesAutomaticMaximumCommodityAdvertising(company) &&
        company.Cash >= AiOperatingReserve(company, profile) + company.AdvertisingCost(6);

    private void PopulateAiAuctionBids(AuctionOffer offer, Random random)
    {
        foreach (var ai in Companies.Where(company => !company.IsHuman && !company.IsBankrupt))
        {
            var netWorth = NetWorthOf(ai);
            var profile = AiOpponentCatalog.ForCompany(ai.Name);
            decimal bid;
            if (offer.IsShipUpgrade)
            {
                var band = ShipAuctionBidBand(netWorth);
                bid = RandomAuctionAmount(random, band.Lower, band.Upper);
                if (random.Next(6) == 0) bid *= 2m;
                bid *= profile.RiskTolerance;

                // The original gives a smaller computer ship a one-in-six
                // chance to react to the current human high bid. Keep that
                // catch-up pressure, but never let it exceed funds available
                // under the same financing rules used by human bidders.
                var largestHumanShip = Companies
                    .Where(company => company.IsHuman && !company.IsBankrupt)
                    .Select(company => company.ShipTons)
                    .DefaultIfEmpty(0)
                    .Max();
                var humanHighBid = offer.Bids
                    .Where(pair => Companies.Any(company => company.IsHuman &&
                        company.Name.Equals(pair.Key, StringComparison.OrdinalIgnoreCase)))
                    .Select(pair => pair.Value)
                    .DefaultIfEmpty(0m)
                    .Max();
                if (random.Next(6) == 0 && ai.ShipTons < largestHumanShip && bid < humanHighBid)
                    bid = humanHighBid + random.Next(1, 20_001);
            }
            else
            {
                var factorBand = FacilityAuctionFactorBand(netWorth);
                bid = RandomAuctionAmount(random,
                    offer.Fee * factorBand.Lower, offer.Fee * factorBand.Upper);
                if (random.Next(6) == 0) bid *= 2m;
                bid *= profile.RiskTolerance;
                bid *= ai.FacilityAuctionVisitMultiplier(offer.Planet);
            }

            var availableFunds = ai.Cash + ai.Bank + ai.AvailableSafeUnionCredit;
            offer.Bids[ai.Name] = GameMath.WholeKubars(Math.Clamp(bid, 0m, availableFunds));
            GameplayLogger.Log("AI AUCTION", ai.Name,
                $"auction={offer.Name}; planet={offer.Planet}; shipUpgrade={offer.IsShipUpgrade}; " +
                $"rawBid={bid:0}; availableFunds={availableFunds:0}; finalBid={offer.Bids[ai.Name]:0}");
        }
    }

    private static (decimal Lower, decimal Upper) ShipAuctionBidBand(decimal netWorth)
    {
        if (netWorth < -100_000m) return (5_000m, 10_000m);
        if (netWorth < -50_000m) return (7_500m, 15_000m);
        if (netWorth < 0m) return (10_000m, 20_000m);
        if (netWorth <= 100_000m) return (12_500m, 50_000m);
        if (netWorth <= 1_000_000m)
        {
            var band = Math.Clamp((int)decimal.Ceiling(netWorth / 100_000m), 2, 10);
            return (10_000m + band * 2_500m, 45_000m + band * 5_000m);
        }
        if (netWorth <= 2_000_000m) return (37_500m, 100_000m);
        if (netWorth <= 3_000_000m) return (40_000m, 110_000m);
        if (netWorth <= 4_000_000m) return (50_000m, 120_000m);
        if (netWorth <= 5_000_000m) return (60_000m, 130_000m);
        if (netWorth <= 6_000_000m) return (70_000m, 140_000m);
        if (netWorth <= 7_000_000m) return (80_000m, 150_000m);
        if (netWorth <= 8_000_000m) return (90_000m, 160_000m);
        if (netWorth <= 9_000_000m) return (100_000m, 170_000m);
        if (netWorth <= 10_000_000m) return (110_000m, 180_000m);
        if (netWorth <= 100_000_000m) return (120_000m, 190_000m);
        return (130_000m, 200_000m);
    }

    private static (decimal Lower, decimal Upper) FacilityAuctionFactorBand(decimal netWorth)
    {
        if (netWorth < -100_000m) return (2.5m, 5m);
        if (netWorth < -50_000m) return (5m, 10m);
        if (netWorth < 0m) return (7.5m, 15m);
        if (netWorth <= 100_000m) return (10m, 20m);
        if (netWorth > 1_000_000m) return (20m, 40m);
        var band = Math.Clamp((int)decimal.Ceiling(netWorth / 100_000m), 2, 10);
        return (9m + band, 18m + band * 2m);
    }

    private static decimal RandomAuctionAmount(Random random, decimal lower, decimal upper)
    {
        var minimum = (int)GameMath.WholeKubars(lower);
        var maximum = (int)GameMath.WholeKubars(upper);
        return random.Next(minimum, maximum + 1);
    }

    private readonly record struct AiCargoPurchase(int CommodityIndex, int Quantity, decimal Price);

    private readonly record struct AiTradePlan(
        string Destination,
        IReadOnlyList<AiCargoPurchase> Purchases,
        decimal FuelRequired,
        decimal ExpectedProfit);

}
