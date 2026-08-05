using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace OpenTradeEngine.Screens;

public partial class PlanetServicesScreen : UserControl
{
    public string PlanetName { get; private set; } = string.Empty;
    private CompanyState? _company;
    public event EventHandler? ContinueRequested;
    public event EventHandler? MainMenuRequested;
    public event EventHandler<string>? SoundRequested;
    public PlanetServicesScreen() => InitializeComponent();

    public void Load(GameInstallation installation, GameSession session, CompanyState company)
    {
        PlanetName = company.Planet;
        _company = company;
        Heading.Text = SpecialHeading(company.Planet);
        var illustrationName = company.Planet.ToUpperInvariant() switch
        {
            "VEXX" => "ASIAN_N.SWF",
            "PYKE" => "MECH_N.SWF",
            "MIRA" => "MONK_N.SWF",
            // Stye's special is an audience with the Traders' Union, not the
            // separate banking service and its teller/manager artwork.
            "STYE" => "LOAN_N.SWF",
            "LORO" => "PEELIA_L.SWF",
            "ZILE" => "ZINN_N.SWF",
            "FRAC" => "INSURE_N.SWF",
            "TILO" => "DEALER_N.SWF",
            "QUEG" => "CORNU_N.SWF",
            "XEEN" => "MECHAN_L.SWF",
            "OOOM" => "SOOTH_N.SWF",
            "HORK" => "AGENT_L.SWF",
            "BASS" => "BROKER_N.SWF",
            "NOSH" => "ZOBROK_N.SWF",
            _ => "WHITE_V.SWF"
        };
        var illustration = SwfImageExtractor.TryExtractFirstEmbeddedImage(
            Path.Combine(installation.SwfDirectory, illustrationName),
            $"SPECIAL_STATIC_{company.Planet.ToUpperInvariant()}_{Path.GetFileNameWithoutExtension(illustrationName)}");
        if (illustration.IsSuccessful) Illustration.Source = new Bitmap(illustration.ImagePath!);

        if (company.Planet.Equals("Xeen", StringComparison.OrdinalIgnoreCase))
        {
            var companyRandom = session.PlanetSpecialRoll(company, "original company random", 1, 100);
            var offer = companyRandom % 4;
            var cost = (decimal)(companyRandom * session.OriginalPlanetSpecialNewsData * 6);
            Description.Text = offer switch
            {
                0 => $"Xeen is one giant junkyard filled with spare parts and brilliant mechanics.\n\nYour favorite mechanic claims he has found the necessary parts and can turbocharge your ship's engine, but it will cost you {cost:N0} kubars.\n\nDo you want him to work on it?",
                1 => $"Xeen is one giant junkyard filled with spare parts and brilliant mechanics.\n\nYour favorite mechanic claims he has found the necessary parts and can expand your ship's cargo bay by 10 tons, but it will cost you {cost:N0} kubars.\n\nDo you want him to work on it?",
                2 => $"Xeen is one giant junkyard filled with spare parts and brilliant mechanics.\n\nYour favorite mechanic claims he has found the necessary parts and can expand your ship's passenger capacity by 1 seat, but it will cost you {cost:N0} kubars.\n\nDo you want him to work on it?",
                _ => $"Xeen is one giant junkyard filled with spare parts and brilliant mechanics.\n\nYour favorite mechanic claims he has found the necessary parts and can expand your ship's fuel tank capacity by 5 tons, but it will cost you {cost:N0} kubars.\n\nDo you want him to work on it?"
            };
            var attemptRoll = session.PlanetSpecialRoll(company, "Xeen attempt", 1, 3);
            var resultRoll = attemptRoll == 1
                ? session.PlanetSpecialRoll(company, "Xeen failure", 1, 15)
                : session.PlanetSpecialRoll(company, "Xeen success wording", 1, 3);
            AddWeeklyAction(session, company, "Upgrade Your Ship",
                () => session.ResolveXeenUpgrade(company, offer, cost, attemptRoll, resultRoll));
        }
        else if (company.Planet.Equals("Pyke", StringComparison.OrdinalIgnoreCase))
        {
            var rating = Math.Min(10, company.BaseEngineSpeed + 1);
            var engineCost = session.PykeEngineCost(company);
            Description.Text = $"Pyke is home to L-Tech, the largest engine manufacturer in Kukubia.\n\nWhile on Pyke you can obtain the best deals on new faster {rating}-kuarp engines.\n\nThe cost varies depending on demand. This week you can purchase a {rating}-kuarp engine for {engineCost:N0} kubars. How about it?";
            AddWeeklyAction(session, company, "Buy Faster Engine", () => session.ResolvePykeEnginePurchase(company));
        }
        else if (company.Planet.Equals("Vexx", StringComparison.OrdinalIgnoreCase))
        {
            Description.Text = "Vexx is the seat of the Imperial Magistrate for the entire system of Kukubia.\n\nWhile on Vexx you can petition the government for lower import tariffs, export tariffs and passenger taxes.";
            var roll = Math.Abs(GameMath.StableHash(session.Seed, session.Week.ToString(), company.Name,
                "Vexx audience")) % 30 + 1;
            AddWeeklyAction(session, company, "Petition for Lower Taxes",
                () => session.ResolveVexxPetition(company, roll));
        }
        else if (company.Planet.Equals("Mira", StringComparison.OrdinalIgnoreCase))
        {
            Description.Text = "Mira is the oldest and most mystical of all the planets in Kukubia. The majority of Kukubia's religions have their origin on Mira.\n\nWhile on Mira, you may pay a visit to the renowned Grand Sages and ask them for a blessing, which can help to turn a bad luck streak into good fortune.";
            var roll = Math.Abs((long)GameMath.StableHash(session.Seed, session.Week.ToString(), company.Name,
                "Mira blessing")) % 30L + 1L;
            AddWeeklyAction(session, company, "Visit Grand Sages",
                () => session.ResolveMiraBlessing(company, (int)roll));
        }
        else if (company.Planet.Equals("Stye", StringComparison.OrdinalIgnoreCase))
        {
            Description.Text = "Stye is the financial capital of Kukubia and where the Traders' Union Headquarters is located.\n\nWhile on Stye, you can ask the Traders' Union for financial assistance.\n\nIf you are lucky, they may lower the interest rate on your loan, increase your credit limit, or raise the interest rate on your savings account.";
            var roll = Math.Abs((long)GameMath.StableHash(session.Seed, session.Week.ToString(), company.Name,
                "Stye union")) % 31L + 1L;
            AddWeeklyAction(session, company, "Ask for Assistance",
                () => session.ResolveStyeAssistance(company, (int)roll));
        }
        else if (company.Planet.Equals("Loro", StringComparison.OrdinalIgnoreCase))
        {
            Description.Text = "Loro is known throughout Kukubia as the Pleasure Planet.\n\nIt is the most popular resort and vacation spot in the entire system.\n\nWhile on Loro, you can grant your crew ship leave. This will boost their morale and make for a better working relationship.";
            var roll = session.PlanetSpecialRoll(company, "Loro leave", 1, 30);
            var incidentCost = session.OriginalSpecialAmount(company, "Loro expense", 5, 20);
            AddWeeklyAction(session, company, "Grant Crew Ship Leave",
                () => session.ResolveLoroCrewLeave(company, roll, incidentCost));
        }
        else if (company.Planet.Equals("Zile", StringComparison.OrdinalIgnoreCase))
        {
            Description.Text = "Zile is Mr. Zinn's home planet.\n\nWhile on Zile, you may pay Mr. Zinn a visit and ask him for a favor.\n\nIf you are lucky, he will lower the interest rate on your loan or increase your credit limit.";
            var roll = session.PlanetSpecialRoll(company, "Zile favor", 1, 31);
            AddWeeklyAction(session, company, "Ask Zinn a Favor",
                () => session.ResolveZileFavor(company, roll));
        }
        else if (company.Planet.Equals("Frac", StringComparison.OrdinalIgnoreCase))
        {
            Description.Text = "Frac is a bustling business center.\n\nWhile on Frac you can pay a visit to the Voyager's Insurance Company headquarters and ask for a reduction in your weekly premiums.";
            var roll = session.PlanetSpecialRoll(company, "Frac review", 1, 30);
            var companyRandom = session.PlanetSpecialRoll(company, "original company random", 1, 100);
            var accountingAmount = companyRandom * company.CargoCapacity * 4m;
            AddWeeklyAction(session, company, "Ask for Lower Premiums",
                () => session.ResolveFracInsuranceReview(company, roll, accountingAmount));
        }
        else if (company.Planet.Equals("Tilo", StringComparison.OrdinalIgnoreCase))
        {
            Description.Text = "Tilo is the only planet in Kukubia with legalized gambling.\n\nIf you're feeling lucky, you can try your hand at Tilo's most popular game: All Or Nothing.\n\nIf you win, you'll double whatever you bet. If you lose, the fun is over. Under Imperial Law, no one may gamble more than 5% of their total cash in one week.";
            AddTiloGamblingAction(session, company);
        }
        else if (company.Planet.Equals("Queg", StringComparison.OrdinalIgnoreCase))
        {
            var market = session.Markets[company.Planet];
            // Original: the week selects one of three six-item commodity
            // classes and the company's 1..100 roll selects the item in it.
            var commodityClass = session.Week % 3;
            var companyRandom = session.PlanetSpecialRoll(company, "original company random", 1, 100);
            var commodity = commodityClass * 6 + companyRandom % 6;
            var available = Math.Max(company.CargoCapacity / 2,
                (int)decimal.Floor(company.CargoCapacity * companyRandom / 100m));
            var minimum = PlanetMarket.MinimumPrice(commodity, session.Difficulty);
            var maximum = CommodityCatalog.All[commodity].MaximumPrice;
            var price = minimum + decimal.Floor(
                (maximum - minimum) * session.OriginalPlanetSpecialNewsData / 100m);
            price = Math.Min(price, decimal.Floor(market.Listings[commodity].Price * 0.90m));
            price = Math.Max(1m, price);
            Description.Text = $"Queg is the infamous renegade planet. It is here that the galaxy's most notorious gangsters come to unload their wares.\n\nThis week Lady Cornucopia, the ruler of Queg, offers to sell you {available} tons of {CommodityCatalog.All[commodity].Name} for only {price:N0} kubars per ton.\n\nDo you want this deal?";
            AddWeeklyAction(session, company, "Buy from Cornucopia",
                () => session.ResolveQuegOffer(company, commodity, available, price,
                    session.OriginalPlanetSpecialNewsData));
        }
        else if (company.Planet.Equals("Ooom", StringComparison.OrdinalIgnoreCase))
        {
            var cost = session.OoomFortuneCost(company);
            Description.Text = $"Ooom is known throughout the galaxy as the Fortune Tellers' Planet. The Soothsayers of Ooom have the uncanny ability to predict the future by scanning one's aura for positive or negative energy.\n\nThis week the Soothsayers are charging {cost:N0} kubars for their service.";
            var awardWindfall = Math.Abs((long)GameMath.StableHash(
                session.Seed, session.Week.ToString(), company.Name, "Ooom windfall")) % 2L == 0L;
            AddWeeklyAction(session, company, "Have Your Fortune Read",
                () => session.ResolveOoomFortune(company, awardWindfall));
        }
        else if (company.Planet.Equals("Hork", StringComparison.OrdinalIgnoreCase))
        {
            Description.Text = "Hork is the media capital of Kukubia, affectionately known as Horkywood. You have an agent whose job is to get your company free publicity.\n\nThis week your agent claims to have a mega media event scheduled for you.\n\nDo you want to do it?";
            var roll = session.PlanetSpecialRoll(company, "Hork publicity", 1, 30);
            var amount = session.OriginalSpecialAmount(company, "Hork payment", 25, 75);
            AddWeeklyAction(session, company, "Do it!",
                () => session.ResolveHorkPublicity(company, roll, amount));
        }
        else if (company.Planet.Equals("Bass", StringComparison.OrdinalIgnoreCase))
        {
            Description.Text = "Bass is where all the stock brokers, market analysts and traders congregate to exchange information.\n\nIf your stock broker is not too busy, she can provide you with detailed information on market conditions across Kukubia.\n\nDo you want to pay her a visit?";
            var unavailable = session.OriginalPlanetSpecialNewsData % 10;
            AddWeeklyAction(session, company, "Visit Your Broker",
                () => session.ResolveBassBroker(company, unavailable));
        }
        else if (company.Planet.Equals("Nosh", StringComparison.OrdinalIgnoreCase))
        {
            var market = session.Markets[company.Planet];
            var companyRoll = session.PlanetSpecialRoll(company, "original company random", 1, 100);
            var discountPercent = 10m + companyRoll / 2m;
            var wholesalePrice = decimal.Floor(market.FuelPrice * (1m - discountPercent / 100m));
            Description.Text = $"Nosh is the fuel depot of Kukubia, and you hear that Zobrok, a big-time wholesaler, is selling fuel at {discountPercent:0.#}% off the retail price.\n\nIn other words, Zobrok is selling fuel at {wholesalePrice:N0} kubars per ton, while most other distributors are selling it at {market.FuelPrice:N0} kubars per ton.\n\nDo you want to fill up your tank?";
            AddWeeklyAction(session, company, "Visit Zobrok",
                () => session.ResolveNoshWholesaler(company, wholesalePrice,
                    session.OriginalPlanetSpecialNewsData));
        }
        else
        {
            Description.Text = "Planet-specific business services and encounters for this world are still being reconstructed from the original game.";
        }
        Refresh();
    }

