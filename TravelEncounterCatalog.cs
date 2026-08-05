using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenTradeEngine;

/// <summary>Interactive travel encounters transcribed from the original Travel2 event library.</summary>
public static class TravelEncounterCatalog
{
    public enum TeeterUpgrade
    {
        Turbocharge,
        CargoBay,
        PassengerSeat,
        FuelTank
    }

    private static readonly string[] Advice =
    [
        "Never invest in the stock market until you've paid off all your debts.",
        "Always try to fill up your ship's cargo bay. It's better to make a small profit than no profit at all.",
        "Always look for commodities with the highest potential profit margin. Cheap goods may not cover expenses.",
        "Don't pay your crew until you absolutely have to. That money could be in the bank earning interest.",
        "Don't pay your taxes until you absolutely have to. That money could be in the bank earning interest.",
        "Only invest in stocks which are on an upward trend.",
        "Don't purchase a larger ship until you've paid off all your debts.",
        "Don't buy facilities until you've paid off all your debts.",
        "If Mr. Zinn or the Traders' Union offers to extend your credit limit, always say yes!",
        "Always obey the law.",
        "Make sure you have a faster engine than your competitors. Speed makes a difference.",
        "Don't use the warehouse until you've paid off all your debts. Stored goods cost money while debt earns interest.",
        "Always read the daily news and weather reports. If they predict trouble, buy insurance before travelling.",
        "Always try the planet specials. They tend to do more good than harm.",
        "Luck runs in streaks. If you're on a bad luck streak, buy insurance before travelling.",
        "Always fill up on fuel when the price is cheap.",
        "Raise passenger ticket prices and advertise. High prices plus advertising can bring in more money.",
        "Never allow your crew to go on strike. When they demand payment, you'd better pay them.",
        "Mr. Zinn typically offers a better rate than the Traders' Union. Pay Union debt first when possible.",
        "Buy commodities in the highest price range with the most room for profit; cargo space is limited."
    ];

    public static int AdviceCount => Advice.Length;

    public static TravelEventResult FreeAdvice(int index) =>
        new("Free Advice",
            "Mulls, a reclusive retired business consultant, comes out of his shell to offer you free advice:\n\n“" +
            Advice[Math.Abs(index) % Advice.Length] + "”", true, "MULLS.SWF", "MULLS.MP3");

    /// <summary>
    /// Travel2's independent one-in-forty Quaso Mutta encounter. It happens
    /// before the normal good/bad event roll and can redirect the destination.
    /// </summary>
    public static TravelEventResult QuasoRedirect(CompanyState company, string suggestedPlanet)
    {
        var originalPlanet = company.Planet;
        return Choice("Quaso Mutta",
            $"The ghost-like spirit of the venerated Quaso Mutta warns you not to travel to {originalPlanet}, " +
            $"or your luck will change for the worse. Instead, you should travel to {suggestedPlanet} immediately.\n\n" +
            $"Do you take this advice and travel to {suggestedPlanet}?",
            $"Travel to {suggestedPlanet}", "Ignore Advice", true,
            accepted =>
            {
                if (!accepted)
                    return new TravelEventResult("Advice Ignored",
                        $"You ignore Quaso Mutta and continue to {originalPlanet}.", company.LastTravelEventGood,
                        "QUASO.SWF", "BLESSING.MP3", null, company.Luck);
                company.Planet = suggestedPlanet;
                company.PassengerAdvertising = 0;
                return new TravelEventResult("Destination Changed",
                    $"You follow Quaso Mutta's warning and redirect the ship to {suggestedPlanet}.", true,
                    "QUASO.SWF", "BLESSING.MP3", null, 85);
            }, "QUASO.SWF", "BLESSING.MP3");
    }

    /// <summary>
    /// Travel2 good event 2. The original does not replace the chosen ship
    /// model: it creates a 200-ton-larger version and applies a model-specific
    /// package of capacity, crew, engine and insurance changes.
    /// </summary>
    public static TravelEventResult NewShipOffer(CompanyState company, decimal cost)
    {
        cost = Math.Clamp(GameMath.WholeKubars(cost), 25_000m, 100_000m);
        var oldTons = company.ShipTons;
        var shortfall = Math.Max(0m, cost - company.Cash - company.Bank);
        var aiAccepts = company.Loan + shortfall <= company.StandardCreditLimit + 25_000m;
        return Choice("New Ship For Sale",
            $"You receive a one-time offer from the Traders' Union to trade in your {oldTons:N0}-ton ship " +
            $"for a {oldTons + 200:N0}-ton ship. This will cost {cost:N0} kubars, and the Union will raise " +
            "your credit limit by 25,000 kubars. If necessary, it will loan you the money to cover the cost.\n\n" +
            "Do you want to do it?",
            "Buy Larger Ship", "Decline", aiAccepts,
            accepted =>
            {
                if (!accepted) return Declined("The one-time ship offer was declined.",
                    $"SHIP{company.ShipNumber}.SWF", "GOOD3.MP3");

                company.PayMandatoryExpense(cost);
                company.StandardCreditLimit += 25_000m;
                ApplyShipExpansion(company);
                return new TravelEventResult("New Ship Purchased",
                    $"Your {company.ShipModel} is expanded from {oldTons:N0} to {company.ShipTons:N0} tons for " +
                    $"{cost:N0} kubars. The Traders' Union credit limit is now {company.StandardCreditLimit:N0} kubars.",
                    true, $"SHIP{company.ShipNumber}.SWF", "GOOD3.MP3");
            }, $"SHIP{company.ShipNumber}.SWF", "GOOD3.MP3");
    }

    /// <summary>Travel2 good event 3: fifty tons of global warehouse space.</summary>
    public static TravelEventResult WarehouseExpansionOffer(CompanyState company, decimal cost)
    {
        cost = Math.Clamp(GameMath.WholeKubars(cost), 15_000m, 50_000m);
        var oldSpace = company.WarehouseCapacity;
        var shortfall = Math.Max(0m, cost - company.Cash - company.Bank);
        var mostUsedWarehouse = company.Warehouses.Values
            .Select(warehouse => warehouse.Values.Sum(lot => lot.Quantity))
            .DefaultIfEmpty(0)
            .Max();
        var needsMoreSpace = mostUsedWarehouse >= Math.Max(1, company.WarehouseCapacity * 3 / 4);
        var aiAccepts = needsMoreSpace &&
                        company.Loan + shortfall <= company.StandardCreditLimit + 25_000m;
        return Choice("Warehouse Space For Sale",
            $"For only {cost:N0} kubars, you can increase your warehouse space from {oldSpace:N0} to " +
            $"{oldSpace + 50:N0} tons on every planet. The Traders' Union will also raise your credit limit by " +
            "25,000 kubars and, if necessary, loan you the money to cover the cost.\n\nDo you want to do it?",
            "Buy Warehouse Space", "Decline", aiAccepts,
            accepted =>
            {
                if (!accepted) return Declined("The warehouse-space offer was declined.", "WAREHOUS.SWF", "GOOD3.MP3");
                company.PayMandatoryExpense(cost);
                company.WarehouseCapacity += 50;
                company.InsurancePriceRange += 5;
                company.StandardCreditLimit += 25_000m;
                return new TravelEventResult("Warehouse Space Purchased",
                    $"Warehouse capacity increases from {oldSpace:N0} to {company.WarehouseCapacity:N0} tons on every " +
                    $"planet. The Traders' Union credit limit is now {company.StandardCreditLimit:N0} kubars.",
                    true, "WAREHOUS.SWF", "GOOD3.MP3");
            }, "WAREHOUS.SWF", "GOOD3.MP3");
    }

