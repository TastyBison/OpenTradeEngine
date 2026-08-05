using System;
using System.Collections.Generic;
using System.Globalization;

namespace OpenTradeEngine;

public static partial class ExploreContentCatalog
{
    private static readonly Dictionary<int, string> OriginalNewsTemplates = new()
    {
        [1] = "We have reports that the Chichi Bobo Rebels have moved into the area around {0}.\n\nOur sources indicate Supreme Commander Dred Nicolson is planning a counter attack this week and fighting will intensify.",
        [2] = "Our sources indicate that the Darleen Smugglers have increased their activity on and around {0}.\n\nThey appear to be attacking cargo ships importing goods to the planet.",
        [3] = "{0} has seen an upsurge in crime with the Baid-Rowel Bandits playing a major role.\n\nThey appear to be targeting merchant ships and passenger liners.",
        [4] = "Last night, {0} was plunged into chaos as the Mooglers, a rowdy group of anarchists, took to the streets to protest Supreme Commander Dred Nicolson's colonial rule.  We have reports of them attacking anything that gets in their way.",
        [5] = "Our sources inform us that a group of wild Fez Fa Fa broke away from their sanctuary yesterday and are headed towards {0}.\n\nThey are known to eat anything in their path, including Kukubian citizens.",
        [6] = "Sources indicate that the Hungo Warriors have taken to the war path near planet {0}.\n\nThese legendary fighters are pillaging local colonies, merchant ships and government outposts.",
        [7] = "The Cylet Mind Buggers are reported to be operating out of the {0} region.\n\nThey appear to be using their mental powers to attack vessels approaching or leaving the planet.",
        [8] = "Herds of mindless Lippo Jungies were spotted migrating towards {0}.\n\nThese enormous roaming space-whales seem to be crushing anything that gets in their path.",
        [9] = "A group of Wicky Wicks have been spotted moving towards {0}.\n\nThese parasitic creatures have been preying on any merchant ships they encounter.  If you are headed for the region, you are advised to change your travel plans.",
        [10] = "Space Pirates, descendants of the infamous Bro Nap Goodshark, have been spotted operating around {0}.\n\nWe have numerous reports of their abducting spacecraft and terrorizing any ships unfortunate enough to cross their path.",
        [11] = "Supreme Commander Dred Nicolson paid a visit to {0} this week.\n\nThousands crowded the Colonial Senate to hear his latest proclamations concerning the planet.",
        [12] = "In a speech this morning, the Governor of {0} promised to stimulate the economy and boost trade by easing regulations and making it more profitable for merchants to buy and sell commodities.",
        [13] = "Last night, the residents of {0} celebrated the birth of their Nation with breathtaking fireworks and a speech by the Colonial Elders.",
        [14] = "Last night, riots rocked {0} as citizens demanded greater autonomy from the Imperial Rule of Dred Nicolson.\n\nPolice responded by arresting hundreds of protesters.",
        [15] = "The economy of {0} has fallen into a slump.\n\nLocal officials blame the problem on unscrupulous traders and price gouging by local merchants.",
        [16] = "The economy of {0} has taken off.\n\nTrade is booming and locals are enjoying the benefits of their new found wealth.",
        [17] = "The indigenous creatures of {0} staged a rally this morning to show solidarity and press for more rights over their native lands.",
        [18] = "Top government officials on {0} have been indicted on corruption charges.\n\nThe Imperial Council says it will conduct a full investigation.",
        [19] = "The Environmental Coalition has targeted {0} in its war against pollution.\n\nThe Coalition is pressuring the governor for better regulations on the disposal of toxic chemicals and industrial waste.",
        [20] = "{0} is ranked as the most productive planet in the colonies by the Imperial Bureau of Statistics.",
        [21] = "A recent poll shows that {0}% of the population on {1} supports the current policies of Supreme Commander Dred Nicolson.",
        [22] = "A recent survey shows that {0}% of the population on {1} eats raw Biki Hoppers for breakfast.",
        [23] = "A recent survey shows that {0}% of the population on {1} buys imported Pupi Daga baby food.",
        [24] = "A recent poll shows that {0}% of the population on {1} bothers to vote in local elections.",
        [25] = "A recent poll shows that {0}% of the population on {1} feels crime is the planet's most serious problem.",
        [26] = "A recent poll shows that {0}% of the population on {1} would rather their planet break from the New Realm.",
        [27] = "A recent poll shows that {0}% of the population on {1} feels the economy will get worse over the next kuku year.",
        [28] = "A recent poll shows that {0}% of the population on {1} thinks the government should do more about the environmental degradation of the solar system.",
        [29] = "A recent poll shows that {0}% of the population on {1} believes Supreme Commander Dred Nicolson is the Divine Incarnation of the Great Bass.",
        [30] = "A recent poll shows that {0}% of the population on {1} is on a high Lissyreen health diet at the present time.",
        [31] = "A recent poll shows that {0}% of the population on {1} thinks that {2} is the best run trading company in Kukubia.",
        [32] = "A recent poll shows that {0}% of the population on {1} thinks that {2} is the worst run trading company in Kukubia.",
        [33] = "A recent poll shows that {0}% of the population on {1} thinks that {2} is the best run trading company in Kukubia.",
        [34] = "A recent poll shows that {0}% of the population on {1} thinks that {2} is the worst run trading company in Kukubia.",
        [35] = "The Supreme Commander Dred Nicolson promises to expand democracy to the colonies just as soon as the situation stabilizes.",
        [36] = "Now for our economic update.\n\nIt looks like there is an oversupply of {0} on {1} these days.  That means low prices for consumers and a good bargain for exporters.",
        [37] = "Now for our economic update.\n\nIt looks like the supply of {0} is running short on {1} these days.  That means high prices for consumers and a healthy profit margin for importers.",
        [38] = "The leading financial analysts at the {0} Exchange report that earnings are higher than expected, and the market looks bullish.\n\nThey predict the market will grow at a steady pace.  However, keep in mind, market analysts are not correct 100% of the time.",
        [39] = "There are reports that the Chichi Bobo Rebels are having a picnic on {0} this weekend.\n\nIf you're planning to travel to that area, you might want to bring along a little extra something in your basket because things could get nasty.",
        [40] = "Our sources indicate that the Darleen Smugglers are targeting the area around {0}.\n\nApparently, they have cut a deal with Lady Cornucopia, who is willing to purchase everything they can plunder.",
        [41] = "It appears that {0} is the new stalking grounds for the Baid-Rowel Bandits.\n\nWe have reports of these remorseless thugs attacking ships while dressed only in designer underpants.  Apparently, they think this enhances the overall feeling of bedlam.",
        [42] = "{0} has erupted in flames as hordes of Mooglers riot in the streets.\n\nThese fanatical anarchists are protesting the prevalence of traffic signs, which they insist infringe upon their personal liberties.  They have also been attacking any vehicles, including cargo ships, which happen to obey the traffic laws.",
        [43] = "A heard of Fez Fa Fa escaped from their sanctuary last night and are heading towards {0}.\n\nThese ravenous eating machines have already consumed a woman, her two pet Poffers, and a lawn mower.",
        [44] = "We have reports that the legendary Hungo Warriors have attacked a freighter orbiting {0}.\n\nThese extraordinary fighters are known to disguise their ships as asteroids and literally ram into their victims.",
        [45] = "The Cylet Mind Buggers have been using their incredible mental powers to harass ships around {0}.\n\nIf you're headed in that direction be prepared for a mental assault.",
        [46] = "Herds of mindless Lippo Jungies are known to be performing their seasonal mating rituals off planet {0} this week.\n\nThese clumsy space-whales are not outright hostile.  However, they have been known to accidentally crush anything that happens to be in their way.",
        [47] = "We have reports that a pack of parasitic Wicky Wicks have been sucking the life blood out of ships near {0}.\n\nIf you are headed for the region next week, you are advised to reconsider your travel plans.",
        [48] = "A band of space pirates are reported to be looting, pillaging and spray-painting silly slogans on ships in the area around {0}.",
        [49] = "This morning, Supreme Commander Dred Nicolson gave a speech about soap to an audience on {0}.\n\nEmperor Dred's great fear is that one day the galaxy will simply run out of soap.",
        [50] = "The Governor of {0} was arraigned today on charges of taking bribes and kickbacks from local industrialists.",
        [51] = "The indigenous population of {0} held a candlelight vigil in honor of Chichi Bobo, who they believe is a martyr to the cause of planetary independence.",
        [52] = "The streets of {0} turned into a battle ground last night as thousands of laborers rioted.  They were protesting the harsh working conditions and low pay.",
        [53] = "The economy of {0} has taken a turn for the worse.  Statistical data shows that the gross domestic product is down by {1}%.",
        [54] = "The economic conditions on {0} have shown a marked improvement over the last quarter.  Exports are up {1}% and inflation appears to be on the down swing.",
        [55] = "The indigenous creatures of {0} performed their annual Moxy Milk bath rituals, which they believe will drive off the evil spirits and restore harmony to their souls.",
        [56] = "Five top cabinet members on {0} have resigned after their party refused to raise the minimum wage for workers on the planet.",
        [57] = "The Environmental Coalition on {0} has come out strongly against the policy of dumping toxic fuel into the planet's oceans.\n\nThey say millions of fish die every year due to this type of reckless behavior.",
        [58] = "A new report released by the Bureau of Statistics ranks the population of {0} as the laziest in Kukubia.  They spend {1}% more time doing nothing than the average Kukubian.",
        [59] = "A recent poll shows that {0}% of the population on {1} consumes two or more post nasal drip pills before going to sleep at night.",
        [60] = "A recent poll shows that {0}% of the population on {1} would rather have two Malakite sandwiches for lunch for 6 months straight than inform a co-worker that he or she has a body odor problem.",
        [61] = "A recent poll shows that {0}% of the population on {1} prefers to drink their Horpi Lactate directly from the nipples of a pregnant Horpi Cow rather than out of a bottle.",
        [62] = "A recent poll shows that {0}% of the population on {1} have had some form of toe surgery in the past 12 months.",
        [63] = "A recent poll shows that {0}% of the population on {1} feel criminals should be given an education in prison rather than being forced to spend 12 hours a day watching TV.",
        [64] = "A recent poll shows that {0}% of the population on {1} have at least one Lava Lamp in their homes.",
        [65] = "A recent poll shows that {0}% of the population on {1} have at least 1,000 kubars invested in the {2} Stock Exchange.",
        [66] = "A recent poll shows that {0}% of the population on {1} supports a recycling program for all paper and plastic products produced on the planet.",
        [67] = "A recent poll shows that {0}% of the population on {1} have considered naming at least one of their children Dred.",
        [68] = "A recent poll shows that {0}% of the population on {1} believes that the government should do more to curb the power of large interplanetary corporations.",
        [69] = "A recent poll shows that {0}% of the population on {1} feel that {2} is charging too much for passenger fares.",
        [70] = "A recent poll shows that {0}% of the population on {1} believe that {2} is partially responsible for the high price of products on the planet.",
        [71] = "A recent poll shows that {0}% of the population on {1} believe that {2} is charging too much for passenger fares.",
        [72] = "A recent poll shows that {0}% of the population on {1} thinks that {2} is partially responsible for the high price of products on the planet.",
        [73] = "The Supreme Commander Dred Nicolson said he fully supports the concept of democracy in theory.\n\nHowever, in practice, he simply cannot trust the people to make the right choices.",
        [74] = "A recently released government report indicates that there is now an abundance of {0} on {1}.\n\nThis glut of product should have some impact upon prices in the region.",
        [75] = "A recently released government report indicates that there is now a scarcity of {0} on {1}.\n\nThis shortage should help to drive up prices in the region.",
        [76] = "Traders on the {0} Exchange feel a bull market is bound to drive up stock prices in the near future.\n\nHowever, their optimistic outlook is based more on the current market trends than any underlying economic factors.",
        [77] = "The Baid-Rowel Bandits have released a public statement saying that in a good faith effort, they will reduce their looting and pillaging by 33% over the next 24 months.",
        [78] = "Despite their growing popularity, the Mooglers, a rowdy bunch of anarchists, have been unsuccessful at forming any sort of leadership because it goes against all their principles.",
        [79] = "Animal rights activists have taken to the streets across Kukubia this week to protest the slaughter of nearly forty Fez Fa Fa.\n\nApparently, nearly forty Fez Fa Fa, wild creatures with an appetite for raw flesh, escaped from their sanctuary last month and attacked an orbiting hamburger factory, devouring 70,000 patties along with the entire staff.",
        [80] = "The Hungo Warriors met today with the Imperial Magistrate and demanded that Dred Nicolson surrender or they would continue their war against the Nicolson Dynasty.\n\nNeedless to say, the Imperial Magistrate did not surrender the entire empire to a few hundred renegades.",
        [81] = "A gathering of pirates turned into a blood bath last night.\n\nWord is that during the Ceremonial Buccaneer Barbecue, one of the pirate clans showed up with less than enough BBQ ribs for everyone.  This led to a dispute, which quickly escalated into a full-fledged fight.\n\nBy the time it ended, dozens of pirates lay slain.",
        [82] = "Scientists at the Imperial University have been researching the extraordinary mental powers of the Cylet Mind Buggers.\n\nUsing a brain extracted from one of these creatures, they managed to toast a slice of sourdough bread.  Further experiments on other food products are anticipated.",
        [83] = "The Lippo Jungie Task Force has been on high alert.\n\nThese mindless space-whales are expected to be passing through Kukubia sometime in the next few weeks, and the task force doesn't want any unpleasant accidents to take place.",
        [84] = "A council has been convened to discuss ways of dealing with the Wicky Wicks, a group of notorious parasitic creatures who prey on unsuspecting merchant ships.",
        [85] = "A family on {0}, who built their home over an old pirate graveyard, claims to have seen the spirit of Bro Nap Goodshark walking around singing dirty songs and cracking eggs on his head.",
        [86] = "L-Tech Engines claims it has been experiencing production delays this past month due to a new automated production line it just completed.\n\nThe Worker's Union refused to comment, except to say \"I told you so.\"",
        [87] = "Scooter Jay, a notorious smuggler, was caught last night with 200 tons of illegally imported Lava Lamps from the galaxy of Pata Pata Pita.\n\nEmperor Dred Nicolson decreed that Scooter Jay will have to pay a one million kubar fine for this transgression.",
        [88] = "Mr. Hands showed up in court today and pleaded guilty to misdemeanor charges of unknowingly selling stolen goods.\n\nThe Imperial Prosecution arranged this plea bargain because they lack the necessary funding and staff to take Hands to trial and win the case.",
        [89] = "Quist, a high flying financier, was arraigned in court today on charges of fraud.\n\nApparently, he is being accused of swindling nearly 2 million kubars out of one of his clients.  The trial will begin next month.\n\nIn the meantime, Quist is already out of jail on bail of only 50,000 kubars.",
        [90] = "Emperor Dred Nicolson met with the governor of {0} today to discuss the possibility of injecting liquid soap into the planet's water supply.\n\nThe Emperor feels this would save time and soap.  The Governor, however, wouldn't commit, fearing the water may not taste right.",
        [91] = "There has been a warehouse shortage on {0}.  Merchants report paying higher prices and struggling to find adequate space for their products.",
        [92] = "Emperor Dred Nicolson attended the annual Eat With Your Feet festival on {0}.\n\nDuring this one day festival, everyone is required to both prepare and eat a meal entirely with their feet.  The Emperor wound up preparing an elegant dish of Humpato stew, which he only spilled twice.",
        [93] = "We have a report that Mr. Zinn, the wealthiest merchant on Zile, has hit hard times.  He claims his fortunes have been depleted after a series of bad loans.\n\nThis, however, cannot be confirmed, and some suspect it may be a clever ploy to avoid paying taxes.",
        [94] = "Lady Cornucopia reportedly increased her defense spending by 200% over the past year.\n\nThis is seen as a move towards further independence from the New Realm.",
        [95] = "Emperor Dred Nicolson attacked the Traders' Union, saying it controls a monopoly on banking inside Kukubia and is holding back growth.",
        [96] = "Emperor Dred Nicolson mentioned today that he is now constructing a summer home on Asteroid B153-89065, the largest and slowest moving asteroid in the Galaxy of Gogg.",
        [97] = "Scientists at the Imperial University are working on developing a new type of ship with double the cargo capacity.\n\nThey expect to have a test model ready by early next year.",
        [98] = "The Ship Worker's Union held a rally today on {0} to protest the installation of new robots in the main shipyard.\n\nThey insist that replacing intelligent beings with computers can only lead to disaster.",
        [99] = "The Imperial Council has offered the Chichi Bobo Freedom Fighters general amnesty if they agree to put down their weapons and give up their struggle against the government.\n\nThe Freedom Fighters said they will agree to those terms just as soon as Emperor Dred Nicolson grants the Kukubian Colonies their independence.",
        [100] = "The Darleen Smugglers were reported to be having a vacation on {0}.\n\nIn fact, several witnesses claim to have spotted them sunbathing on the local beaches.",
        [101] = "The Wobbler has announced a new experimental play will be opening on {0} this week.\n\nThe Wobbler claims this play will shock the pants off most Kukubians.  However, that remains to be seen.",
        [102] = "Brow, a notorious industrial spy, started advertising in the newspapers on {0} this week.\n\nBrow hopes that his new publicity campaign will boost his image and bring in clients.",
        [103] = "Yoyo, the well-known gambler, was arrested yesterday after trying to cheat an undercover police officer out of 5,000 kubars.\n\nYoyo is expected to be released on bail by tomorrow morning.",
        [104] = "Limpus, a renowned humanitarian and philanthropist, said he is establishing a children's hospital on {0} this week.\n\nHe plans on naming it the Limpus Loves Children Center.",
        [105] = "Kukubia's casinos reported record profits last quarter.  The manager of one of the casinos said:\n\n\"People should be happy to lose money to us.  Otherwise, they would end up gambling in some dirty basement, instead of these luxury resorts.\"",
        [106] = "The Minster of Trash reported that the amount of garbage being taken to Kukubia's dumps has fallen by 44% in the past year, due primarily to improved recycling facilities in all the major cities.",
        [107] = "The Soothsayer's Union lodged a complaint today with the Minister of Predictions, claiming that unregistered Soothsayers were handing out erroneous readings.",
        [108] = "Horkywood is pumping out movies these days, and many of them seem to be about the royal Nicolson Family.\n\nTwo new feature films will depict the heroic life of the Great Bass Nicolson, while a third film will chronicle the childhood of Emperor Dred Nicolson.",
        [109] = "{0} is hosting the 134th Annual Stock Broker's Convention this week.\n\nBrokers from all across the galaxy are expected to show up and exchange hot tips and insider information.",
        [110] = "A large explosion shook {0}  today when a giant oil tanker went up in flames.  This is the third such incident this year, and local authorities are investigating the cause.\n\nThe owners of the tanker insist that it is nothing to worry about... just a small fuel leak.",
        [111] = "The Imperial Council met on {0} today to debate whether or not to triple the import and export tariffs throughout Kukubia.\n\nOutside, merchants protested claiming this could bankrupt their companies.",
        [112] = "L-Tech, the largest manufacturer of engines in Kukubia, announced it will be laying off 30% of its work force in hopes of increasing corporate profits and boosting shareholder value.",
        [113] = "The Governor of {0} has drafted a new initiative to save the planet's historic business district.\n\nOver the past decade, pollution combined with poor maintenance has led to the disintegration of many of the planet's most prized landmarks.",
        [114] = "Voyager's Insurance Company came under attack today for charging excessively high premiums.\n\nThe Cosmic Consumer Caucus released figures showing that Voyager's is making record profits at the expense of business owners and the general public.",
        [115] = "We have reports that the Chichi Bobo Rebels have moved into the area around {0}.\n\nOur sources indicate Supreme Commander Dred Nicolson is planning a counter attack this week and fighting will intensify.",
        [116] = "Our sources indicate that the Darleen Smugglers have increased their activity on and around {0}.\n\nThey appear to be attacking cargo ships importing goods to the planet.",
        [117] = "{0} has seen an upsurge in crime with the Baid-Rowel Bandits playing a major role.\n\nThey appear to be targeting merchant ships and passenger liners.",
        [118] = "Last night, {0} was plunged into chaos as the Mooglers, a rowdy group of anarchists, took to the streets to protest Supreme Commander Dred Nicolson's colonial rule.\n\nWe have reports of them attacking anything that gets in their way.",
        [119] = "Our sources inform us that a group of wild Fez Fa Fa broke away from their sanctuary yesterday and are headed towards {0}.\n\nThey are known to eat anything in their path, including Kukubian citizens.",
        [120] = "Sources indicate that the Hungo Warriors have taken to the war path near planet {0}.\n\nThese legendary fighters are pillaging local colonies, merchant ships and government outposts.",
        [121] = "The Cylet Mind Buggers are reported to be operating out of the {0} region.\n\nThey appear to be using their mental powers to attack vessels approaching or leaving the planet.",
        [122] = "Herds of mindless Lippo Jungies were spotted migrating towards {0}.\n\nThese enormous roaming space-whales seem to be crushing anything that gets in their path.",
        [123] = "A group of Wicky Wicks have been spotted moving towards {0}.\n\nThese parasitic creatures have been preying on any merchant ships they encounter.  If you are headed for the region, you are advised to change your travel plans.",
        [124] = "Space Pirates, descendants of the infamous Bro Nap Goodshark, have been spotted operating around {0}.\n\nWe have numerous reports of their abducting spacecraft and terrorizing any ships unfortunate enough to cross their path.",
    };