    private void AddWeeklyAction(
        GameSession session,
        CompanyState company,
        string text,
        Func<TradeResult> action,
        bool consumeSuccessfulUse = true)
    {
        var alreadyCompleted = company.LastSpecialWeek == session.Week;
        var button = new Button
        {
            Content = alreadyCompleted ? "Return to Main Menu" : text,
            IsEnabled = true
        };
        button.Classes.Add("gameplay-footer");
        if (alreadyCompleted)
        {
            Description.Text = "You have already completed your Planet Special for this week.\n\nTry again next week.";
            StatusText.Text = string.Empty;
        }
        button.Click += (_, _) =>
        {
            if (button.Tag as string == "return" || company.LastSpecialWeek == session.Week)
            {
                MainMenuRequested?.Invoke(this, EventArgs.Empty);
                return;
            }
            var result = action();
            ShowOutcome(result);
            StatusText.Text = string.Empty;
            var sound = SpecialOutcomeAudio(session, company, result);
            if (!string.IsNullOrWhiteSpace(sound)) SoundRequested?.Invoke(this, sound);
            if (result.IsSuccessful && consumeSuccessfulUse)
            {
                company.LastSpecialWeek = session.Week;
                button.Content = "Return to Main Menu";
                button.Tag = "return";
            }
            else
            {
                button.Content = text;
                button.Tag = null;
            }
            Refresh();
        };
        ActionButtons.Children.Add(button);
    }