    /// <summary>
    /// Applies the original 200-ton, ship-model-specific expansion package.
    /// Both the travelling Traders' Union offer and the ship auction award use
    /// this method so their resulting ship statistics remain identical.
    /// </summary>
    public static void ApplyShipExpansion(CompanyState company)
    {
        company.ShipTons += 200;
        var upgrade = ShipExpansionFor(company.ShipNumber);
        company.BaseEngineSpeed += upgrade.Engine;
        company.FuelCapacityBonus += upgrade.Fuel;
        company.CargoCapacityBonus += upgrade.Cargo;
        company.PassengerCapacityBonus += upgrade.Passengers;
        company.CrewCapacityBonus += upgrade.Crew;
        company.InsurancePriceRange += upgrade.Insurance;
    }

    private static (int Engine, int Fuel, int Cargo, int Passengers, int Crew, int Insurance)
        ShipExpansionFor(int shipNumber) => shipNumber switch
        {
            1 => (1, 5, 50, 4, 2, 6),
            2 => (0, 10, 60, 3, 2, 8),
            3 => (0, 30, 40, 3, 1, 2),
            4 => (0, 10, 65, 5, 3, 8),
            5 => (0, 15, 50, 2, 1, 6),
            6 => (0, 15, 50, 4, 2, 6),
            7 => (1, 10, 40, 3, 2, 6),
            8 => (0, 15, 55, 3, 2, 6),
            9 => (0, 15, 45, 5, 2, 4),
            10 => (0, 10, 75, 1, 1, 10),
            11 => (0, 10, 40, 8, 6, 2),
            _ => (0, 15, 55, 4, 3, 6)
        };

    public static TravelEventResult ZinnLoanExtension(CompanyState company)
    {
        const decimal amount = 50_000m;
        var newLoan = company.ZinnLoan + amount;
        // The loan itself rises by 50,000; the decompiled original raises the
        // corresponding credit ceiling by 100,000.
        var newLimit = company.ZinnCreditLimit + 100_000m;
        return Choice("Need More Money?",
            $"Because Mr. Zinn feels {company.Name} is a good investment, he offers to increase the loan by " +
            $"{amount:N0} kubars.\n\nIf accepted, the loan will become {newLoan:N0} kubars. Its weekly rate " +
            $"will remain {company.ZinnRate:0.#}%, and the credit limit will rise to {newLimit:N0} kubars.\n\n" +
            "Do you want to accept his offer?",
            "Accept Loan", "Decline", company.Cash < 100_000m,
            accepted =>
            {
                if (!accepted) return Declined("Mr. Zinn's offer was declined.", "ZINN_N.SWF", "ZINN.MP3");
                company.Cash += amount;
                company.ZinnLoan += amount;
                company.ZinnCreditLimit += 100_000m;
                return new TravelEventResult("Loan Increased",
                    $"Mr. Zinn transfers {amount:N0} kubars to {company.Name}. The new loan balance is " +
                    $"{company.ZinnLoan:N0} kubars and the credit limit is {company.ZinnCreditLimit:N0} kubars.",
                    true, "ZINN_N.SWF", "ZINN.MP3");
            }, "ZINN_N.SWF", "ZINN.MP3");
    }

    public static TravelEventResult ExoticForSale(CompanyState company)
    {
        const decimal price = 100m;
        var exoticIndex = CommodityCatalog.All.Length - 1;
        var affordable = (int)(company.Cash / price);
        var quantity = Math.Min(company.CargoFree, affordable);
        return Choice("Exotic For Sale",
            $"Nectum, a foreign commodities wholesaler, offers to sell you {quantity} tons of Exotic for only " +
            $"{price:N0} kubars per ton.\n\nNectum explains that his trip has been delayed three months, and if " +
            "he does not sell the Exotic now, it will go bad before he returns home.\n\nAre you interested?",
            "Buy Exotic", "Decline", quantity > 0,
            accepted =>
            {
                if (!accepted) return Declined("Nectum continues on his way with the Exotic.", "NECTUM.SWF", "NECTUM.MP3");
                var total = quantity * price;
                var purchase = company.BuySpecialCargo(exoticIndex, quantity, price);
                if (!purchase.IsSuccessful)
                    return new TravelEventResult("Purchase Failed", purchase.Message, false);
                return new TravelEventResult("Exotic Purchased",
                    $"You purchase {quantity} tons of Exotic for {total:N0} kubars.", true, "MONEY_N.SWF", "GOOD2.MP3");
            }, "NECTUM.SWF", "NECTUM.MP3");
    }

    public static TravelEventResult CaptainLeahyOffer(CompanyState company)
    {
        var paid = company.Cargo.Values.Sum(lot => lot.AverageCost * lot.Quantity);
        var offer = GameMath.WholeKubars(paid * 2m);
        return Choice("Purchase Offer",
            $"Captain Leahy, an ex-pirate turned respectable businessman, offers to buy all of your cargo for " +
            $"{offer:N0} kubars—twice as much as you paid for it.\n\nHe assures you this is entirely legal and " +
            "you will make a guaranteed profit.\n\nDo you accept Leahy's offer?",
            "Accept Offer", "Decline", offer > 0m,
            accepted =>
            {
                if (!accepted) return Declined("Captain Leahy's offer was declined.", "LEAHY.SWF", "LEAHY.MP3");
                var tons = company.Cargo.Values.Sum(lot => lot.Quantity);
                company.Cargo.Clear();
                company.Cash += offer;
                return new TravelEventResult("Cargo Sold",
                    $"Captain Leahy buys all {tons} tons of cargo for {offer:N0} kubars.", true,
                    "MONEY_N.SWF", "GOOD2.MP3");
            }, "LEAHY.SWF", "LEAHY.MP3");
    }

    /// <summary>Travel2 event 17: four-times cargo cost, with a one-in-five police check and seven-times-cost fine.</summary>
    public static TravelEventResult ScooterJayOffer(CompanyState company, bool policeCatch)
    {
        var tons = company.Cargo.Values.Sum(lot => lot.Quantity);
        var basis = company.Cargo.Values.Sum(lot => lot.AverageCost * lot.Quantity);
        if (basis == 0m) basis = tons * 300m;
        var offer = basis * 4m;
        return Choice("Scooter Jay",
            $"Scooter Jay, a smuggler from Pata Pata Pita, offers {offer:N0} kubars for all your cargo. " +
            "Imperial law forbids this trade.\n\nDo you break the law and accept?",
            "Accept Illegal Offer", "Decline", tons > 0,
            accepted =>
            {
                if (!accepted) return Declined("You refuse Scooter Jay's illegal offer.", "SCOOTER.SWF", "SCOOTER.MP3");
                company.Cargo.Clear();
                company.Cash += offer;
                if (!policeCatch)
                    return new TravelEventResult("Illegal Sale Complete",
                        $"Scooter Jay pays {offer:N0} kubars and disappears into space.", true,
                        "SCOOTER.SWF", "SCOOTER.MP3");
                var fine = basis * 7m;
                company.PayMandatoryExpense(fine);
                return new TravelEventResult("You Are Caught",
                    $"The Imperial Police were watching. Your cargo is gone and you are fined {fine:N0} kubars. " +
                    "Voyager's Insurance does not cover criminal activity.", false, "POLICE.SWF", "POLICE.MP3");
            }, "SCOOTER.SWF", "SCOOTER.MP3");
    }