    private static string OriginalNewsReport(GameSession session)
    {
        if (session.WeeklyStockNewsCode is >= 1000 and <= 1003 &&
            !string.IsNullOrWhiteSpace(session.WeeklyStockNewsPlanet))
            return StockNewsReport(session);
        var random = new Random(GameMath.StableHash(session.Seed, session.Week.ToString(), "explore news"));
        var report = random.Next(1, 125);
        return FormatOriginalNewsReport(session, report);
    }

    private static string StockNewsReport(GameSession session)
    {
        var planet = session.WeeklyStockNewsPlanet;
        return session.WeeklyStockNewsCode switch
        {
            1000 => $"The {planet} Exchange shot up this week!!!\n\nThe average share price gained an amazing {session.WeeklyStockNewsPoints:N0} points on heavy trading.\n\nWe have reports from the {planet} Exchange that a number of big investors, including Mr. Zinn, made a tidy profit on this unexpected market shift.",
            1001 => $"The {planet} Exchange plummeted this week, as the average share price fell {session.WeeklyStockNewsPoints:N0} points.\n\nWe have reports from the {planet} Exchange that a number of big investors lost a fortune in this unexpected down-turn.",
            _ when session.StockTrends.GetValueOrDefault(planet, 50) <= 50 =>
                $"Traders at the {planet} Exchange are expecting the worst.\n\nMarket analysts predict a downward trend in the stock market on planet {planet}.  This bear market is likely to affect the share prices of the {planet} Exchange.",
            _ => $"Traders on the floor of the {planet} Exchange are expecting to see some profits in the long term.\n\nAnalysts are predicting the {planet} Exchange is about to take off on a gradual upward trend."
        };
    }