    private void AddTiloGamblingAction(GameSession session, CompanyState company)
    {
        if (company.LastSpecialWeek == session.Week)
        {
            AddWeeklyAction(session, company, "Try Gambling",
                () => TradeResult.Fail("You have already gambled this week."));
            return;
        }

        var button = new Button { Content = "Try Gambling", IsEnabled = true };
        button.Classes.Add("gameplay-footer");
        button.Click += (_, _) =>
        {
            if (button.Tag as string == "return")
            {
                MainMenuRequested?.Invoke(this, EventArgs.Empty);
                return;
            }

            // The original closes the casinos on one out of every six weekly
            // news states. A closure consumes the visit; an invalid wager does not.
            var closureRoll = session.OriginalPlanetSpecialNewsData;
            if (closureRoll % 6 == 0)
            {
                var reasons = new[]
                {
                    "The casinos are closed this week during Emperor Dred Nicolson's anti-gambling campaign.",
                    "The casinos are closed for a local religious holiday.",
                    "The Imperial Magistrate has ordered the casinos closed for a complete audit.",
                    "A major earthquake has closed the casinos for the remainder of the week.",
                    "Pirates sacked several casinos, forcing the Governor to shut down operations.",
                    "The casinos have closed for a week to pay their respects to a prominent owner.",
                    "The Governor has shut the casinos after a series of fights."
                };
                var reason = reasons[session.Week % reasons.Length];
                company.LastSpecialWeek = session.Week;
                ShowOutcome(TradeResult.Success(reason, OutcomeHighlight.Negative("closed")));
                SoundRequested?.Invoke(this, "NEUTRAL.MP3");
                MakeTiloReturnButton(button);
                return;
            }

            // showAmountNew receives one quarter of the legal maximum as Lower
            // and 5% of current cash as Upper.
            var maximum = company.MaximumTiloWager;
            AmountEntry.Show("All Or Nothing", $"Cash:  {company.Cash:N0}\nMaximum legal wager:  {maximum:N0}",
                "Enter your bet:", maximum, amount =>
                {
                    var wagerResult = company.PlaceTiloWager(amount);
                    if (!wagerResult.IsSuccessful)
                    {
                        ShowOutcome(TradeResult.Fail(
                            $"{wagerResult.Message}\n\nThe Casino Dealer asks you to return when you are ready to gamble."));
                        SoundRequested?.Invoke(this, "NEUTRAL2.MP3");
                        button.Content = "Try Gambling";
                        button.Tag = null;
                        Refresh();
                        return;
                    }

                    var stake = decimal.Floor(amount);
                    company.LastSpecialWeek = session.Week;
                    var initialRoll = Math.Abs((long)GameMath.StableHash(session.Seed,
                        session.Week.ToString(), company.Name, "Tilo initial wager")) % 20L + 1L;
                    if (initialRoll > 10L)
                    {
                        ShowOutcome(TradeResult.Success(
                            $"Sorry, but you lose. The Casino Dealer takes your {stake:N0} kubars and informs you that you will have to come back another time.",
                            OutcomeHighlight.Negative("lose"),
                            OutcomeHighlight.Negative($"{stake:N0} kubars")));
                        SoundRequested?.Invoke(this, "THANKYOU.MP3");
                        MakeTiloReturnButton(button);
                        Refresh();
                        return;
                    }

                    SoundRequested?.Invoke(this, "GAMBLE.MP3");
                    OfferTiloDoubleOrNothing(session, company, stake, 2m, 9, 1);
                    Refresh();
                }, lowerPreset: decimal.Floor(maximum * 0.25m),
                middlePreset: decimal.Floor(maximum * 0.50m), upperPreset: maximum);
        };
        ActionButtons.Children.Add(button);
    }

