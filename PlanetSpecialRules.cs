using System;
using System.Linq;

namespace OpenTradeEngine;

/// <summary>
/// Planet-special result tables translated from frm_Special_action in the
/// original SWF.  Keeping these outside the view makes every original branch
/// directly testable and prevents presentation changes from changing rules.
/// </summary>
public sealed partial class GameSession
{
    /// <summary>
    /// Gazillionaire stores one 1..100 news roll and reuses it throughout a
    /// week. Several planet specials deliberately key their availability and
    /// prices from that same value.
    /// </summary>
    public int OriginalPlanetSpecialNewsData => 1 + (int)(Math.Abs((long)GameMath.StableHash(
        Seed, Week.ToString(), "original newsData")) % 100L);

    public int PlanetSpecialRoll(CompanyState company, string table, int minimum, int maximum)
    {
        var span = maximum - minimum + 1;
        return minimum + (int)(Math.Abs((long)GameMath.StableHash(
            Seed, Week.ToString(), company.Name, "planet special", table)) % span);
    }

    public decimal OriginalSpecialAmount(CompanyState company, string table, int minimum, int maximum)
    {
        var roll = PlanetSpecialRoll(company, table, minimum, maximum);
        return GameMath.WholeKubars(roll * company.ShipTons);
    }

    public TradeResult ResolveZileFavor(CompanyState company, int roll)
    {
        using var action = GameplayLogger.BeginCompanyAction(company, "ZILE FAVOR", $"roll={roll}");
        roll = Math.Clamp(roll, 1, 31);
        switch (roll)
        {
            case 7:
            case 18:
            {
                var old = company.ZinnRate;
                company.ZinnRate = old <= 1m ? 2m : old - 1m;
                var improved = company.ZinnRate < old;
                var phrase = improved
                    ? $"lowers your interest rate from {old:0.#}% to {company.ZinnRate:0.#}%"
                    : "raises your rate to 2%";
                return TradeResult.Success($"Mr. Zinn {phrase}.", improved
                    ? OutcomeHighlight.Positive(phrase)
                    : OutcomeHighlight.Negative(phrase));
            }
            case 8:
                company.ZinnCreditLimit += 50_000m;
                return TradeResult.Success("Mr. Zinn extends your credit limit by 50,000 kubars.",
                    OutcomeHighlight.Positive("extends your credit limit by 50,000 kubars"));
            case 19:
                company.ZinnCreditLimit += 75_000m;
                return TradeResult.Success("Mr. Zinn raises your credit limit by 75,000 kubars.",
                    OutcomeHighlight.Positive("raises your credit limit by 75,000 kubars"));
            case 9 when company.ZinnLoan > 0m:
                return ForgiveZinnDebt(company, 3m, "one third");
            case 20 when company.ZinnLoan > 0m:
                return ForgiveZinnDebt(company, 4m, "one quarter");
            case 10:
                company.Cash += 50_000m;
                company.ZinnLoan += 50_000m;
                company.ZinnCreditLimit += 75_000m;
                return TradeResult.Success("Mr. Zinn loans you 50,000 more kubars and extends your credit limit by 75,000 kubars.",
                    OutcomeHighlight.Positive("loans you 50,000 more kubars"),
                    OutcomeHighlight.Positive("extends your credit limit by 75,000 kubars"));
            case 21:
                company.Cash += 40_000m;
                company.ZinnLoan += 40_000m;
                company.ZinnCreditLimit += 60_000m;
                return TradeResult.Success("Mr. Zinn lends you 40,000 more kubars and extends your credit limit by 60,000 kubars.",
                    OutcomeHighlight.Positive("lends you 40,000 more kubars"),
                    OutcomeHighlight.Positive("extends your credit limit by 60,000 kubars"));
            default:
                return TradeResult.Success(ZileFlavour(roll));
        }
    }

    private static TradeResult ForgiveZinnDebt(CompanyState company, decimal divisor, string fraction)
    {
        var old = company.ZinnLoan;
        company.ZinnLoan = GameMath.WholeKubars(old - old / divisor);
        var phrase = $"forgives {fraction} of your debt";
        return TradeResult.Success($"Mr. Zinn {phrase}. Your debt shrinks from {old:N0} to {company.ZinnLoan:N0} kubars.",
            OutcomeHighlight.Positive(phrase),
            OutcomeHighlight.Positive($"{old:N0} to {company.ZinnLoan:N0} kubars"));
    }

