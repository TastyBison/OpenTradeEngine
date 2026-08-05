namespace OpenTradeEngine;

public static class TutorialCatalog
{
    private static readonly string[] SinglePlayer =
    [
        "Welcome to the Tutorial!\n\nThe game will start off simple and gradually become more sophisticated. Once you learn the basic rules, you can add features at your own pace.",
        "Tutorial!\n\nThis turn introduces Zinn's Loan. You borrowed from Mr. Zinn to purchase your spaceship, and he charges 4% interest every week.",
        "Tutorial!\n\nThis turn introduces Supply. The Supply Chart shows how plentiful each commodity is on every planet. Plentiful goods tend to be cheap; rare goods tend to be expensive.",
        "Tutorial!\n\nThis turn introduces the Traders' Union Loan, which allows you to borrow operating money.",
        "Tutorial!\n\nThis turn introduces Voyager's Insurance, which protects your company from applicable disasters during the next journey.",
        "Tutorial!\n\nThis turn introduces View City. You can look around the city you are visiting and read about the planet's history.",
        "Tutorial!\n\nFuel is now required for your spaceship. From the next turn onward, you may decide when to add each remaining feature.",
        "Tutorial!\n\nPassengers are now available. You can make money transporting them between planets. The next feature is Crew.",
        "Tutorial!\n\nCrew is now active, so you must pay your ship's crew a salary. The next feature is Advertising.",
        "Tutorial!\n\nAdvertising is now available for attracting passengers and cargo. The next feature is Taxes.",
        "Tutorial!\n\nTaxes are now active and must be paid as part of running your business. The next feature is the Bank.",
        "Tutorial!\n\nThe Bank is now available, allowing you to deposit savings and earn interest. The next feature is Warehouses.",
        "Tutorial!\n\nWarehouses are now available on all seven planets. The next feature is Explore Planets.",
        "Tutorial!\n\nExplore Planets is now available for city information, weather, news and local history. The next feature is the Distance Chart.",
        "Tutorial!\n\nThe Distance Chart now shows the distances between planets and the locations of competing ships. The next feature is Facilities.",
        "Tutorial!\n\nFacilities and government auctions are now active. Facility owners collect a fee when competitors visit that planet. The final feature is Stock Markets.",
        "Tutorial Completed!\n\nStock Markets and File Options shortcuts are now available. You are playing the complete game."
    ];

    private static readonly string[] Multiplayer =
    [
        SinglePlayer[0], SinglePlayer[1], SinglePlayer[2], SinglePlayer[3], SinglePlayer[4], SinglePlayer[5],
        "Tutorial!\n\nThis turn introduces Fuel. You must now purchase fuel for your spaceship.",
        "Tutorial!\n\nThis turn introduces Passengers, allowing you to make money transporting people between planets.",
        "Tutorial!\n\nThis turn introduces Crew. You must now pay your ship's crew a salary.",
        "Tutorial!\n\nThis turn introduces Advertising for attracting passengers and cargo.",
        "Tutorial!\n\nThis turn introduces Taxes. Your company must now pay its taxes and tariffs.",
        "Tutorial!\n\nThis turn introduces the Bank, where savings earn weekly interest.",
        "Tutorial!\n\nThis turn introduces Warehouses for storing commodities on all seven planets.",
        "Tutorial!\n\nThis turn introduces Explore Planets, including weather, news and local history.",
        "Tutorial!\n\nThis turn introduces the Distance Chart for comparing planetary distances and competitor locations.",
        "Tutorial!\n\nThis turn introduces Facilities and the government auctions that award them.",
        SinglePlayer[16]
    ];

    public static string Text(int stage, bool multiplayer, decimal cash, decimal zinnLoan)
    {
        stage = System.Math.Clamp(stage, 1, 17);
        var text = (multiplayer ? Multiplayer : SinglePlayer)[stage - 1];
        return stage == 1
            ? $"{text}\n\nYou begin with {cash:N0} kubars in cash and a debt of {zinnLoan:N0} kubars to Mr. Zinn."
            : text;
    }
}