    /// <summary>Travel2 event 18: a hold-full Exotic swap with a one-in-five confiscation check.</summary>
    public static TravelEventResult HandsCargoOffer(CompanyState company, bool policeCatch)
    {
        var tons = company.Cargo.Values.Sum(lot => lot.Quantity);
        var oldCost = company.Cargo.Values.Sum(lot => lot.AverageCost * lot.Quantity);
        var exotic = CommodityCatalog.All.Length - 1;
        return Choice("Hands",
            $"Hands, a well-known crook, offers to exchange your cargo for {company.CargoCapacity} tons of Exotic. " +
            "You know the goods are stolen.\n\nDo you break the law and accept?",
            "Accept Illegal Trade", "Decline", tons > 0,
            accepted =>
            {
                if (!accepted) return Declined("You refuse Hands' stolen goods.", "HANDS.SWF", "HANDS.MP3");
                company.Cargo.Clear();
                if (policeCatch)
                {
                    company.PayMandatoryExpense(50_000m);
                    return new TravelEventResult("You Are Caught",
                        "The Imperial Police confiscate all cargo and fine you 50,000 kubars. Voyager's Insurance " +
                        "does not cover criminal activity.", false, "POLICE.SWF", "POLICE.MP3");
                }
                company.Cargo[exotic] = new CargoLot
                {
                    Quantity = company.CargoCapacity,
                    AverageCost = GameMath.WholeKubars(oldCost / company.CargoCapacity)
                };
                return new TravelEventResult("Stolen Exotic Aboard",
                    $"Hands swaps your cargo for {company.CargoCapacity} tons of Exotic.", true,
                    "HANDS.SWF", "HANDS.MP3");
            }, "HANDS.SWF", "HANDS.MP3");
    }

    /// <summary>Travel2 event 19: 15..35 times ship mass, with a one-in-four six-times repayment.</summary>
    public static TravelEventResult CurtonianLoan(CompanyState company, int costRoll, bool repays)
    {
        costRoll = Math.Clamp(costRoll, 15, 35);
        var loan = costRoll * company.ShipTons;
        return Choice("Ship Stranded",
            $"Curtonian Plus asks to borrow {loan:N0} kubars to repair his stranded ship and promises to repay " +
            "you when he gets home.\n\nDo you lend him the money?",
            "Make Loan", "Decline", company.Cash + company.Bank >= loan,
            accepted =>
            {
                if (!accepted) return Declined("You leave Curtonian Plus to await another ship.", "CURTIS.SWF", "CURTIS.MP3");
                company.PayMandatoryExpense(loan);
                if (!repays)
                    return new TravelEventResult("No Repayment",
                        "Curtonian Plus never sends the promised repayment.", false, "CURTIS.SWF", "CURTIS.MP3");
                var repayment = loan * 6m;
                company.Cash += repayment;
                return new TravelEventResult("Curtonian Plus",
                    $"Curtonian's wealthy family repays your kindness with {repayment:N0} kubars.", true,
                    "CURTIS.SWF", "GOOD2.MP3");
            }, "CURTIS.SWF", "CURTIS.MP3");
    }

    /// <summary>Travel2 event 20: Quist takes the investment; the original contains no later repayment.</summary>
    public static TravelEventResult QuistInvestment(CompanyState company, int costRoll)
    {
        costRoll = Math.Clamp(costRoll, 25, 75);
        var investment = costRoll * company.ShipTons;
        return Choice("Quist",
            $"Quist, a high-flying financier, asks you to invest {investment:N0} kubars in a sure-fire scheme " +
            "which he claims will make a tenfold profit.\n\nDo you invest?",
            "Invest", "Decline", false,
            accepted =>
            {
                if (!accepted) return Declined("You decline Quist's scheme.", "QUIST.SWF", "QUIST.MP3");
                company.PayMandatoryExpense(investment);
                return new TravelEventResult("Investment Made",
                    $"Quist departs with your {investment:N0}-kubar investment. No repayment is recorded.", false,
                    "QUIST.SWF", "QUIST.MP3", SkipOutcomeScreen: true);
            }, "QUIST.SWF", "QUIST.MP3");
    }

    /// <summary>Travel2 event 21: 15..35 times ship mass, one-in-three flop, otherwise three-times repayment.</summary>
    public static TravelEventResult WobblerSponsorship(CompanyState company, int costRoll, bool succeeds)
    {
        costRoll = Math.Clamp(costRoll, 15, 35);
        var cost = costRoll * company.ShipTons;
        return Choice("Supporting Art",
            $"The Wobbler asks you to sponsor an avant-garde theatrical production for {cost:N0} kubars. " +
            "It may be an artistic masterpiece, but it may not make any money.\n\nDo you sponsor it?",
            "Sponsor Production", "Decline", company.Cash + company.Bank >= cost,
            accepted =>
            {
                if (!accepted) return Declined("You decline to sponsor The Wobbler.", "WOBBLER.SWF", "WOBBLER.MP3");
                company.PayMandatoryExpense(cost);
                if (!succeeds)
                    return new TravelEventResult("Bummer",
                        "The experimental play is a complete flop, and Emperor Dred bans it.", false,
                        "DRED.SWF", "BAD5.MP3");
                var repayment = cost * 3m;
                company.Cash += repayment;
                return new TravelEventResult("Blockbuster!",
                    $"The play is a surprise hit. The Wobbler repays you {repayment:N0} kubars.", true,
                    "DRED.SWF", "GOOD2.MP3");
            }, "WOBBLER.SWF", "WOBBLER.MP3");
    }

    /// <summary>
    /// Travel2 event 22. Brow's original upper roll is 50. OpenTradeEngine
    /// progressively unlocks that range by limiting its upper bound to the
    /// current week while preserving the original minimum roll of 10. Brow
    /// charges the resulting roll times the buyer's ship mass. A
    /// successful job hits an anonymous random subset of active competitors
    /// for a ship-mass-adjusted amount between three and four-and-a-half times
    /// the base damage. The one-in-five police outcome adds an uncovered
    /// four-times-fee fine.
    /// </summary>
    public static TravelEventResult BrowSabotage(CompanyState company,
        IReadOnlyList<CompanyState> competitors, int feeRoll, bool policeCatch, int damageSeed)
    {
        feeRoll = Math.Clamp(feeRoll, 10, 50);
        var fee = feeRoll * company.ShipTons;
        var eligibleTargets = EligibleSabotageTargets(company, competitors);
        // The AI must be able to cover Brow's fee and the entire four-fee
        // police fine from liquid funds. It will not gamble its credit licence.
        var aiAccepts = eligibleTargets.Length > 0 && company.Cash + company.Bank >= fee * 5m;
        return Choice("Business Proposition",
            $"Brow, an industrial spy, offers to sabotage your competitors for {fee:N0} kubars. " +
            "He assures you he is an expert at nasty tricks.\n\nDo you accept his offer?",
            "Hire Brow", "Decline", aiAccepts,
            accepted =>
            {
                if (!accepted) return Declined("You decline Brow's proposition.", "SABOTAGE.SWF", "SABOTAGE.MP3");
                company.PayMandatoryExpense(fee);
                if (policeCatch)
                {
                    var fine = fee * 4m;
                    company.PayMandatoryExpense(fine);
                    return new TravelEventResult("You Are Caught",
                        $"The Imperial Police catch the scheme and fine you {fine:N0} kubars in addition to Brow's fee. " +
                        "Voyager's Insurance does not cover industrial sabotage.", false, "POLICE.SWF", "POLICE.MP3");
                }

                var targets = SelectSabotageTargets(company, competitors,
                    new Random(GameMath.StableHash(damageSeed, company.Name, "Brow targets")));
                var reports = new string[targets.Length];
                for (var index = 0; index < targets.Length; index++)
                {
                    var target = targets[index];
                    var baseDamage = Math.Max(1, (int)GameMath.WholeKubars(target.ShipTons * fee / company.ShipTons));
                    var random = new Random(GameMath.StableHash(damageSeed, company.Name, target.Name, "Brow sabotage"));
                    var rawDamage = random.Next(baseDamage * 2, baseDamage * 3 + 1);
                    var damage = GameMath.WholeKubars(rawDamage * 1.5m);
                    target.PayMandatoryExpense(damage);
                    reports[index] = $"{target.Name} loses {damage:N0} kubars";
                    QueuePublicSabotageNotice(competitors, target,
                        $"{target.Name} was hit by an act of industrial sabotage, causing {damage:N0} kubars in losses.",
                        "SABOTAGE.MP3");
                }
                return new TravelEventResult("Sabotage Complete",
                    string.Join("; ", reports) + ".", true, "SABOTAGE.SWF", "SABOTAGE.MP3");
            }, "SABOTAGE.SWF", "SABOTAGE.MP3");
    }