    public static void ApplyWeeklyNewsSignal(GameSession session)
    {
        session.WeeklyStockNewsCode = 0;
        session.WeeklyStockNewsPlanet = string.Empty;
        session.WeeklyStockNewsPoints = 0m;
        // Decompiled frm_Travel3_newsEvent: special financial news begins on
        // turn four and has a 15% weekly chance. It is separate from the
        // ordinary 1..124 flavour-news table.
        if (session.Week < 4 || session.Planets.Count == 0) return;
        var random = new Random(GameMath.StableHash(session.Seed, session.Week.ToString(),
            "weekly stock news"));
        var newsData = random.Next(1, 101);
        var exchange = session.Planets[random.Next(session.Planets.Count)];
        if (random.Next(1, 101) > 15 || session.CrashedExchanges.Contains(exchange)) return;

        var news = random.Next(1000, 1004);
        var price = session.SharePrices.GetValueOrDefault(exchange);
        var movement = (newsData / 4m + 25m) / 100m;
        switch (news)
        {
            case 1000 when price > 250m:
                session.SharePrices[exchange] = decimal.Floor(price * (1m + movement));
                session.StockTrends[exchange] = 50;
                session.WeeklyStockNewsCode = news;
                session.WeeklyStockNewsPlanet = exchange;
                session.WeeklyStockNewsPoints = session.SharePrices[exchange] - price;
                break;
            case 1001 when price > 250m:
                session.SharePrices[exchange] = decimal.Floor(price * (1m - movement));
                session.StockTrends[exchange] = 50;
                session.WeeklyStockNewsCode = news;
                session.WeeklyStockNewsPlanet = exchange;
                session.WeeklyStockNewsPoints = price - session.SharePrices[exchange];
                break;
            case 1002 or 1003:
                // Both original branches reinforce the exchange's existing
                // direction: 50 or below becomes a certain bear roll, while
                // above 50 becomes a certain bull roll for the following week.
                session.StockTrends[exchange] =
                    session.StockTrends.GetValueOrDefault(exchange, 50) <= 50 ? 0 : 100;
                session.WeeklyStockNewsCode = news;
                session.WeeklyStockNewsPlanet = exchange;
                break;
        }
    }

