using System;
using System.Collections.Generic;

namespace OpenTradeEngine;

public static partial class ExploreContentCatalog
{
    public static IReadOnlyList<string> TimePages { get; } =
    [
        "As you may have guessed, Kukubians go by the kuku clock, which was invented by Mono Cheezo, the first King of Kukubia.\n\nBecause he had nothing better to do, Mono Cheezo declared that one kuku day should equal the time it takes a cup of Bool Juice to evaporate from a freshly opened Bool Nut shell (which equals the time it takes light to travel 18.6 billion miles).",
        "This is in contrast to a typical earth day, which is equal to the time it takes light to travel 16.07 billion miles.\n\nAnother unique feature of Kukubian time is that it follows the Nicolsonian Time Tables, a testament to Emperor Dred Nicolson's supreme ego.\n\nThe kuku calendar begins with the year 0 A.B. because Emperor Dred decided that the most important day in history is the day he was born. A.B. stands for After the Birth of Supreme Commander Dred Nicolson.",
        "Any event that occurred before Emperor Dred was born is referred to as B.B. (Before the Birth).\n\nBass Nicolson ruled Gogg from 600 B.B. until 103 B.B. Then Hork Nicolson took power and ruled until 3 A.B., when he was assassinated.\n\nFrom 3 A.B. until 24 A.B. there was chaos in the Imperial Government. Then the military intervened and reinstated Dred Nicolson to the throne.",
        "According to our records, most Kukubians live to be well over 500 kuku years old.\n\nThe date is 139 A.B. Every time you trade commodities between two planets, it takes approximately one kuku week. On average, merchants spend between 16 and 48 kuku hours travelling and five kuku days conducting business on the planet.",
        "One final piece of trivia: other systems in the Galaxy of Gogg begin their calendars with the Supreme Commander's wedding day, the first time he hunted wild space snakes, or any other day he deems important.\n\nThis becomes confusing when travelling between systems. Fortunately, for this game, you will not have to leave the Kukubian solar system."
    ];