    /// <summary>
    /// Travel2 event 42. Hapa Jillo's original upper roll is 70.
    /// OpenTradeEngine progressively unlocks that range by limiting its upper
    /// bound to the current week while preserving the original minimum roll of
    /// 20. The syndicate charges the resulting roll times
    /// ship mass and attacks an anonymous random subset using the same
    /// 3.0..4.5-times ship-scaled damage formula as Brow. A one-in-five police
    /// outcome adds a four-times-fee fine, one travel delay and an explicit
    /// luck value of 45.
    /// </summary>
    public static TravelEventResult HapaJilloSabotage(CompanyState company,
        IReadOnlyList<CompanyState> competitors, int feeRoll, bool policeCatch, int damageSeed)
    {
        feeRoll = Math.Clamp(feeRoll, 20, 70);
        var fee = feeRoll * company.ShipTons;
        var eligibleTargets = EligibleSabotageTargets(company, competitors);
        var aiAccepts = eligibleTargets.Length > 0 && company.Cash + company.Bank >= fee * 5m;
        return Choice("Thug's Proposition",
            $"The Hapa Jillo Crime Syndicate offers to make life extremely difficult for your competitors " +
            $"if you pay them {fee:N0} kubars. If necessary, the Traders' Union will cover the shortfall.\n\n" +
            "Do you want to accept this evil offer?",
            "Hire Hapa Jillos", "Decline", aiAccepts,
            accepted =>
            {
                if (!accepted) return Declined("You reject the Hapa Jillo Crime Syndicate's offer.",
                    "HAPA.SWF", "HAPA.MP3");
                company.PayMandatoryExpense(fee);
                if (policeCatch)
                {
                    var fine = fee * 4m;
                    company.PayMandatoryExpense(fine);
                    company.TravelDelay++;
                    return new TravelEventResult("You Are Caught",
                        $"The Imperial Police uncover the scheme and fine you {fine:N0} kubars in addition to " +
                        "the syndicate's fee. Voyager's Insurance does not cover criminal conspiracy.",
                        false, "POLICE.SWF", "POLICE.MP3", null, 45);
                }

                var targets = SelectSabotageTargets(company, competitors,
                    new Random(GameMath.StableHash(damageSeed, company.Name, "Hapa targets")));
                var reports = new string[targets.Length];
                for (var index = 0; index < targets.Length; index++)
                {
                    var target = targets[index];
                    var baseDamage = Math.Max(1, (int)GameMath.WholeKubars(target.ShipTons * fee / company.ShipTons));
                    var random = new Random(GameMath.StableHash(damageSeed, company.Name, target.Name,
                        "Hapa Jillo sabotage"));
                    var rawDamage = random.Next(baseDamage * 2, baseDamage * 3 + 1);
                    var damage = GameMath.WholeKubars(rawDamage * 1.5m);
                    target.PayMandatoryExpense(damage);
                    var report = HapaDamageReport(target, damage, random.Next(13));
                    reports[index] = report;
                    QueuePublicSabotageNotice(competitors, target, report, "HAPA.MP3");
                }
                return new TravelEventResult("Competitors Attacked",
                    string.Join("\n\n", reports), true, "HAPA.SWF", "HAPA.MP3");
            }, "HAPA.SWF", "HAPA.MP3");
    }

    private static CompanyState[] EligibleSabotageTargets(CompanyState attacker,
        IReadOnlyList<CompanyState> competitors) => competitors
        .Where(target => !ReferenceEquals(target, attacker) && !target.IsBankrupt &&
                         !target.Name.Equals(attacker.Name, StringComparison.OrdinalIgnoreCase))
        .ToArray();

    private static void QueuePublicSabotageNotice(IReadOnlyList<CompanyState> companies,
        CompanyState target, string message, string audioAsset)
    {
        var notice = new TurnNotice($"{target.Name} Was Sabotaged!", message,
            GameSession.CompanyPortraitAsset(target), audioAsset, true);
        foreach (var human in companies.Where(company => company.IsHuman && !company.IsBankrupt))
            human.PendingTurnNotices.Add(notice);
    }

    private static CompanyState[] SelectSabotageTargets(CompanyState attacker,
        IReadOnlyList<CompanyState> competitors, Random random)
    {
        var eligible = EligibleSabotageTargets(attacker, competitors);
        if (eligible.Length <= 1) return eligible;

        // For seven active companies this produces 3..5 victims: floor(7/2)
        // through 7-2. One competitor is always hit when it is the only target.
        var activeCount = eligible.Length + 1;
        var minimum = Math.Clamp(activeCount / 2, 1, eligible.Length);
        var maximum = Math.Clamp(activeCount - 2, minimum, eligible.Length);
        var targetCount = random.Next(minimum, maximum + 1);
        for (var index = eligible.Length - 1; index > 0; index--)
        {
            var swap = random.Next(index + 1);
            (eligible[index], eligible[swap]) = (eligible[swap], eligible[index]);
        }
        return eligible.Take(targetCount).ToArray();
    }

    private static string HapaDamageReport(CompanyState target, decimal damage, int variant) => variant switch
    {
        0 => $"Hapa Jillo street thugs vandalize {target.Name}, causing {damage:N0} kubars in damage.",
        1 => $"A corrupt banker tricks {target.Name} into a dubious stock fund, costing {damage:N0} kubars.",
        2 => $"A Hapa Jillo cruiser steals cargo from {target.Name}, causing a {damage:N0}-kubar loss.",
        3 => $"A remote-control droid explodes aboard {target.Name}, causing {damage:N0} kubars in damage.",
        4 => $"Hapa Jillos attack {target.Name}'s warehouse, causing {damage:N0} kubars in damage.",
        5 => $"A corrupt merchant sells {target.Name} defective goods worth {damage:N0} kubars.",
        6 => $"A mob attacks {target.Name}'s crew, leaving {damage:N0} kubars in hospital costs.",
        7 => $"Tampered fuel ruins {target.Name}'s engine and costs {damage:N0} kubars to repair.",
        8 => $"An illegal-employment scandal costs {target.Name} {damage:N0} kubars in legal fees.",
        9 => $"A sick passenger extracts a {damage:N0}-kubar settlement from {target.Name}.",
        10 => $"A Hapa Jillo battle ship damages {target.Name} by {damage:N0} kubars.",
        11 => $"The Hapa Jillo Merchants' Association costs {target.Name} {damage:N0} kubars in legal fees.",
        _ => $"The syndicate's interference costs {target.Name} {damage:N0} kubars."
    };