    private static string ZileFlavour(int roll) => roll switch
    {
        1 => "Mr. Zinn apologizes for being unable to lower his rates or extend any more credit at this time.",
        2 => "Mr. Zinn says he is all out of money and cannot lower his rates.",
        3 => "Mr. Zinn says times are tough for everyone and you must do your best with the current rates.",
        4 => "Mr. Zinn says his own company may go bankrupt, so please do not ask him for favors.",
        5 => "Mr. Zinn cannot lower his rates now, but asks you to come back in a few weeks.",
        6 => "Mr. Zinn says his shareholders set the rate and the situation is out of his control.",
        9 => "Mr. Zinn invites you to share dinner with him.",
        11 => "Mr. Zinn's butler says he is away on business.",
        12 => "Mr. Zinn has Nefratic Fever and cancels the meeting.",
        13 => "Mr. Zinn asks what you think about him getting a nose transplant.",
        14 => "Mr. Zinn says he must maintain his luxurious lifestyle and cannot help.",
        15 => "Mr. Zinn encourages you to borrow from the Traders' Union instead.",
        16 => "Mr. Zinn is involved in several large deals and cannot loan you cash.",
        17 => "Mr. Zinn jokes about asking you for half a million kubars, but cannot help today.",
        20 => "Mr. Zinn introduces you to Mrs. Zinn and his twelve daughters.",
        _ => "Mrs. Zinn says her husband is tied up in meetings and asks you to return next time."
    };

    public TradeResult ResolveFracInsuranceReview(CompanyState company, int roll, decimal accountingAmount)
    {
        using var action = GameplayLogger.BeginCompanyAction(company, "FRAC INSURANCE REVIEW",
            $"roll={roll}; amount={accountingAmount:0}");
        roll = Math.Clamp(roll, 1, 30);
        if (roll is >= 8 and <= 11 or >= 23 and <= 26)
            return AdjustOriginalInsuranceRange(company, company.InsurancePriceRange > 6 ? -5 : 5);
        if (roll is 12 or 27)
            return AdjustOriginalInsuranceRange(company, 5);
        if (roll == 20)
        {
            accountingAmount = GameMath.WholeKubars(accountingAmount);
            company.PayMandatoryExpense(accountingAmount);
            return TradeResult.Success(
                $"Your agent finds that Voyager's Insurance has been under-charging you. You owe {accountingAmount:N0} kubars.",
                OutcomeHighlight.Negative($"{accountingAmount:N0} kubars"));
        }
        if (roll >= 28)
        {
            accountingAmount = GameMath.WholeKubars(accountingAmount);
            company.Cash += accountingAmount;
            return TradeResult.Success(
                $"Your agent finds that Voyager's Insurance has been overcharging you. You receive a refund of {accountingAmount:N0} kubars.",
                OutcomeHighlight.Positive("refund"), OutcomeHighlight.Positive($"{accountingAmount:N0} kubars"));
        }

        var message = roll switch
        {
            1 => "The insurance company says it is working on lower rates for everyone.",
            2 => "The insurance company insists its rates are the lowest in the galaxy.",
            3 => "Your request will be considered the next time rates are adjusted.",
            4 => "After a two-hour wait, you are told the person you need is not in this week.",
            5 => "The insurance company tells you to return when things are less busy.",
            6 => "The company shows you its statistics and says your rates cannot be lowered further.",
            7 => "The company promises to do its best to keep you satisfied.",
            >= 13 and <= 15 => "The company says premiums are statistical and cannot be altered without a change in your accident record.",
            16 => "New government regulations prevent the company from lowering existing premiums.",
            17 => "Your agent says Voyager's Insurance is in the red and cannot lower rates.",
            18 => "The company says your poor accident record makes further reductions impossible.",
            19 => "Your agent reminds you that Voyager has a monopoly, so complaints will get you nowhere.",
            21 => "Your representative spends the meeting ranting about government interference.",
            _ => "Your agent praises Voyager's quality service and says you should be thankful to be a customer."
        };
        return TradeResult.Success(message);
    }

    private TradeResult AdjustOriginalInsuranceRange(CompanyState company, int change)
    {
        var old = company.InsurancePriceRange;
        company.InsurancePriceRange = Math.Max(1, old + change);
        company.InsuranceCost = GenerateInsuranceQuote(company);
        var difference = Math.Abs(company.InsurancePriceRange - old);
        var lower = company.InsurancePriceRange < old;
        var phrase = lower ? $"lower your rates an average of {difference}%" : $"raise your rates an average of {difference}%";
        return TradeResult.Success($"Voyager's Insurance will {phrase}.", lower
            ? OutcomeHighlight.Positive(phrase)
            : OutcomeHighlight.Negative(phrase));
    }