    private void OfferTiloDoubleOrNothing(
        GameSession session,
        CompanyState company,
        decimal stake,
        decimal payoutMultiplier,
        int winThreshold,
        int round)
    {
        var currentProfit = stake * (payoutMultiplier - 1m);
        var message = round == 1
            ? $"You win!!!\n\nThe Casino Dealer informs you that you have just won {currentProfit:N0} kubars. Under the house rules, you may now go for double or nothing.\n\nDo you want to continue gambling?"
            : $"You win again!!!\n\nYour current profit is {currentProfit:N0} kubars. You may collect it now or go for double or nothing again.";
        ShowOutcome(TradeResult.Success(message,
            OutcomeHighlight.Positive("win"),
            OutcomeHighlight.Positive($"{currentProfit:N0} kubars")));

        ActionButtons.Children.Clear();
        var continueButton = new Button { Content = "Double Or Nothing", IsEnabled = true };
        continueButton.Classes.Add("gameplay-footer");
        continueButton.Click += (_, _) =>
        {
            var roll = Math.Abs((long)GameMath.StableHash(session.Seed, session.Week.ToString(),
                company.Name, "Tilo double or nothing", round.ToString())) % 20L + 1L;
            if (roll > winThreshold)
            {
                ShowOutcome(TradeResult.Success(
                    "Sorry, but you lose it all. The Casino Dealer informs you that you will have to come back next week.",
                    OutcomeHighlight.Negative("lose it all")));
                SoundRequested?.Invoke(this, "THANKYOU.MP3");
                MakeTiloReturnButton();
                Refresh();
                return;
            }

            SoundRequested?.Invoke(this, "GAMBLE.MP3");
            OfferTiloDoubleOrNothing(session, company, stake, payoutMultiplier * 2m,
                Math.Max(0, winThreshold - 1), round + 1);
            Refresh();
        };

        var collectButton = new Button { Content = "Collect Winnings", IsEnabled = true };
        collectButton.Classes.Add("gameplay-footer");
        collectButton.Click += (_, _) =>
        {
            var payout = stake * payoutMultiplier;
            var profit = payout - stake;
            company.CollectTiloPayout(payout);
            ShowOutcome(TradeResult.Success(
                $"Congratulations, you just won {profit:N0} kubars! It is better to be lucky than smart.",
                OutcomeHighlight.Positive($"{profit:N0} kubars")));
            SoundRequested?.Invoke(this, "TILO.MP3");
            MakeTiloReturnButton();
            Refresh();
        };
        ActionButtons.Children.Add(continueButton);
        ActionButtons.Children.Add(collectButton);
    }