    private static string FormatOriginalNewsReport(GameSession session, int report)
    {
        report = Math.Clamp(report, 1, 124);
        var random = new Random(GameMath.StableHash(session.Seed, session.Week.ToString(), "explore news", report.ToString()));
        var planet = session.Planets.Count == 0 ? "Kukubia" : session.Planets[random.Next(session.Planets.Count)];
        var company = session.Companies.Count == 0 ? "a local trading company" : session.Companies[random.Next(session.Companies.Count)].Name;
        var commodity = CommodityCatalog.All[random.Next(CommodityCatalog.All.Length)].Name;
        var percent = random.Next(1, 101).ToString(CultureInfo.InvariantCulture);
        if (report is 38 or 76)
        {
            var trend = Math.Clamp(session.StockTrends.GetValueOrDefault(planet, 50), 0, 100);
            if (report == 38)
                return trend switch
                {
                    >= 61 => $"The leading financial analysts at the {planet} Exchange report that earnings are higher than expected, and the market looks bullish.\n\nThey predict the market will grow at a steady pace. However, market analysts are not correct 100% of the time.",
                    <= 39 => $"The leading financial analysts at the {planet} Exchange report that the market looks bearish.\n\nEarnings are down, and they expect the stock market to decline. However, market analysts are not correct 100% of the time.",
                    _ => $"Financial analysts at the {planet} Exchange have mixed feelings about the market. Some are bullish while others are bearish."
                };
            return trend switch
            {
                >= 61 => $"Traders on the {planet} Exchange feel a bull market is bound to drive up stock prices in the near future.\n\nTheir outlook is based on current market trends.",
                <= 39 => $"Traders on the {planet} Exchange feel a bear market is bound to drive down stock prices in the near term.\n\nTheir outlook is based on current market trends.",
                _ => $"Traders on the {planet} Exchange have mixed feelings about the market. Some are bullish, while others are bearish."
            };
        }
        var arguments = NewsArguments(report, planet, company, commodity, percent);
        return string.Format(CultureInfo.InvariantCulture, OriginalNewsTemplates[report], arguments);
    }

    private static object[] NewsArguments(int report, string planet, string company, string commodity, string percent) => report switch
    {
        >= 21 and <= 30 or >= 59 and <= 64 or >= 66 and <= 68 => [percent, planet],
        >= 31 and <= 34 or >= 69 and <= 72 => [percent, planet, company],
        53 or 54 => [planet, percent],
        58 => [planet, percent],
        65 => [percent, planet, planet],
        36 or 37 or 74 or 75 => [commodity, planet],
        _ => [planet]
    };
}