    public TradeResult ResolveXeenUpgrade(CompanyState company, int upgrade, decimal cost, int attemptRoll, int successTextRoll)
    {
        using var action = GameplayLogger.BeginCompanyAction(company, "XEEN UPGRADE",
            $"upgrade={upgrade}; cost={cost:0}; attempt={attemptRoll}; result={successTextRoll}");
        upgrade = Math.Clamp(upgrade, 0, 3);
        if (attemptRoll == 1)
        {
            var work = upgrade switch
            {
                0 => "turbocharge your ship's engine",
                1 => "expand your ship's cargo bay by 10 tons",
                2 => "expand your ship's passenger capacity by 1 seat",
                _ => "expand your ship's fuel tank capacity by 5 tons"
            };
            return TradeResult.Success(XeenFailureText(Math.Clamp(successTextRoll, 1, 15), work));
        }

        company.PayMandatoryExpense(cost);
        var improvement = upgrade switch
        {
            0 => ApplyXeenTurbo(company),
            1 => ApplyXeenCargo(company),
            2 => ApplyXeenPassenger(company),
            _ => ApplyXeenFuel(company)
        };
        var lead = successTextRoll switch
        {
            1 => "With a few spare parts and some hard work, your mechanic manages to",
            2 => "Your mechanic is successful! In no time, he manages to",
            _ => "Your mechanic labors day and night and does a fantastic job to"
        };
        var message = $"{lead} {improvement} for {cost:N0} kubars.";
        return TradeResult.Success(message,
            OutcomeHighlight.Positive(improvement), OutcomeHighlight.Negative($"{cost:N0} kubars"));
    }

    private static string ApplyXeenTurbo(CompanyState company)
    {
        company.Turbocharges++;
        return "turbocharge your ship's engine";
    }

    private static string ApplyXeenCargo(CompanyState company)
    {
        company.CargoCapacityBonus += 10;
        return "expand your ship's cargo bay by 10 tons";
    }

    private static string ApplyXeenPassenger(CompanyState company)
    {
        company.PassengerCapacityBonus++;
        return "increase passenger capacity by 1 seat";
    }

    private static string ApplyXeenFuel(CompanyState company)
    {
        company.FuelCapacityBonus += 5;
        return "expand your ship's fuel tank capacity by 5 tons";
    }

    private static string XeenFailureText(int roll, string work) => roll switch
    {
        1 => $"Your mechanic takes the ship apart, then discovers it is impossible to {work}.",
        2 => "Your mechanic's garage is a disaster, and the mechanic is passed out on the floor.",
        3 => "Your mechanic is not at the workshop. After waiting three hours, you leave.",
        4 => "The Mechanics' Union Dance is being held in the garage, so no work gets done.",
        5 => "Your mechanic cannot obtain the proper parts for the upgrade.",
        6 => "Your mechanic works for 48 hours, cannot get the upgrade right, and gives up.",
        7 => "Your mechanic spends the afternoon telling jokes and never starts the work.",
        8 => "A critical problem with the ship's power system forces the mechanic to scrap the upgrade.",
        9 => "Your mechanic struggles with the changes, but the job proves too difficult.",
        10 => "The upgrade leaves no room for the crew, so you force the mechanic to undo it.",
        11 => "The mechanic warns the change may permanently damage the drive system, so you cancel it.",
        12 => "Your mechanic is too busy to upgrade the ship this week.",
        13 => "After examining the ship, your cautious mechanic decides the upgrade is a bad idea.",
        14 => "Your mechanic declares the ship a piece of junk and refuses to work on it.",
        _ => "Your mechanic cannot find the right tools and asks you to come back another week."
    };