    private void MakeTiloReturnButton(Button? existingButton = null)
    {
        ActionButtons.Children.Clear();
        var button = existingButton ?? new Button();
        button.Content = "Return to Main Menu";
        button.IsEnabled = true;
        button.Tag = "return";
        if (!button.Classes.Contains("gameplay-footer")) button.Classes.Add("gameplay-footer");
        if (existingButton is null)
            button.Click += (_, _) => MainMenuRequested?.Invoke(this, EventArgs.Empty);
        ActionButtons.Children.Add(button);
    }

    private static string SpecialOutcomeAudio(GameSession session, CompanyState company, TradeResult result)
    {
        var positive = result.Highlights.Any(highlight => highlight.Tone == OutcomeTone.Positive);
        var negative = result.Highlights.Any(highlight => highlight.Tone == OutcomeTone.Negative);
        var message = result.Message;

        return company.Planet.ToUpperInvariant() switch
        {
            // Queg, Xeen and Nosh use the neutral cue when the offered deal
            // cannot be completed, then their character cue on completion.
            "XEEN" when !positive => "NEUTRAL.MP3",
            "XEEN" => "TEETER.MP3",
            "PYKE" when !result.IsSuccessful ||
                        message.Contains("cannot", StringComparison.OrdinalIgnoreCase) => "NEUTRAL.MP3",
            "PYKE" => Math.Abs((long)GameMath.StableHash(session.Seed, session.Week.ToString(),
                company.Name, "Pyke result audio")) % 2L == 0L ? "ENGINE.MP3" : "MECH.MP3",
            "MIRA" when negative && !positive => "BAD.MP3",
            "MIRA" when positive => "BLESSING.MP3",
            "MIRA" => "MONK.MP3",
            "STYE" when negative && !positive => "BAD.MP3",
            "STYE" when positive => "GOOD.MP3",
            "STYE" => "LOAN.MP3",
            "VEXX" when negative && !positive => "ASIAN.MP3",
            "VEXX" when positive => "IMPERIAL.MP3",
            "VEXX" => "ASIAN2.MP3",
            "LORO" when message.Contains("fuel tank", StringComparison.OrdinalIgnoreCase) => "FUEL.MP3",
            "LORO" when negative && !positive => "BAD.MP3",
            "LORO" when positive => "GOOD.MP3",
            "LORO" => "CREW.MP3",
            "ZILE" or "FRAC" when negative && !positive => "BAD.MP3",
            "ZILE" or "FRAC" when positive => "GOOD.MP3",
            "ZILE" => "ZINN.MP3",
            "FRAC" => "INSURE.MP3",
            "QUEG" when !positive => "NEUTRAL.MP3",
            "QUEG" => "CORNU.MP3",
            "OOOM" when !result.IsSuccessful ||
                        message.Contains("this week", StringComparison.OrdinalIgnoreCase) => "NEUTRAL3.MP3",
            "OOOM" => "SOOTH.MP3",
            "HORK" when negative && !positive ||
                        message.Contains("villain", StringComparison.OrdinalIgnoreCase) ||
                        message.Contains("smuggling", StringComparison.OrdinalIgnoreCase) ||
                        message.Contains("robber baron", StringComparison.OrdinalIgnoreCase) ||
                        message.Contains("complete fool", StringComparison.OrdinalIgnoreCase) ||
                        message.Contains("defend your passenger prices", StringComparison.OrdinalIgnoreCase) ||
                        message.Contains("money miser", StringComparison.OrdinalIgnoreCase) ||
                        message.Contains("how little they are paid", StringComparison.OrdinalIgnoreCase) => "BAD7.MP3",
            "HORK" when message.Contains("orphanage", StringComparison.OrdinalIgnoreCase) ||
                        message.Contains("nobody wants to talk", StringComparison.OrdinalIgnoreCase) ||
                        message.Contains("ship size and cargo capacity", StringComparison.OrdinalIgnoreCase) ||
                        message.Contains("replaced by Peelia", StringComparison.OrdinalIgnoreCase) ||
                        message.Contains("ways to improve service", StringComparison.OrdinalIgnoreCase) ||
                        message.Contains("loses interest", StringComparison.OrdinalIgnoreCase) ||
                        message.Contains("sound bite", StringComparison.OrdinalIgnoreCase) ||
                        message.Contains("uneventful evening", StringComparison.OrdinalIgnoreCase) => "AGENT2.MP3",
            "HORK" => "AGENT.MP3",
            "BASS" when !message.Contains("recommendations", StringComparison.OrdinalIgnoreCase) => "NEUTRAL.MP3",
            "BASS" => "STOCK2.MP3",
            "NOSH" when !message.Contains("saving you", StringComparison.OrdinalIgnoreCase) => "NEUTRAL.MP3",
            "NOSH" => "FUEL.MP3",
            _ => string.Empty
        };
    }