    /// <summary>Travel2 event 23 is rigged: accepting always loses half the advertised prize.</summary>
    public static TravelEventResult YoyoCoinFlip(CompanyState company, int prizeRoll)
    {
        prizeRoll = Math.Clamp(prizeRoll, 1, 35);
        var prize = prizeRoll * company.ShipTons;
        var loss = GameMath.WholeKubars(prize / 2m);
        return Choice("Flipping Coin",
            $"Yoyo offers a coin toss. Tails pays you {prize:N0} kubars; heads costs you {loss:N0} kubars.\n\n" +
            "Do you accept Yoyo's offer?",
            "Flip Coin", "Decline", false,
            accepted =>
            {
                if (!accepted) return Declined("You refuse to gamble with Yoyo.", "YOYO.SWF", "YOYO.MP3");
                company.PayMandatoryExpense(loss);
                return new TravelEventResult("Heads!!!",
                    $"You lose {loss:N0} kubars. It never pays to gamble with a professional.", false,
                    "DIME.PNG", "YOYO.MP3");
            }, "YOYO.SWF", "YOYO.MP3");
    }

    /// <summary>Travel2 event 24 clears all cargo and explicitly sets good-event probability to 85.</summary>
    public static TravelEventResult LimpusCharity(CompanyState company)
    {
        var tons = company.Cargo.Values.Sum(lot => lot.Quantity);
        return Choice("Charity Request",
            "Limpus asks you to donate all cargo aboard your ship to the Kukubian Children's Fund.\n\n" +
            "Do you give your valuable cargo to the needy children?",
            "Donate Cargo", "Decline", company.Luck < 30 && tons > 0,
            accepted =>
            {
                if (!accepted) return Declined("You keep your cargo.", "LIMPUS.SWF", "LIMPUS.MP3");
                company.Cargo.Clear();
                return new TravelEventResult("Blessed!",
                    $"You donate all {tons} tons. The Grand Sages grant your company their highest blessing.",
                    true, "MONK_N.SWF", "BLESSING.MP3", null, 85);
            }, "LIMPUS.SWF", "LIMPUS.MP3");
    }

    /// <summary>
    /// Travel2 event 25. Sleg pays a fixed 150,000 kubars for a three-kuarp
    /// downgrade. OpenTradeEngine treats turbocharges as permanent ship upgrades,
    /// so they remain installed when the base engine changes.
    /// </summary>
    public static TravelEventResult SlegEngineTrade(CompanyState company)
    {
        const decimal payment = 150_000m;
        var replacementSpeed = Math.Max(1, company.BaseEngineSpeed - 3);
        return Choice("Trade Engine?",
            $"Sleg, an inter-galactic commodities broker, asks if you will trade your " +
            $"{company.BaseEngineSpeed}-kuarp engine for his {replacementSpeed}-kuarp engine.\n\n" +
            "He has a valuable shipment of Uliss Leaves that he must transport to Outer Glosser right away, " +
            $"and he is willing to pay you {payment:N0} kubars if you make the trade.\n\nWill you do it?",
            "Make Trade", "Decline", company.BaseEngineSpeed >= 5 && company.Cash < 100_000m,
            accepted =>
            {
                if (!accepted) return Declined("You keep your current engine.", "SLEG.SWF", "HELP.MP3");
                var oldEffectiveSpeed = company.EngineSpeed;
                company.BaseEngineSpeed = replacementSpeed;
                company.Cash += payment;
                return new TravelEventResult("Engine Traded",
                    $"Sleg pays {payment:N0} kubars. Your {oldEffectiveSpeed}-kuarp effective engine is replaced " +
                    $"by a {replacementSpeed}-kuarp engine while your {company.Turbocharges} turbocharge upgrade(s) remain installed.",
                    true, "SLEG.SWF", "GOOD2.MP3");
            }, "SLEG.SWF", "HELP.MP3");
    }

    /// <summary>Travel2 event 26 is a non-interactive visit from Emperor Dred.</summary>
    public static TravelEventResult RoyalVisitor(CompanyState company) =>
        new("Royal Visitor",
            $"Supreme Commander Dred Nicolson, Emperor of the New Realm, pays a visit to {company.Name}.\n\n" +
            "He says the company is critical to the success of Kukubia, and he will do his best to support " +
            "free trade and open up new markets.", true, "DRED.SWF", "DRED.MP3");

    /// <summary>Travel2 event 27 uses the original 25..75 times ship-mass award.</summary>
    public static TravelEventResult IsoGift(CompanyState company, int awardRoll)
    {
        awardRoll = Math.Clamp(awardRoll, 25, 75);
        var award = awardRoll * company.ShipTons;
        return Choice("Helping Monk",
            "You come across Iso, a painfully shy monk who disdains material possessions. Pilgrims often give " +
            $"Iso extraordinary sums of money, but she has no use for it.\n\nWill you relieve Iso of {award:N0} kubars?",
            "Accept Gift", "Decline", true,
            accepted =>
            {
                if (!accepted) return Declined("You leave Iso's gift for another traveller.", "ISO.SWF", "ISO.MP3");
                company.Cash += award;
                return new TravelEventResult("A Generous Gift",
                    $"Iso gives {company.Name} {award:N0} kubars.", true, "ISO.SWF", "GOOD2.MP3");
            }, "ISO.SWF", "ISO.MP3");
    }

    /// <summary>Travel2 event 28: R.J. Raffety buys an entire holding at 115% of market value.</summary>
    public static TravelEventResult RaffetyShareOffer(CompanyState company, string exchange, decimal marketPrice)
    {
        var shares = company.Shares.GetValueOrDefault(exchange);
        var paid = company.ShareAverageCosts.GetValueOrDefault(exchange) * shares;
        var proceeds = GameMath.WholeKubars(shares * marketPrice * 1.15m);
        var profit = proceeds - paid;
        return Choice("Chance To Cash Out",
            $"R.J. Raffety offers to buy all {shares:N0} of your shares on the {exchange} Exchange for the " +
            $"current market price plus 15%. You paid {paid:N0} kubars and will receive {proceeds:N0} kubars, " +
            $"a profit of {profit:N0} kubars.\n\nAre you interested in selling?",
            "Sell Shares", "Decline", shares > 0 && profit >= 0m,
            accepted =>
            {
                if (!accepted) return Declined("You keep your shares.", "RJ.SWF", "RJ.MP3");
                company.Shares.Remove(exchange);
                company.ShareAverageCosts.Remove(exchange);
                company.Cash += proceeds;
                return new TravelEventResult("Shares Sold",
                    $"R.J. buys all {shares:N0} {exchange} shares for {proceeds:N0} kubars.", true,
                    "RJ.SWF", "GOOD2.MP3");
            }, "RJ.SWF", "RJ.MP3");
    }

    /// <summary>Travel2 event 29: Nebbit sells shares at 20% below the current exchange price.</summary>
    public static TravelEventResult NebbitShareOffer(CompanyState company, string exchange, decimal marketPrice)
    {
        var offerPrice = Math.Max(1m, GameMath.WholeKubars(marketPrice * 0.8m));
        // Decompiled Travel2 event 29 invests one quarter of liquid funds,
        // capped at 2,000,000 kubars. It does not spend the entire cash and
        // savings balance.
        var investmentBudget = Math.Min(2_000_000m,
            GameMath.WholeKubars((company.Cash + company.Bank) * 0.25m));
        var quantity = (int)decimal.Floor(investmentBudget / offerPrice);
        var total = quantity * offerPrice;
        return Choice("Chance To Invest",
            $"Nebbit, a big-time stock broker, offers to arrange the purchase of {quantity:N0} shares on the " +
            $"{exchange} Exchange for 20% less than the current market price. The deal costs {total:N0} kubars.\n\n" +
            "Are you interested?",
            "Buy Shares", "Decline", quantity > 0,
            accepted =>
            {
                if (!accepted) return Declined("You decline Nebbit's offer.", "NEBBIT.SWF", "NEBBIT.MP3");
                if (quantity <= 0 || total > company.Cash + company.Bank)
                    return new TravelEventResult("Offer Unavailable", "You no longer have enough money for the deal.",
                        false, "NEBBIT.SWF", "NEBBIT.MP3");
                var cashPaid = Math.Min(company.Cash, total);
                company.Cash -= cashPaid;
                company.Bank -= total - cashPaid;
                var oldShares = company.Shares.GetValueOrDefault(exchange);
                var oldCost = company.ShareAverageCosts.GetValueOrDefault(exchange) * oldShares;
                company.Shares[exchange] = oldShares + quantity;
                company.ShareAverageCosts[exchange] = GameMath.WholeKubars(
                    (oldCost + total) / (oldShares + quantity));
                return new TravelEventResult("Special Investment",
                    $"You purchase {quantity:N0} {exchange} shares for {total:N0} kubars.", true,
                    "NEBBIT.SWF", "GOOD2.MP3");
            }, "NEBBIT.SWF", "NEBBIT.MP3");
    }

