# OpenTradeEngine

OpenTradeEngine is a modern, open-source reimplementation of the game engine used by **Gazillionaire**.

The project does not distribute Gazillionaire's original artwork, sounds, text, or other game assets. A legitimate installation of Gazillionaire is required. OpenTradeEngine loads those assets from the user's installed copy, similarly to how projects such as OpenMW use the data from an original game installation.

## Goals

- Reimplement Gazillionaire's game logic in modern C# and Avalonia.
- Preserve the original game's rules, presentation, humour, and overall flow where appropriate.
- Load original images, static SWF artwork, sounds, and game text from the installed game.
- Keep original copyrighted assets out of this repository and release packages.
- Document the original mechanics as they are decoded from the game.
- Improve selected systems that were simplified in the original implementation.

## Intentional gameplay changes

OpenTradeEngine is purpose-built for Gazillionaire compatibility, but it will not reproduce every original shortcut when a more complete mechanic would improve the simulation.

### Player-like AI

The original opponents use a heavily simplified simulation. They have abstract cargo, simplified buying and selling, and separate event logic. They do not maintain all the state or make all the decisions available to a human player.

OpenTradeEngine will work toward opponents that play by the same underlying rules as players. AI companies should eventually be able to:

- Buy and sell real commodities using real cargo holds.
- Pay the same market prices, taxes, tariffs, fuel costs, and operating expenses.
- Choose destinations based on trade opportunities and strategy.
- Purchase ships and engines using the same systems available to players.
- Use banks, loans, warehouses, facilities, stocks, passengers, advertising, insurance, and other applicable systems.
- Experience events through consistent game rules rather than relying only on abstract cash adjustments.
- Become bankrupt or successful because of their actual decisions and circumstances.

AI personalities and difficulty levels can still influence strategy, risk tolerance, planning ability, and imperfect decision-making. The intention is not to make every opponent optimal; it is to make their results arise from playing the game.

This work should be introduced gradually. Until a system has a complete player-like AI implementation, the verified original behaviour can be used as the compatibility baseline.

### Engines and turbocharging

In the original implementation, purchasing an engine upgrade and turbocharging an engine have the same mechanical result:

```text
engine += 1
```

OpenTradeEngine will represent the installed engine and its turbocharged state separately.

Planned rules:

- A newly purchased engine starts unturbocharged.
- Turbocharging is stackable: an installed engine can receive multiple turbo upgrades.
- Each turbocharge increases the effective engine speed by one kuarp.
- The first turbocharge increases normal fuel consumption by 10%. Each later turbocharge adds 90% of the previous turbocharge's fuel penalty, producing diminishing additional fuel costs.
- Buying a replacement engine removes all turbocharges attached to the old engine.
- Pyke sells replacement engines.
- Xeen's mechanic and applicable travel events turbocharge the currently installed engine.

This makes turbocharging a trade-off rather than a second name for an ordinary engine upgrade: the ship arrives sooner, but the player must carry and purchase additional fuel. The individual penalties are `10%`, `9%`, `8.1%`, `7.29%`, and so on. Fuel remains fractional when applying this modifier instead of being rounded up to another whole ton. The cumulative effect must be clearly displayed before the player accepts another turbocharge and in the ship-information screen afterward.

Example:

```text
Installed engine:  5 kuarp
Turbocharges:      2
Turbo speed:       +2 kuarp
Effective speed:   7 kuarp
Fuel multiplier:   1.19x
```

The interface should make the distinction visible instead of displaying both operations as an indistinguishable engine-level increase.

### Markets and commodity supply

OpenTradeEngine will retain the original randomly changing `comR` economy. A high `comR` makes a commodity more plentiful and cheaper on a planet, while a low value makes it scarcer and more expensive. This produces useful differences between planets without requiring a detailed production-and-consumption simulation.

The part requiring redesign is quantity scaling. In the original game, AI ships artificially create additional stock at their current planet according to cargo capacity, without actually selling cargo into that market:

```text
AI stock contribution = max(0, floor(ship capacity / 4 - 100))
```

Consequently, market supply tends to increase later in the game merely because opponents own larger ships. This is incompatible with the goal of having AI companies trade through the same systems as the player, and it makes late-game scaling depend on incidental AI locations.

The revised commodity system should:

- Preserve the original `comR` price, scarcity, and weekly volatility model.
- Remove the artificial relationship between visiting AI ship capacity and newly generated stock.
- Scale generated quantities through an explicit economy-wide progression so larger late-game cargo ships remain useful.
- Make that progression consistent across planets rather than dependent on which AI ships happen to be present.
- Subtract real purchases by players and AI companies from the stock available for the remainder of the week.
- Add real sales by players and AI companies where the market rules call for returned stock.
- Apply the same availability and transaction rules to human and AI companies.
- Preserve shortages, regional price differences, advertising effects, and opportunities for speculation.

The precise quantity-progression formula still needs to be designed and tested. It should grow slowly enough to preserve scarcity while providing enough stock for later ships, without changing the established `comR` identity of the economy.

### Passengers and advertising

The original passenger system generates a private random passenger count for each company from ship capacity and advertising. OpenTradeEngine will retain independent passenger generation rather than introducing a shared passenger market. The part requiring redesign is the abrupt ticket-price penalty, which makes 4,000 kubars combined with maximum advertising an unusually strong universal strategy: raising the fare to 4,001 immediately applies an additional demand penalty.

OpenTradeEngine will rework passenger transport so that:

- Ticket-price demand follows a smooth curve without exploitable threshold points.
- Small fare changes produce correspondingly small changes in demand.
- Advertising has diminishing returns instead of simply overwhelming the passenger-capacity limit.
- Passenger availability remains independent for each company and is not depleted by competitors.
- Players and AI companies use the same independent passenger-generation and fare-demand rules.
- Optional passenger luxuries may reduce fare resistance in exchange for a visible per-passenger or per-journey cost.
- Luxury benefits have diminishing returns so they do not merely replace 4,000 kubars with another universal optimum.
- Cheap, moderate, and premium fares remain viable in different circumstances instead of one price being universally optimal.
- Passenger revenue, commissions or operating costs, and tax treatment remain clearly visible to the player.

A continuous demand curve is the intended baseline. Its exact shape, advertising response, and any passenger-luxury costs will be tuned during implementation rather than preserving the original stepped multipliers.

### AI stock trading

The original AI genuinely pays the market price for shares and receives the market value when selling, so its trend-based stock strategy can be retained. However, its transaction rules will be normalised with the player's rules:

- AI companies pay the same 1% brokerage commission on purchases and sales.
- Human and AI companies use the same once-per-week purchase restriction.
- Human and AI companies may invest at most 1% of their available cash and savings in new shares each week.
- The original 10,000-kubar weekly ceiling is removed, allowing stock investment to scale with company wealth.
- Exchange closures and crash losses apply identically to every company.
- AI personality thresholds may still determine when a company buys or liquidates its local holdings.

The interface must clearly display the 1% allowance, the amount already invested during the current week, and the remaining amount available. AI personalities may choose to invest less than their allowance, but cannot exceed the same limit imposed on human players.

## Current status

The project currently provides:

- An Avalonia desktop application.
- A first-launch installation-folder browser.
- Validation for `Gazillionaire.swf` and the `SWF`, `PNG`, and `MP3` asset folders.
- An opening menu based on the original Gazillionaire screen.
- Static extraction and local caching of the original `ZILE2.SWF` title artwork through FFDec during development.
- Main-menu controls for fullscreen, sound state, information screens, and quitting.
- An additional **About OpenTradeEngine** menu entry.

New-game setup, saved games, and the main game simulation have not yet been implemented.

## Development reference

The local development workspace currently expects reference material outside this Git repository:

```text
C:\Users\kille\source\repos\Gazillionaire
C:\Users\kille\source\repos\decompiled_ffdec
```

These paths are development references only and must not become release requirements. Released builds will ask the user to locate their own Gazillionaire installation.

## Building

Requirements:

- .NET 10 SDK
- Avalonia dependencies restored through NuGet
- A Gazillionaire installation for runtime assets
- JPEXS Free Flash Decompiler (FFDec) for the current development SWF-to-PNG extraction path

Build from the repository root:

```powershell
dotnet build
```

The development executable is generated under:

```text
bin\Debug\net10.0\OpenTradeEngine.exe
```

## Asset policy

Do not commit or distribute:

- Gazillionaire SWF files
- Extracted or converted original artwork
- Original PNG or MP3 files
- Decompiled ActionScript or p-code dumps
- Cached assets generated from the user's installation

Source code, compatibility metadata, mechanics documentation, and extraction instructions may be included without bundling the original game assets.