    private void Refresh()
    {
        if (_company is null) return;
        EngineText.Text = $"Installed engine: {_company.BaseEngineSpeed} kuarp   Turbocharges: {_company.Turbocharges}   " +
                          $"Effective speed: {_company.EngineSpeed}   Fuel penalty: None   Cash: {_company.Cash:N0}";
    }

    private void ShowOutcome(TradeResult result)
    {
        Description.Inlines?.Clear();
        Description.Text = string.Empty;
        Description.Foreground = Brushes.White;

        if (result.Highlights.Length == 0)
        {
            Description.Inlines?.Add(new Run(result.Message)
            {
                Foreground = result.IsSuccessful ? Brushes.White : NegativeBrush
            });
            return;
        }

        var remaining = result.Message;
        while (remaining.Length > 0)
        {
            var match = result.Highlights
                .Select(highlight => (Highlight: highlight,
                    Index: remaining.IndexOf(highlight.Text, StringComparison.OrdinalIgnoreCase)))
                .Where(candidate => candidate.Index >= 0)
                .OrderBy(candidate => candidate.Index)
                .FirstOrDefault();
            if (match.Highlight is null)
            {
                Description.Inlines?.Add(new Run(remaining) { Foreground = Brushes.White });
                break;
            }

            if (match.Index > 0)
                Description.Inlines?.Add(new Run(remaining[..match.Index]) { Foreground = Brushes.White });
            var matchedText = remaining.Substring(match.Index, match.Highlight.Text.Length);
            Description.Inlines?.Add(new Run(matchedText)
            {
                Foreground = match.Highlight.Tone == OutcomeTone.Positive ? PositiveBrush : NegativeBrush,
                FontWeight = FontWeight.Bold
            });
            remaining = remaining[(match.Index + match.Highlight.Text.Length)..];
        }
    }