    /// <summary>Travel2 event 30: 5,000 kubars for every empty passenger seat.</summary>
    public static TravelEventResult TatilusPassengerOffer(CompanyState company)
    {
        var passengers = Math.Max(0, company.PassengerCapacity - company.Passengers);
        var payment = passengers * 5_000m;
        return Choice("Emergency Service Request",
            $"Tatilus, an emergency passenger broker, has {passengers} passengers desperate to get to " +
            $"{company.Planet} this week. If you take them aboard, he will pay 5,000 kubars per passenger, " +
            $"or {payment:N0} kubars in total.\n\nAre you interested?",
            "Take Passengers", "Decline", passengers > 0,
            accepted =>
            {
                if (!accepted) return Declined("You leave Tatilus to find another ship.", "TATILUS.SWF", "TATILUS.MP3");
                company.Passengers += passengers;
                company.PassengersPickedUp = true;
                company.Cash += payment;
                return new TravelEventResult("Emergency Passengers Boarded",
                    $"All {passengers} passengers board, filling the ship. Tatilus pays {payment:N0} kubars.",
                    true, "TATILUS.SWF", "GOOD2.MP3");
            }, "TATILUS.SWF", "TATILUS.MP3");
    }

    /// <summary>Travel2 event 31: Gurttle pays 3,500 kubars per ton for half the current fuel.</summary>
    public static TravelEventResult GurttleFuelOffer(CompanyState company)
    {
        var fuelSold = company.Fuel / 2m;
        var payment = GameMath.WholeKubars(fuelSold * 3_500m);
        var percentFull = company.FuelCapacity <= 0 ? 0m : decimal.Floor(company.Fuel * 100m / company.FuelCapacity);
        return Choice("Gas Guzzler",
            $"Gurttle, a notorious fuel fiend, offers to pay {payment:N0} kubars if you allow him to gulp down " +
            $"half the fuel in your tank. Your tank is currently {percentFull:N0}% full.\n\n" +
            "Do you accommodate Gurttle's biological needs?",
            "Sell Half", "Decline", company.Fuel > 1m,
            accepted =>
            {
                if (!accepted) return Declined("You refuse to feed Gurttle.", "GURTTLE.SWF", "GURTTLE.MP3");
                company.Fuel -= fuelSold;
                company.Cash += payment;
                return new TravelEventResult("Fuel Sold",
                    $"Gurttle consumes {fuelSold:0.#} tons of fuel and pays {payment:N0} kubars.", true,
                    "GURTTLE.SWF", "GOOD2.MP3");
            }, "GURTTLE.SWF", "GURTTLE.MP3");
    }

    /// <summary>Travel2 event 32: Lord 104 offers three times the original cargo cost.</summary>
    public static TravelEventResult Lord104CargoOffer(CompanyState company)
    {
        var tons = company.Cargo.Values.Sum(lot => lot.Quantity);
        var paid = company.Cargo.Values.Sum(lot => lot.AverageCost * lot.Quantity);
        if (paid == 0m) paid = tons * 300m;
        var payment = paid * 3m;
        return Choice("Prince's Offer",
            $"Lord 104, the 104th Prince of the Leaper Colony, offers to buy all of your cargo for " +
            $"{payment:N0} kubars - three times as much as you paid for it.\n\nDo you accept Lord 104's offer?",
            "Accept Offer", "Decline", tons > 0,
            accepted =>
            {
                if (!accepted) return Declined("You keep your cargo.", "LORD.SWF", "LORD.MP3");
                company.Cargo.Clear();
                company.Cash += payment;
                return new TravelEventResult("Cargo Sold",
                    $"Lord 104 buys all {tons} tons of cargo for {payment:N0} kubars.", true,
                    "LORD.SWF", "GOOD2.MP3");
            }, "LORD.SWF", "LORD.MP3");
    }

    /// <summary>
    /// Travel2 event 34: Squowk replaces all cargo with a hold full of one
    /// other commodity. The original redistributes the former total purchase
    /// cost over the new cargo, so the swap does not manufacture book value.
    /// </summary>
    public static TravelEventResult SquowkCargoSwap(CompanyState company, int commodityIndex)
    {
        commodityIndex = Math.Clamp(commodityIndex, 0, CommodityCatalog.All.Length - 1);
        var oldTons = company.Cargo.Values.Sum(lot => lot.Quantity);
        var oldCost = company.Cargo.Values.Sum(lot => lot.AverageCost * lot.Quantity);
        var offeredTons = company.CargoCapacity;
        var commodity = CommodityCatalog.All[commodityIndex].Name;
        return Choice("Swapping Cargo",
            $"Squowk, a migrating commodities merchant, offers to exchange all {oldTons} tons of cargo on your " +
            $"ship for {offeredTons} tons of {commodity}. Squowk has a surplus under his wings and cannot wait " +
            "to unload it.\n\nAre you interested in making a trade?",
            "Swap Cargo", "Decline", oldTons > 0,
            accepted =>
            {
                if (!accepted) return Declined("You keep your current cargo.", "SQUOWK.SWF", "SQUOWK.MP3");
                company.Cargo.Clear();
                company.Cargo[commodityIndex] = new CargoLot
                {
                    Quantity = offeredTons,
                    AverageCost = offeredTons == 0 ? 0m : GameMath.WholeKubars(oldCost / offeredTons)
                };
                return new TravelEventResult("Cargo Swapped",
                    $"Squowk exchanges your cargo for {offeredTons} tons of {commodity}.", true,
                    "SQUOWK.SWF", "GOOD2.MP3");
            }, "SQUOWK.SWF", "SQUOWK.MP3");
    }