    public TradeResult ResolveQuegOffer(
        CompanyState company, int commodity, int quantity, decimal pricePerTon, int newsData)
    {
        using var action = GameplayLogger.BeginCompanyAction(company, "QUEG OFFER",
            $"commodity={commodity}; quantity={quantity}; price={pricePerTon:0}; newsData={newsData}");
        commodity = Math.Clamp(commodity, 0, CommodityCatalog.All.Length - 1);
        quantity = Math.Max(1, quantity);
        if (company.CargoFree < quantity)
            return TradeResult.Success(
                $"Lady Cornucopia offers {quantity} tons of {CommodityCatalog.All[commodity].Name}, but your ship has room for only {company.CargoFree} tons.");

        // Decompiled original: newsData % 5 == 0 cancels the transaction and
        // turn % 10 selects one of ten explanations.
        if (Math.Abs(newsData) % 5 == 0)
        {
            var excuse = (Week % 10) switch
            {
                0 => "Lady Cornucopia refuses to sell you anything and has two armed guards escort you from the palace.",
                1 => "The warehouse guard has no authorization to sell you anything, and the administrator refuses the deal.",
                2 => "Lady Cornucopia orders you to leave or have your ship blown up.",
                3 => "Lady Cornucopia has a severe headache and cancels all business transactions this week.",
                4 => "Lady Cornucopia takes one look at you, feels sick, and orders you to leave.",
                5 => "Lady Cornucopia is meeting Emperor Dred Nicolson and refuses to sell you anything.",
                6 => "A skirmish with Hapa Jillos leaves the palace grounds in chaos, and guards order you away.",
                7 => "Lady Cornucopia has run out of goods and asks you to return next week.",
                8 => "The warehouse's goods are defective and must be destroyed, so there will be no sales this week.",
                _ => "Lady Cornucopia must reserve her goods for larger clients and cannot sell to you this week."
            };
            return TradeResult.Success(excuse);
        }

        var total = GameMath.WholeKubars(quantity * pricePerTon);
        company.PayMandatoryExpense(total);
        if (!company.Cargo.TryGetValue(commodity, out var lot)) lot = new CargoLot();
        var oldValue = lot.Quantity * lot.AverageCost;
        lot.Quantity += quantity;
        lot.AverageCost = (oldValue + total) / lot.Quantity;
        company.Cargo[commodity] = lot;
        var message = $"Lady Cornucopia sells you {quantity} tons of {CommodityCatalog.All[commodity].Name} at {pricePerTon:N0} kubars per ton, for {total:N0} kubars.";
        return TradeResult.Success(message,
            OutcomeHighlight.Positive($"{quantity} tons of {CommodityCatalog.All[commodity].Name}"),
            OutcomeHighlight.Negative($"{total:N0} kubars"));
    }

    public TradeResult ResolveHorkPublicity(CompanyState company, int roll, decimal amount)
    {
        using var action = GameplayLogger.BeginCompanyAction(company, "HORK PUBLICITY", $"roll={roll}; amount={amount:0}");
        roll = Math.Clamp(roll, 1, 30);
        amount = GameMath.WholeKubars(Math.Max(0m, amount));
        if (roll is 1 or 2 or 6 or 8 or 12 or 22)
        {
            company.Cash += amount;
            var message = HorkFlavour(roll) + $" You are paid {amount:N0} kubars.";
            return TradeResult.Success(message, OutcomeHighlight.Positive($"{amount:N0} kubars"));
        }
        if (roll == 13)
        {
            company.PayMandatoryExpense(amount);
            var message = HorkFlavour(roll) + $" The ambulance bill is {amount:N0} kubars.";
            return TradeResult.Success(message, OutcomeHighlight.Negative($"{amount:N0} kubars"));
        }
        return TradeResult.Success(HorkFlavour(roll));
    }