    private static readonly IBrush PositiveBrush = new SolidColorBrush(Color.Parse("#77D7FF"));
    private static readonly IBrush NegativeBrush = new SolidColorBrush(Color.Parse("#F05555"));

    private static string SpecialHeading(string planet) => planet.ToUpperInvariant() switch
    {
        "VEXX" => "Imperial Magistrate",
        "PYKE" => "L-Tech Sales Rep.",
        "MIRA" => "Grand Sage",
        "STYE" => "Traders' Union Official",
        "LORO" => "Peelia Veelia, Queen of Loro",
        "ZILE" => "Mr. Zinn",
        "FRAC" => "Your Insurance Agent",
        "TILO" => "Casino Dealer",
        "QUEG" => "Lady Cornucopia",
        "XEEN" => "Your Mechanic",
        "OOOM" => "Soothsayer",
        "HORK" => "Your Agent",
        "BASS" => "Your Stock Broker",
        "NOSH" => "Zobrok the Fuel Wholesaler",
        _ => $"Explore {planet}"
    };

    private void HelpButton_Click(object? sender, RoutedEventArgs e) => HelpOverlay.IsVisible = true;
    private void CloseHelpButton_Click(object? sender, RoutedEventArgs e) => HelpOverlay.IsVisible = false;

    private void ContinueButton_Click(object? sender, RoutedEventArgs e) => ContinueRequested?.Invoke(this, EventArgs.Empty);
}
