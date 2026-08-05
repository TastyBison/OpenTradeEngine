namespace OpenTradeEngine;

/// <summary>Help copy transcribed from the original GameStrings library.</summary>
public static class OriginalHelpCatalog
{
    public const string MainMenu =
        "The Main Menu is the heart of the game. From this menu you can access every major sub-menu.\n\n" +
        "If you want to save your game, quit or start a new game, click on the File Options button.\n\n" +
        "In addition, most sub-menus contain a context-sensitive Help button. If you have any questions, " +
        "click the Help button for an explanation of that menu.";

    public static string Crew(CompanyState company) =>
        $"The number of employees you have depends on the size and type of your ship.\n\n" +
        $"Your company currently employs {company.CrewCount} crew members. You must pay each employee " +
        $"a weekly salary of {company.CrewSalary:N0} kubars.\n\n" +
        "You will not be required to hire any additional employees until you purchase a larger ship.";

    public static string Fuel(CompanyState company) =>
        $"Your ship has a {company.FuelCapacity:N0}-ton fuel tank and runs on Ionic Fuel, a combination " +
        "of nuclear and synthetic fuels.\n\nFuel costs between 200 and 2,000 kubars per ton.\n\n" +
        "Every time you travel to a planet, your ship consumes between 2 and 16 tons of fuel.\n\n" +
        "On average, the longer the distance travelled and the larger the ship, the more fuel is used. " +
        "If you want to save fuel, travel to a nearby planet rather than one on the other side of Kukubia.";

    public static string Journey(string planet) =>
        $"You are on {planet}.\n\nClick on the planet you wish to travel to.\n\n" +
        "The shorter the distance and the faster your ship, the greater the chance you'll arrive first " +
        "and be able to buy up all the best products!";

    public const string Insurance =
        "Any time you are on a planet, you may purchase Voyager's Insurance.\n\n" +
        "This insurance will protect you against a number of misfortunes which may occur during space travel, " +
        "such as damage from asteroids, losses due to space pirates, warehouse fires, and much more.\n\n" +
        "If you purchase insurance, it will only cover your next trip. You must pay an insurance premium every " +
        "week if you want to be fully covered.\n\nYour insurance costs will rise and fall depending on local " +
        "conditions, natural disasters, your accident record and fluctuations in the insurance market.\n\n" +
        "If you purchase a larger ship or more warehouse space, your insurance costs will also rise.";

    public const string Shortcuts =
        "The Shortcuts menu allows you to speed up the game by cutting down on the number of mouse clicks " +
        "required to perform routine tasks.\n\nWhen a shortcut is activated, it allows you to pay your crew, " +
        "fill up your fuel tank, or buy insurance simply by clicking on the appropriate Main Menu button. " +
        "This way you can bypass the sub-menus.";

    public static string Taxes(CompanyState company) =>
        "The Imperial Government, which rules over all of Kukubia, levies two types of taxes: a Passenger Tax " +
        $"and a Commodities Tariff.\n\nThe Passenger Tax is a {company.PassengerTaxRate}% tax on all income " +
        $"from passengers.\n\nThe Commodities Tariff is a {company.ExportTariffRate}% tax on all goods exported " +
        $"from a planet and a {company.ImportTariffRate}% tax on all goods imported to a planet. If the goods " +
        "never leave a planet, you will not be charged any tariffs.";

    public static string Passengers(CompanyState company) =>
        $"Your ship can accommodate up to {company.PassengerCapacity} passengers.\n\n" +
        "You can charge as much or as little as you like for tickets. If your ticket price is too high, nobody " +
        "will want to travel on your ship. If your ticket price is too low, it will be difficult to make a profit.\n\n" +
        "In the future, you may be given the opportunity to buy a larger ship. The larger your ship, the more " +
        "passengers you can carry.";

    public static string Explore(string planet) =>
        "While on a planet you may visit any of the local institutions.\n\nThe Special allows you to access each " +
        "planet's special resources such as banking, insurance, taxes, religion, engine construction and so on.\n\n" +
        "The Weather can alert you to dangerous storms in the area.\n\nThe News can provide you with updates on " +
        $"pirate attacks, revolutions and other events which will affect your business.\n\nThe Time and About {planet} " +
        "sections are good sources of Kukubian trivia and background information.";

    public static string Marketplace(CompanyState company) =>
        "As a space merchant, your goal is to buy commodities for a low price on one planet, then travel to " +
        $"another planet and sell them for a substantially higher price.\n\nYour ship can hold up to {company.CargoCapacity} " +
        "tons of cargo. As long as you have enough money and room on your ship, you can purchase any of the " +
        "goods available for sale. If you make a mistake and buy the wrong commodity, return it right away for " +
        $"a full refund.\n\nYour Cargo is the amount on your ship. On {company.Planet} is the amount for sale " +
        "locally. You Paid is your average purchase price, Market Price is the local selling price, and Price " +
        "Range shows its lowest and highest possible price.\n\nShow Available includes local goods and your cargo; " +
        "Show Cargo displays goods already aboard; Show All displays every commodity.";

