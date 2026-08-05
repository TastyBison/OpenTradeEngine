
using System;

namespace OpenTradeEngine;

public static class ArrivalDialogueCatalog
{
    private static readonly string[] Taunts =
    [
        "Sorry, but the early bird gets all the tastiest worms.",
        "It seems like your ship needs a bigger engine.",
        "It pays to be first!",
        "I hope there's something left for you.",
        "The first company to reach a planet gets first choice of commodities.",
        "You might try travelling to a closer planet next time.",
        "Better late than never.",
        "Don't worry, I'm sure there will be something left for you to buy.",
        "Faster is better.",
        "Can't win 'em all!",
        "It's time for you to upgrade your ship.",
        "It seems like the competition is getting quicker.",
        "Speed is a virtue.",
        "First come, first served.",
        "The first one to a planet gets the pick of the litter.",
        "It never hurts to be first.",
        "Don't worry, there's bound to be something left for you.",
        "The best products go quickly.",
        "The competition is hot to trot.",
        "You've got to be on the ball to beat the competition.",
        "You're getting slow in your old age.",
        "You're slowing down.",
        "There's a definite need for speed.",
        "Better to be fast than big.",
        "Be fast or be last.",
        "Can't win 'em all.",
        "A kuarp a day keeps the competition at bay.",
        "You'll be lucky to fill up your ship.",
        "Your pilot sure is slow.",
        "Better late than never.",
        "Sorry, but there won't be much left for you."
    ];

    public static TurnNotice Create(GameSession session, CompanyState speaker, CompanyState recipient,
        string planet, ArrivalPurchase? purchase)
    {
        var tauntIndex = Math.Abs((long)GameMath.StableHash(session.Seed, session.Week.ToString(),
            speaker.Name, recipient.Name, planet, "arrival taunt")) % Taunts.Length;
        var taunt = Taunts[tauntIndex];
        if (speaker.IsHuman)
        {
            return new TurnNotice($"{speaker.Name} Arrives on {planet} First!",
                $"\"{taunt}\"", $"SHIP{Math.Clamp(speaker.ShipNumber, 1, 12)}.SWF",
                $"SHIP{Math.Clamp(speaker.ShipNumber, 1, 6)}.MP3", true);
        }

        var profile = AiOpponentCatalog.ForCompany(speaker.Name);
        var purchaseText = purchase is { } cargo
            ? $" and buys up {cargo.Quantity} tons of {CommodityCatalog.All[cargo.CommodityIndex].Name} at a bargain price!"
            : " before you can place an order!";
        return new TurnNotice($"{speaker.Name} Beats You to {planet}",
            $"{speaker.Name} beats you to {planet}{purchaseText}\n\n\"{taunt}\"",
            $"OP{profile.Number}.PNG", $"OP{profile.Number}.MP3", true);
    }
}

public readonly record struct ArrivalPurchase(int CommodityIndex, int Quantity);