    private static readonly Dictionary<string, string> FirstHistoryPages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Vexx"] = "In 300 B.B., Mono Cheezo, a failed insurance salesman and amateur explorer, decided it was his destiny to seek out and discover new planets and achieve greatness and glory. He sold his home, bought an ancient 200-ton dairy transport ship, hired a ragtag crew, and headed off for the far reaches of Gogg.\n\nAfter months of zig-zagging across the galaxy, he finally crashlanded on Vexx, an uncharted virgin planet, or so he thought.",
        ["Pyke"] = "The Hungo Warriors colonized Pyke 800 years before anyone in the New Realm ever heard of the planet.\n\nThe legendary Hungo Warriors were known throughout the galaxy for their ongoing war with the Army of the New Realm. The Hungo won every battle they fought, but they were losing the war. Attrition had reduced the small tribe to a fraction of its original numbers, yet they continued to fight on.",
        ["Mira"] = "As far back as 1,000 B.B., Mira was awash in religious and spiritual energy. The native Quaso Mutta are gelatinous blobs who spend days sitting on the green sandstone plains vibrating with Jaalesh. Their lives are spent communicating in silence with the non-material world.\n\nTheir silent prayers would have gone uninterrupted if it had not been for one man, Lily Slimwagon.",
        ["Stye"] = "In 103 B.B., after the death of the great Bass Nicolson, the Kukubian system was still forbidden to all but government spacecraft. This was intended to prevent the re-emergence of pirates and other dangerous forces. Although trade was limited, a few planets began to develop significant economies.\n\nOne of those was Stye, which rapidly grew into the financial capital of Kukubia.",
        ["Loro"] = "Loro became known as the Pleasure Planet because of the genius of Peelia Veelia, leader of the Mamo Wuzzies.\n\nMamo Wuzzies are bulky, energetic creatures who inhabited Loro before humans ever set foot in Kukubia. Peelia Veelia saw the expansion of the New Realm as the perfect opportunity to satisfy her subjects' basic need for stimulation.",
        ["Zile"] = "Zile was colonized in 430 B.B. by a notorious pirate clan from Outer Glosser.\n\nThe leader of the clan, Bro Nap Goodshark, was an unscrupulous smuggler who dealt primarily in weapons and Agmal Juice. The Imperial Government was hot on his trail. Zile seemed to be the perfect place to hide out and set up a new operation.",
        ["Frac"] = "Frac was the first planet in the Kukubian system to be colonized.\n\nSir Lily Slimwagon, a fearless and somewhat eccentric explorer, arrived on Frac in 551 B.B. He found a lush, tropical planet inhabited by Veggie Piddles, intelligent vegetable creatures who thrived on vine juice and lemon pudding.",
        ["Tilo"] = "Tilo is the only planet in the Kukubian System with legalized gambling. Supreme Commander Dred Nicolson detests gambling and has outlawed casinos in most major cities.\n\nCasinos take money away from the public, and Dred would much rather they spend their hard-earned money on taxes, which go directly to him.",
        ["Queg"] = "Queg is the infamous renegade planet. It is here that the galaxy's most notorious gangsters come to unload their wares.\n\nA powerful warlord named Lady Cornucopia rules the planet and brokers all transactions. Because Emperor Dred needs her in his on-going skirmish with the Hapa Jillos, he tolerates her illicit activities.",
        ["Xeen"] = "Xeen is one giant junkyard filled with spare parts and brilliant mechanics.\n\nXeen got its start after Bass Nicolson defeated the Hapa Jillo Empire and needed somewhere to dump his old and battered battle ships. Since Xeen was inhospitable and had no native intelligent life, it seemed the natural place for a junkyard.",
        ["Ooom"] = "Ooom is known throughout the Kukubian System as the Fortune Teller's Planet. Its Soothsayers can predict the future by scanning one's aura for positive or negative energy.\n\nAs far back as 400 B.B., Bass Nicolson consulted the Soothsayers before entering battle. This may partly account for his victory over the Hapa Jillo Empire.",
        ["Hork"] = "The planet Hork is named after Emperor Hork Nicolson, Emperor Dred's father.\n\nHork Nicolson saw the planet as the hub of his propaganda machine. He was losing control of the Kukubian Colonies, but his efforts at regulating all media and cramming political slogans down everyone's throat only made the rebellious colonies more upset.",
        ["Bass"] = "Bass is named after the great Bass Nicolson, Dred Nicolson's grandfather.\n\nAfter defeating the Hapa Jillo Empire and the Pirates of Zile, he transported his plundered treasures here. A legion of accountants, merchants and financial types followed to catalogue everything and invest the profits.",
        ["Nosh"] = "The Quaso Mutta have revealed that millions of years ago, Nosh was a small star, literally a ball of fire fuelled by its super-heated core.\n\nA rare solar snow storm extinguished the surface. The planet cooled over the next million years, leaving huge fuel reservoirs that drove the initial expansion of the New Realm."
    };

    private static readonly string[] NewsReports =
    [
        "A recent poll shows that 25% of the population on Loro bothers to vote in local elections.",
        "The Astral Reporter says merchants across Kukubia are watching fuel prices closely this week.",
        "Officials on Stye insist that the Traders' Union remains financially sound despite persistent rumors.",
        "Tourism officials on Loro report another strong week for resorts and pleasure cruises.",
        "The Ministry of Travel reminds captains to check the Weather Bureau before leaving orbit.",
        "Analysts on Bass predict lively trading as investors react to the latest commodity reports."
    ];

    public static IReadOnlyList<string> HistoryPages(string planet) =>
        FullHistoryPages.TryGetValue(planet, out var pages)
            ? pages
            : [PlanetCatalog.Describe(planet)];

    public static string NewsReport(GameSession session) => OriginalNewsReport(session);
    public static string NewsReport(GameSession session, int report) => FormatOriginalNewsReport(session, report);
}