    public const string Supply =
        "This Supply Chart gives you a bird's-eye view of each planet's economy.\n\nIf the supply is 0%, the " +
        "goods are rare on the planet and the price tends to be higher. If the supply is 100%, the commodity is " +
        "plentiful and the price tends to be lower. Keep in mind that no economy is entirely predictable.\n\n" +
        "Prices can change overnight due to natural disasters, labor strikes, changes in government policy, " +
        "local wars and other unexpected events.\n\nShow Available includes local goods and your cargo; Show Cargo " +
        "displays goods already aboard; Show All displays every commodity.";

    public static string Warehouse(CompanyState company) =>
        $"You currently have {company.WarehouseCapacity} tons of warehouse space. That means you can store up to " +
        $"{company.WarehouseCapacity} tons of commodities on each planet.\n\nUnfortunately, you cannot increase your " +
        "warehouse space whenever you feel like it. The Traders' Union strictly regulates the sale of all space. " +
        "This helps maintain a trade balance between the planets and prevents larger companies from monopolizing " +
        "the market.\n\nAs a member of the Traders' Union, your name is automatically entered into a lottery. " +
        "If your name comes up, you will be given the chance to purchase more space.";

    public const string Advertising =
        "There are two types of advertising: Passenger Advertising and Commodity Advertising.\n\nThe more you spend " +
        "on Passenger Advertising, the greater the chance that more passengers will be waiting to buy tickets on " +
        "the next planet.\n\nThe more you invest in Commodity Advertising, the more units will be available for you " +
        "to purchase on the next planet. Commodity Advertising does not affect commodity prices.\n\nAdvertising is " +
        "only effective for one week. If you purchase a larger ship, advertising costs rise because you need more " +
        "passengers and commodities to fill it.";

    public static string Money(decimal target) =>
        $"The standard currency in Kukubia is the kubar. In order to win the game, your company must attain a " +
        $"net worth of {target:N0} kubars.\n\nYour net worth is computed by adding together your cash, savings, " +
        "stock and other assets, then subtracting any money you owe the Traders' Union and Mr. Zinn.";

    public const string ShipInfo =
        "The Ship Info screen provides you with details about your ship.\n\nClick on the buttons to find out " +
        "more about your ship, fuel tank, crew, engine, passenger capacity, cargo bay and fuel usage.";

    public static string ShipDetail(CompanyState company, string section) => section switch
    {
        "size" => $"You currently own a {company.ShipTons}-ton ship.\n\nThis does not mean you can carry " +
            $"{company.ShipTons} tons of cargo. The engine room, bridge, passenger seating, crew quarters, " +
            $"life-support systems and fuel tanks use most of that space. The remaining {company.CargoCapacity} " +
            "tons can be used to haul cargo.",
        "larger" => "You cannot buy or sell your ship whenever you feel like it. The Traders' Union strictly " +
            "regulates commercial ship sales to maintain a trade balance and prevent larger companies from " +
            "monopolizing the market.\n\nAs a member of the Traders' Union, your name is entered into a lottery. " +
            "If selected, you can trade in your old ship and purchase a new, larger ship.",
        "tank" => $"The fuel capacity of your ship is {company.FuelCapacity} tons. This is the maximum amount of " +
            "fuel your ship can carry.\n\nA larger tank lets you wait for a later planet when the local fuel price is high.",
        "crew" => $"The number of employees depends on the size and model of your ship.\n\nYour company employs " +
            $"{company.CrewCount} crew members. Each receives a weekly salary of {company.CrewSalary:N0} kubars. " +
            "You will not need additional employees until you purchase a larger ship.",
        "engine" => $"Your ship has a {company.BaseEngineSpeed}-kuarp engine" +
            (company.Turbocharges > 0 ? $" with {company.Turbocharges} turbocharge upgrade(s), for an effective speed of {company.EngineSpeed} kuarps" : string.Empty) +
            ". The higher the kuarp value, the faster your ship can travel. This helps you beat competitors to a " +
            "planet and buy the best commodities.\n\nTravel time is roughly the distance divided by engine speed. " +
            "Faster engines can be purchased on Pyke; turbocharging is available on Xeen.",
        "passengers" => $"Passenger capacity depends on ship size and model. Your ship accommodates up to " +
            $"{company.PassengerCapacity} passengers.\n\nMore advertising attracts more travellers, while higher ticket " +
            $"prices reduce demand. You currently have {company.Passengers} passenger(s) aboard.",
        "cargo" => $"Cargo capacity depends on ship size and model. Your ship can hold up to " +
            $"{company.CargoCapacity} tons and currently carries {company.CargoUsed} tons. Generally, larger ships " +
            "can transport more commodities between planets.",
        "fuel" => "Every trip consumes between 2 and 16 tons of fuel. Longer distances and larger ships usually " +
            "consume more. Turbocharging currently increases engine speed without increasing fuel use. " +
            "Travel to a nearby planet when you need to conserve fuel.",
        _ => ShipInfo
    };

    public static string Graph(decimal target) =>
        $"Net Worth is computed by adding a company's cash, savings, stock and other assets, then subtracting " +
        $"money owed to the Traders' Union or Mr. Zinn. A company wins at {target:N0} kubars.\n\nCompany History " +
        "shows each company's net worth over the last 20 weeks.\n\nMarket Strength is based on ship size; larger " +
        "ships can transport more cargo and passengers.";
}