    /// <summary>
    /// Travel2 event 37. The original computes the offer as the player's fixed
    /// 1..100 random value multiplied by that week's 1..100 newsData value and
    /// by six. The fixed value modulo four also selects the offered upgrade.
    /// </summary>
    public static TravelEventResult TeeterOffer(CompanyState company, int companyRandom, int weeklyNewsData)
    {
        companyRandom = Math.Clamp(companyRandom, 1, 100);
        weeklyNewsData = Math.Clamp(weeklyNewsData, 1, 100);
        var cost = companyRandom * weeklyNewsData * 6m;
        var upgrade = (companyRandom % 4) switch
        {
            1 => TeeterUpgrade.Turbocharge,
            2 => TeeterUpgrade.CargoBay,
            3 => TeeterUpgrade.PassengerSeat,
            _ => TeeterUpgrade.FuelTank
        };
        var (heading, work, resultHeading, resultText) = upgrade switch
        {
            TeeterUpgrade.Turbocharge => ("Turbocharging Engine", "turbocharge your ship's engine",
                "Engine Upgraded!", "turbocharged your ship's engine"),
            TeeterUpgrade.CargoBay => ("Expanding Cargo Bay", "expand your ship's cargo bay by 10 tons",
                "Cargo Bay Expanded", "expanded your ship's cargo bay by 10 tons"),
            TeeterUpgrade.PassengerSeat => ("Adding Passenger Seat", "expand your ship's passenger capacity by 1 seat",
                "More Passenger Space", "expanded your ship's passenger capacity by 1 seat"),
            _ => ("Upping Tank Capacity", "expand your ship's fuel tank capacity by 5 tons",
                "Bigger Fuel Tank", "expanded your ship's fuel tank capacity by 5 tons")
        };

        return Choice(heading,
            $"You come across Teeter, a travelling repairman. Teeter says that he has the necessary parts to " +
            $"{work}. This will only cost you {cost:N0} kubars.\n\nAre you interested?",
            "Yes", "No", company.Cash >= cost,
            accepted =>
            {
                if (!accepted) return Declined("You decline Teeter's offer.", "TEETER.SWF", "TEETER.MP3");
                var transaction = upgrade switch
                {
                    TeeterUpgrade.Turbocharge => company.TurbochargeAtCost(cost),
                    TeeterUpgrade.CargoBay => company.ExpandCargoBay(cost),
                    TeeterUpgrade.PassengerSeat => company.AddPassengerSeat(cost),
                    _ => company.ExpandFuelTank(cost)
                };
                if (!transaction.IsSuccessful)
                    return new TravelEventResult("Not Enough Money", transaction.Message, false,
                        "TEETER.SWF", "TEETER.MP3");
                return new TravelEventResult(resultHeading,
                    $"Teeter is brilliant! In no time, he has {resultText} for {cost:N0} kubars.",
                    true, "TEETER.SWF", "TEETER2.MP3");
            }, "TEETER.SWF", "TEETER.MP3");
    }

    /// <summary>Travel2 event 38: a 5,000..25,000-kubar computer automation gamble.</summary>
    public static TravelEventResult MeegOffer(CompanyState company, decimal cost, bool succeeds)
    {
        cost = Math.Clamp(GameMath.WholeKubars(cost / 1_000m) * 1_000m, 5_000m, 25_000m);
        return Choice("Automating Computer System",
            $"You happen across Meeg, a true adolescent technoid who claims to know just about everything.\n\n" +
            $"Meeg offers to entirely re-program your ship's computers, allowing you to replace one crew member " +
            $"with a fully automated system. The job will cost {cost:N0} kubars and may upset the Labor Union, " +
            "but it could save money in the long run.\n\nAre you interested?",
            "Yes", "No", company.CrewCount > 2 && company.Cash + company.Bank >= cost,
            accepted =>
            {
                if (!accepted) return Declined("You decline Meeg's offer.", "MEEG.SWF", "GOOD.MP3");
                if (succeeds)
                {
                    var transaction = company.AutomateCrewPosition(cost);
                    return new TravelEventResult("Success!",
                        $"Meeg re-programs the system successfully. One crew position is eliminated from the " +
                        $"payroll for {cost:N0} kubars. The Labor Union is not happy with this development.",
                        transaction.IsSuccessful, "MEEG.SWF", "GOOD5.MP3");
                }

                var repairCost = cost * 2m;
                company.PayMandatoryExpense(repairCost);
                company.TravelDelay++;
                return new TravelEventResult("System Screwed",
                    $"Meeg crashes the entire computer system, delaying the trip. A replacement motherboard costs " +
                    $"{repairCost:N0} kubars. Voyager's Insurance does not cover this self-inflicted crash.",
                    false, "MEEG.SWF", "BAD2.MP3");
            }, "MEEG.SWF", "GOOD.MP3");
    }

    /// <summary>Travel2 event 39. Spike's damage is a 15..50 roll multiplied by ship mass.</summary>
    public static TravelEventResult SpikeAdoption(CompanyState company, int damageRoll)
    {
        damageRoll = Math.Clamp(damageRoll, 15, 50);
        var damage = damageRoll * company.ShipTons;
        return Choice("Home For Tramp?",
            "You happen across Spike the Space Mutt. Spike hops aboard your ship and makes himself at home.\n\n" +
            "You can either adopt this stray dog or leave him on his asteroid.\n\nDo you adopt Spike?",
            "Adopt Spike", "Leave Spike", false,
            accepted =>
            {
                if (!accepted) return new TravelEventResult("Spike Left Behind",
                    "You leave Spike safely on his asteroid and continue your journey.", true,
                    "SPIKE.SWF", "SPIKE.MP3");
                company.PayMandatoryExpense(damage);
                return new TravelEventResult("Nightmare",
                    $"Spike runs wild through the ship, harassing passengers and tearing apart wiring and seats. " +
                    $"You finally leave him on a passing asteroid, but not before he causes {damage:N0} kubars " +
                    "of damage. Voyager's Insurance does not cover household pets.",
                    false, "SPIKE.SWF", "SPIKE.MP3");
            }, "SPIKE.SWF", "SPIKE.MP3");
    }

    /// <summary>Travel2 event 40. The fee uses the same 15..50 times ship-mass scale.</summary>
    public static TravelEventResult NibbleOffer(CompanyState company, int feeRoll, bool succeeds)
    {
        feeRoll = Math.Clamp(feeRoll, 15, 50);
        var fee = feeRoll * company.ShipTons;
        var weeklySaving = company.CrewCount * 100m;
        var aiAccepts = company.Cash + company.Bank >= fee && company.CrewSalary >= 1_500m &&
                        fee <= weeklySaving * 30m;
        return Choice("Aggressive Proposition",
            $"Nibble, a professional bully, offers to intimidate your crew into lowering their salary. He assures " +
            $"you this union-busting tactic works every time and promises to do the job for {fee:N0} kubars.\n\n" +
            "How about it?",
            "Hire Nibble", "Decline", aiAccepts,
            accepted =>
            {
                if (!accepted) return Declined("You decline Nibble's proposition.", "NIBBLE.SWF", "NIBBLE.MP3");
                company.PayMandatoryExpense(fee);
                if (succeeds)
                {
                    company.CrewSalary = Math.Max(0m, company.CrewSalary - 100m);
                    return new TravelEventResult("It Worked",
                        $"Nibble breaks up the union and scares the crew into lowering their weekly salary by " +
                        $"100 kubars per person. The new salary is {company.CrewSalary:N0} kubars.",
                        true, "NIBBLE.SWF", "NIBBLE2.MP3");
                }

                var backWages = company.CrewWagesOwed;
                company.PayMandatoryExpense(backWages);
                company.CrewWagesOwed = 0m;
                company.CrewSalary += 100m;
                return new TravelEventResult("Disastrous Result",
                    $"Nibble's bullying backfires and the crew goes on strike. You pay {backWages:N0} kubars in " +
                    $"back wages and must raise weekly salary by 100 kubars per person to {company.CrewSalary:N0}.",
                    false, "NIBBLE.SWF", "BAD.MP3");
            }, "NIBBLE.SWF", "NIBBLE.MP3");
    }

