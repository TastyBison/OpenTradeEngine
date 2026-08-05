using System;

namespace OpenTradeEngine;

/// <summary>The original seventy-entry Kukubian Weather Bureau report table.</summary>
public static class WeatherCatalog
{
    public static string Forecast(int code, string planet, int seed, int week)
    {
        if (string.IsNullOrWhiteSpace(planet)) return "No forecast is available this week.";
        code = Math.Clamp(code, 1, 70);
        var random = new Random(GameMath.StableHash(seed, week.ToString(), "weather report details", code.ToString()));
        var percent = random.Next(1, 101);
        var previous = random.Next(1, Math.Max(2, percent + 1));
        var deaths = random.Next(20, 20_001);

        return code switch
        {
            1 or 61 => $"Our satellites indicate you should be careful when travelling near {planet}.\n\nA meteor storm is expected to move into the area this week.",
            2 or 62 => $"We expect a solar storm to hit {planet} this week.\n\nIf you plan to travel in that area, be sure to prepare for some rough weather.",
            3 or 63 => $"This week's forecast shows a Space Hurricane forming around {planet}.\n\nIf you plan to visit {planet}, you might invest in a little Voyager's Insurance.",
            4 or 64 => $"The skies are looking quite turbulent on planet {planet} these days.\n\nThe locals inform us that a Stellar Typhoon will hit the planet any day now.",
            5 or 65 => $"It looks like a corrosive cloud of Dexxy Gas is moving towards {planet}.\n\nIf you plan on being in the area, you should beware.",
            6 or 66 => $"If you're planning a trip to {planet} any time soon, be on the lookout for Mippi Weeds.\n\nA sticky drift of weeds is headed in that direction.",
            7 or 67 => $"We predict a stream of acidic Bollup Juice will be flowing past {planet} in the near future.\n\nThis deadly liquid is known to cause severe damage to engine pods and hull liners.",
            8 or 68 => $"It seems like a pool of Wess Vapor is accumulating around {planet}.\n\nIt should reach harmful levels within a few days.",
            9 or 69 => $"Beware, a Stellar Whirlpool is forming off planet {planet}.\n\nIt should reach critical strength in the next few days.",
            10 or 70 => $"We have reports of a Bobble Warp, a dangerous break in the time-space continuum, flaring up in the region around {planet}.",
            >= 11 and <= 15 => "All is looking clear this week. No reports of meteor storms or space hurricanes yet.\n\nYou know what they say, \"No news is good news.\"",
            16 => $"{planet} has been experiencing some mighty dry weather lately.\n\nLocal Officials expressed fears that this may turn into a drought.",
            17 => $"It's cloudy over {planet} these days.\n\nExpect occasional showers, but it should clear up later in the week.",
            18 => $"Don't forget your raincoat if you're headed to {planet}.\n\nWe expect a healthy dose of wet weather.",
            19 => $"It looks like the skies around {planet} have cleared up.\n\nGood news for anyone heading that way.",
            20 => $"It's the perfect time of year to travel to {planet}.\n\nThe skies are looking beautiful and the Imo Blossoms are in full bloom.",
            21 => $"The weather around planet {planet} has taken a turn for the better these days.\n\nNo Bobble Warps or Dexxy Gas has been spotted for quite some time.",
            22 => $"It appears that the seasonal Meteor Storms around {planet} aren't coming this year.\n\nThat is good news for merchants operating in the region.",
            23 => $"We have reports that the Wess Vapor problem around {planet} has completely diminished.\n\nYou shouldn't experience any trouble if you are headed in that direction.",
            24 => $"The space around {planet} is black and beautiful this week.\n\nIt looks like clear sailing for those of you passing through the area.",
            25 => $"It's the perfect time of year to visit {planet} and join in the local Ipnu Festivals.\n\nThe weather is expected to be gorgeous for at least the next week.",
            26 => $"If you enjoy drinking Gapa Milk under milky green skies, now is a great time to head to {planet}.\n\nThe unusually humid weather has inspired the Gapa Cattle to produce record quantities.",
            27 => $"The unseasonably hot weather on {planet} has produced an explosion of Kele Jumpers.\n\nThese hungry insects are causing farmers a headache but shouldn't trouble visitors.",
            28 => $"The temperature on {planet} has dropped lately.\n\nThis climate change may trouble local farmers but should not inconvenience visitors.",
            29 => $"Don't expect perfect weather when you visit {planet}. Heavy rains are coming.\n\nOnly minor flooding is expected this year.",
            30 => $"The region around {planet} looks like it is finally clearing up.\n\nWe don't expect any new problems in the area for quite some time.",
            31 => $"A recent heat wave on {planet} has caused 2,000 tons of whipped cream to go rancid.\n\nThis is a major loss for the whipped cream industry.",
            32 => $"The freezing temperatures on {planet} have caused a number of outdoor lava lamps to stop functioning.",
            33 => $"Warm temperatures on {planet} caused the Yiffit Fly population to explode.\n\nVisitors should bring Yiffit repellent.",
            34 => $"The extremely hot weather on {planet} melted thousands of tons of jelly beans into giant jelly gobs.\n\nManufacturers must destroy them and write off the losses.",
            35 => $"The cold weather on {planet} kept people indoors, and local shop owners are suffering.",
            36 => $"An unexpected tidal wave on {planet} wiped out an entire village.\n\nFortunately, it happened during the Hagel Pilgrimage and the village was almost empty.",
            37 => $"During the heat wave on {planet}, locals began painting their skin silver to reflect the sun.\n\nVisitors are advised to bring sunglasses.",
            38 => $"Locals on {planet} are engaged in their annual snow eating contest.\n\nThe winner claims the Snow Stuffer of the Year Award.",
            39 => $"Mothers on {planet} claim the hot summer prevented numerous pregnancies.\n\nThe birth rate has fallen 14% compared with last year.",
            40 => $"Because of the heavy rains on {planet}, umbrella sales were well above average and store inventories are unusually low.",
            41 => $"Heavy snowfall on {planet} inspired a company to package and sell fresh snow to ski resorts.\n\nIt remains to be seen whether there is a market.",
            42 => $"Gusty winds and nonstop rain fell on {planet}.\n\nEmperor Dred was visiting and vowed never to return after taking the weather as a personal insult.",
            43 => $"An extended drought on {planet} ruined the entire year's grain harvest.",
            44 => $"A major earthquake shook {planet} late last night.\n\nAs of today, {deaths:N0} are reported dead and thousands are homeless.",
            45 => $"Devastating floods wiped out {percent}% of the farms on {planet}.\n\nEmperor Dred declared a planetary emergency and is sending aid.",
            46 => $"Warm weather triggered an explosion in the Catox Bug population, and farmers on {planet} are battling to save their crops.",
            47 => $"The weather on {planet} has been warm and beautiful. The Governor responded to a flood of tourists by outlawing nude sunbathing.",
            48 => $"The Kukubian Association of Meteorologists is planning a picnic on {planet} this weekend.\n\nUnfortunately, rain is forecast for both days.",
            49 => $"A new study shows our weather predictions are correct {percent}% of the time.\n\nThis is up from {previous}% last year.",
            50 => "The Kukubian Broadcasters Association is considering a fruits and vegetables channel featuring produce-related stories 24 hours a day.",
            51 => $"Clouds of space dust were spotted around {planet}.\n\nExperts say the dust may look ominous, but it should not impede travel.",
            52 => $"Clouds of green gas are rising from {planet}.\n\nScientists say they may look dangerous, but they are nothing to worry about.",
            53 => $"Last night, a meteor struck a merchant ship trying to land on {planet}. The ship was heavily damaged but landed safely.",
            54 => $"Clouds of radioactive particles were spotted near {planet}.\n\nExperts claim they are harmless and should disperse within days.",
            55 => $"A commuter craft struck a freighter approaching {planet}.\n\nIt exploded on impact, and everyone aboard is reported dead.",
            56 => $"A corrosive cloud of Dexxy Gas damaged a cargo ship headed for {planet}.\n\nNo one was injured and the gas has dispersed.",
            57 => $"A stream of Bollup Juice damaged a merchant ship near {planet}.\n\nThe stream has since been neutralized.",
            58 => $"A meteor shower rained down on {planet}.\n\nSeventeen buildings were damaged and two individuals were injured.",
            59 => $"Dense gas around {planet} created a traffic jam.\n\nShips must travel at a snail's pace until the gas can be dispersed.",
            _ => $"A survey indicates weather-related accidents across Kukubia are down {percent}% from last year.\n\nPerhaps more travellers are planning safer trips."
        };
    }
}