    private static string HorkFlavour(int roll) => roll switch
    {
        1 => "You star on Evy Ev's Planet Chat as a self-made entrepreneur.",
        2 => "You appear on prime-time television selling jelly beans.",
        3 => "You spend an afternoon bouncing Borly Balls with children at an orphanage.",
        4 => "The Ministry of Travel uses your company as an example in a new promotion.",
        5 => "The Astral Reporter publishes a glowing article portraying you as a financial genius.",
        6 => "You discuss your imports and exports on the hit Geeg Matiss Extravaganza.",
        7 => "The Cosmic Consumer Caucus unexpectedly praises your efforts to keep fares down.",
        8 => "The Interplanetary Inquirer prints a ridiculous story claiming you are a robot.",
        9 => "You discuss trade regulations on Planet Line News and gain good publicity.",
        10 => "You mingle with Kukubia's rich and famous at the Ceremony of the Stars.",
        11 => "A daytime talk show casts you as a ruthless merchant and the villain of the episode.",
        12 => "Your agent lands you an embarrassing prime-time job promoting underwear.",
        13 => "A Borly Ball hits you during a charity benefit and you are taken to hospital.",
        14 => "The Ministry of Travel accuses your company of smuggling, harming your public image.",
        15 => "The Astral Reporter portrays you as a robber baron who inflates passenger fares.",
        16 => "You make a complete fool of yourself on the Geeg Matiss Extravaganza.",
        17 => "The Cosmic Consumer Caucus makes you defend your passenger prices.",
        18 => "The Interplanetary Inquirer portrays you as an obsessive money miser.",
        19 => "Planet Line News interviews your employees about how little they are paid.",
        20 => "At the Ceremony of the Stars, nobody wants to talk to you.",
        21 => "On daytime television, you explain that your company provides the best service at the lowest cost.",
        22 => "You make a prime-time commercial for Lava Lamps.",
        23 => "A famous sports star volunteers to promote your company at the next big game.",
        24 => "The Ministry of Travel ties your company to a new government travel program.",
        25 => "An Astral Reporter journalist asks only about ship size and cargo capacity, then leaves.",
        26 => "You are replaced by Peelia Veelia before your late-night television appearance.",
        27 => "The Cosmic Consumer Caucus talks only about ways to improve service.",
        28 => "The Interplanetary Inquirer loses interest when you arrive for your interview.",
        29 => "Your Planet Line News interview turns out to be only a sound bite.",
        _ => "You spend an uneventful evening in the crowd at the Ceremony of the Stars."
    };

    public TradeResult ResolveBassBroker(CompanyState company, int unavailableResult)
    {
        using var action = GameplayLogger.BeginCompanyAction(company, "BASS BROKER",
            $"unavailableResult={unavailableResult}");
        if (Week % 6 != 0)
        {
            return TradeResult.Success("Your stock broker gives you the following recommendations:\n\n" +
                string.Join("\n", Planets.Select(planet => $"{planet} Exchange = {StockRecommendation(planet)}")));
        }

        var message = Math.Clamp(unavailableResult, 0, 9) switch
        {
            0 => "Your broker is away at a bond convention on Loro.",
            1 => "Your broker is in a rush and only advises you to invest for the long term.",
            2 => "Your broker is distraught over her husband leaving again, and you spend the meeting consoling her.",
            3 => "Your broker spends the whole meeting complaining about taxes.",
            4 => "Your broker is on the stock-exchange floor for the rest of the week.",
            5 => "Your broker shows you her pet Cameo and discusses Cameo fur all afternoon.",
            6 => "Your broker gives you a long lecture about avoiding risky investments.",
            7 => "Your broker is advising Emperor Dred Nicolson and cannot see you.",
            8 => "Your broker refuses to predict the market in the current economy.",
            _ => "Your broker is meeting with the Imperial Magistrate and cannot see you this week."
        };
        return TradeResult.Success(message);
    }

    public TradeResult ResolveNoshWholesaler(CompanyState company, decimal pricePerTon, int newsData)
    {
        using var action = GameplayLogger.BeginCompanyAction(company, "NOSH WHOLESALER",
            $"price={pricePerTon:0}; newsData={newsData}");
        if (company.Fuel >= company.FuelCapacity)
            return TradeResult.Success("You go to fill up your tank but discover it is already full.");
        // Decompiled original: newsData % 5 + 1 == 1 makes Zobrok
        // unavailable, while turn % 7 selects the explanation.
        if (Math.Abs(newsData) % 5 == 0)
        {
            var message = (Week % 7) switch
            {
                0 => "The previous customer purchased the last of Zobrok's fuel.",
                1 => "Zobrok changes his mind and asks you to return another week.",
                2 => "Fuel prices changed, so Zobrok can no longer give you the discount.",
                3 => "Zobrok discovers a leak in his main tank and cannot sell fuel.",
                4 => "Zobrok changes his mind. There is no fuel for you this week.",
                5 => "Zobrok is nowhere to be found, so you eventually leave.",
                _ => "Zobrok has an emergency and cannot help you this week."
            };
            return TradeResult.Success(message);
        }

        var quantity = company.FuelCapacity - company.Fuel;
        var retail = Markets[company.Planet].FuelPrice;
        var cost = GameMath.WholeKubars(quantity * pricePerTon);
        company.PayMandatoryExpense(cost);
        company.Fuel = company.FuelCapacity;
        var saving = GameMath.WholeKubars(quantity * Math.Max(0m, retail - pricePerTon));
        return TradeResult.Success($"Zobrok fills your tank, saving you {saving:N0} kubars.",
            OutcomeHighlight.Positive($"saving you {saving:N0} kubars"));
    }
}