    /// <summary>Travel2 event 41. The offer is 25..125 times ship mass; a police fine is twice the offer.</summary>
    public static TravelEventResult SpeevakOffer(CompanyState company, int offerRoll, bool policeCatch)
    {
        offerRoll = Math.Clamp(offerRoll, 25, 125);
        var offer = offerRoll * company.ShipTons;
        return Choice("Waste Wanted",
            $"A distraught pregnant space fly, commonly known as a Speevak, offers to pay you {offer:N0} kubars " +
            "to empty your ship's septic tanks onto her asteroid. The waste is perfect for breeding baby flies, " +
            "and she promises to clean up afterwards.\n\nDo you agree?",
            "Agree", "Decline", true,
            accepted =>
            {
                if (!accepted) return Declined("You refuse the Speevak's illegal waste-disposal offer.",
                    "SPEEVAK.SWF", "SPEEVAK.MP3");
                company.Cash += offer;
                if (!policeCatch)
                    return new TravelEventResult("Waste Delivered",
                        $"The Speevak pays {offer:N0} kubars and cleans up the asteroid as promised.",
                        true, "SPEEVAK.SWF", "SPEEVAK.MP3");

                var fine = offer * 2m;
                company.PayMandatoryExpense(fine);
                company.TravelDelay++;
                return new TravelEventResult("Sanitation Hazard",
                    $"The sanitation police catch the illegal dumping and fine you {fine:N0} kubars. Voyager's " +
                    "Insurance does not cover illegal waste disposal.",
                    false, "POLICE.SWF", "POLICE.MP3");
            }, "SPEEVAK.SWF", "SPEEVAK.MP3");
    }

    /// <summary>Travel2 good event 43. The pilot's maneuver cuts this journey's duration in half.</summary>
    public static TravelEventResult PilotShortcut(CompanyState company)
    {
        company.TravelTimeMultiplier *= 0.5d;
        return new TravelEventResult("Travel Time Shortened",
            "Because of some skillful maneuvering on the part of your pilot, it will take you considerably less " +
            "time than expected to reach your destination planet.", true, "PILOT.SWF", "PILOT.MP3");
    }

    /// <summary>Travel2 good event 44. Snoz pays for an immediate diversion.</summary>
    public static TravelEventResult SnozTransport(CompanyState company, string destination, int paymentRoll)
    {
        paymentRoll = Math.Clamp(paymentRoll, 25, 125);
        var payment = paymentRoll * company.ShipTons;
        return Choice("Transportation Needed",
            $"Snoz Lombardo, a less than acclaimed lounge singer, has landed a contract at a hot lounge on " +
            $"{destination}. He will pay you {payment:N0} kubars if you take him there right away.\n\nHow about it?",
            $"Take Snoz to {destination}", "Decline", true,
            accepted =>
            {
                if (!accepted) return Declined("You leave Snoz to find another ship.", "SNOZ.SWF", "SNOZ.MP3");
                company.Cash += payment;
                company.Planet = destination;
                company.PassengerAdvertising = 0;
                return new TravelEventResult("Contract Accepted",
                    $"Snoz pays {payment:N0} kubars and your ship changes course for {destination}.", true,
                    "SNOZ.SWF", "SNOZ.MP3");
            }, "SNOZ.SWF", "SNOZ.MP3");
    }

    /// <summary>Travel2 good event 45. One-quarter of accepted lifts ends with free show passes.</summary>
    public static TravelEventResult ShimmerTransport(CompanyState company, string destination, int rewardRoll,
        bool greetedByAnotherShip)
    {
        rewardRoll = Math.Clamp(rewardRoll, 100, 150);
        var reward = rewardRoll * company.ShipTons;
        return Choice("Desperate Dancer",
            $"Lady Shimmer, a former polka dancer, waves down your ship. Her vessel ran out of gas and she " +
            $"desperately needs a lift to {destination}, where she is to perform for the Emperor.\n\n" +
            $"Do you give Shimmer a lift to {destination}?",
            $"Take Shimmer to {destination}", "Decline", true,
            accepted =>
            {
                if (!accepted) return Declined("You leave Lady Shimmer to await another vessel.",
                    "SHIMMER.SWF", "SHIMMER.MP3");
                company.Planet = destination;
                company.PassengerAdvertising = 0;
                if (greetedByAnotherShip)
                    return new TravelEventResult("Happy Dancer",
                        $"While travelling to {destination}, another ship arrives for Lady Shimmer. She gives " +
                        "you two free passes to her show, hops aboard the other ship, and waves goodbye.", true,
                        "SHIMMER.SWF", "SHIMMER2.MP3");
                company.Cash += reward;
                return new TravelEventResult("Kindness Rewarded",
                    $"Emperor Dred greets you on the way to {destination} and gives you a gold medallion worth " +
                    $"{reward:N0} kubars for helping Lady Shimmer.", true, "DRED.SWF", "DRED.MP3");
            }, "SHIMMER.SWF", "SHIMMER.MP3");
    }

    /// <summary>Travel2 good event 46. The Teal Tree pays for a diversion to its home grove.</summary>
    public static TravelEventResult TealTransport(CompanyState company, string destination, int paymentRoll)
    {
        paymentRoll = Math.Clamp(paymentRoll, 25, 125);
        var payment = paymentRoll * company.ShipTons;
        return Choice("So Far Away",
            $"You discover a lonely Teal Tree growing on an asteroid. She begs you to take her to {destination} " +
            "so she can be reunited with her grove.\n\nDo you take the tree there?",
            $"Take Tree to {destination}", "Decline", true,
            accepted =>
            {
                if (!accepted) return Declined("You leave the Teal Tree on her asteroid.", "TEAL.SWF", "TEAL.MP3");
                company.Planet = destination;
                company.PassengerAdvertising = 0;
                company.Cash += payment;
                return new TravelEventResult("The Teal's Gift",
                    $"The Teal Tree is so thankful to be returning to {destination} that she gives you " +
                    $"{payment:N0} kubars for your trouble.", true, "MONEY_N.SWF", "GOOD4.MP3");
            }, "TEAL.SWF", "TEAL.MP3");
    }

    /// <summary>Travel2 good event 47. Selling the ship's water has a one-in-four navigation risk.</summary>
    public static TravelEventResult StubbsWaterOffer(CompanyState company, string wrongDestination, int paymentRoll,
        bool navigationError)
    {
        paymentRoll = Math.Clamp(paymentRoll, 25, 75);
        var payment = paymentRoll * company.ShipTons;
        var intendedDestination = company.Planet;
        return Choice("Water Guzzler",
            $"Stubbs, a crazed water junkie, offers {payment:N0} kubars for all the water on your ship. Your " +
            "passengers and crew will have nothing to drink or bathe in before arrival.\n\nDo you take this chance?",
            "Sell All Water", "Decline", true,
            accepted =>
            {
                if (!accepted) return Declined("You refuse to sell the ship's water.", "STUBBS.SWF", "STUBBS.MP3");
                company.Cash += payment;
                if (!navigationError)
                    return new TravelEventResult("Water Sold",
                        $"Stubbs pays {payment:N0} kubars for the ship's water.", true,
                        "STUBBS.SWF", "STUBBS.MP3");
                company.Planet = wrongDestination;
                company.TravelDelay++;
                return new TravelEventResult("Wrong Planet",
                    "With no water, the passengers complain, the crew nearly mutinies, and the dehydrated pilot " +
                    $"makes a serious navigation error. Your ship travels to {wrongDestination} instead of " +
                    $"{intendedDestination}.", false, "PILOT.SWF", "PILOT.MP3");
            }, "STUBBS.SWF", "STUBBS.MP3");
    }

    private static TravelEventResult Choice(string heading, string message, string accept, string decline,
        bool aiAccepts, Func<bool, TravelEventResult> resolve, string image = "", string audio = "GOOD.MP3") =>
        new(heading, message, true, image, audio, new TravelEventChoice(accept, decline, aiAccepts, resolve));

    private static TravelEventResult Declined(string message, string image = "", string audio = "GOOD.MP3") =>
        new("Offer Declined", message, true, image, audio);
}
