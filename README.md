# OpenTradeEngine

## Event debugger

During a game, open **File Options** and choose **Event Debugger**. It walks through the independent Quaso encounter, all 46 good-event slots, and bad events 2 through 19. Each event is built with disposable test data, so accepting offers or suffering losses cannot change the active campaign or its save.
Press **F10** from any in-game screen to open it immediately.
Press **Shift+F10** to jump directly to the first bad-event card.

OpenTradeEngine is a modern, open-source reimplementation of the game engine used by **Gazillionaire**.

The project does not distribute Gazillionaire's original artwork, sounds, text, or other game assets. A legitimate installation of Gazillionaire is required. OpenTradeEngine loads those assets from the user's installed copy, similarly to how projects such as OpenMW use the data from an original game installation.

## Gameplay debug logs

The launcher can optionally record a complete plain-text gameplay audit log. It records human and AI game actions and their resulting state, including trades, passenger and advertising results, banking, loans, fuel, stock actions, planet specials, route candidates, auction bids, event rolls and outcomes, weekly market processing, and save/load activity. Raw keyboard input, pointer movement and mouse coordinates are not recorded.

Logs are written to the local OpenTradeEngine `logs` folder. The launcher provides 25, 50, 100 and 250 MB storage limits. Individual files rotate at 10 MB, and the oldest completed segment is removed when the selected total limit is reached. Logging is disabled by default and never changes the save-file format.

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

The first complete AI target is **Intermediate**, which is the standard ruleset and receives no hidden bonuses. It should simply make the best decisions it can with the same information and resources available to a human player. Easier and harder personalities can be derived later by adding decision noise or deeper planning rather than changing the rules.

The Intermediate planner will:

- Randomly select an unused ship after all human selections, weighted toward that personality's preferred models but falling back to any remaining model.
- Sell existing cargo and rank new commodity/destination combinations by real expected journey profit, including purchase price, destination sale price, tariffs, fuel and operating reserves.
- Prefer the highest total achievable profit rather than blindly selecting only the highest profit per ton; cargo capacity, available stock and affordable quantity therefore matter.
- Scale commodity advertising against usable cargo capacity and expected commodity profit, and passenger advertising against available passenger seating and expected fare revenue.
- Compare an insurance premium with expected uninsured loss using real cargo value, the company's hidden event odds and the likely insured damage exposure. Luck remains internal and is never displayed to the player.
- Plan during human turns when no earlier-arriving company can invalidate its local market snapshot, then validate stock, cash and destination assumptions immediately before executing the turn.
- Use personality weights for ship preferences, risk tolerance and event **choices**. Random event outcomes remain governed by the shared resolver and are never biased in the AI's favour.

### Engines and turbocharging

In the original implementation, purchasing an engine upgrade and turbocharging an engine have the same mechanical result:

```text
engine += 1
```

OpenTradeEngine will represent the installed engine and its turbocharged state separately.

Planned rules:

- Turbocharges are permanent ship upgrades and remain installed when the base engine is replaced.
- Turbocharging is stackable: an installed engine can receive multiple turbo upgrades.
- Each turbocharge increases the effective engine speed by one kuarp.
- Turbocharging currently has no fuel, cash or reliability penalty beyond its purchase price. Its long-term balancing trade-off is intentionally undecided.
- Buying, trading or otherwise replacing the base engine preserves every turbocharge.
- Pyke sells replacement engines.
- Xeen's mechanic and applicable travel events turbocharge the currently installed engine.

Turbocharging is still stored and displayed separately from the installed engine so a future balancing rule can be added without collapsing the two systems back together. For now it is simply a purchased `+1` effective engine-speed upgrade.

Example:

```text
Installed engine:  5 kuarp
Turbocharges:      2
Turbo speed:       +2 kuarp
Effective speed:   7 kuarp
Fuel penalty:      None
```

The interface should make the distinction visible instead of displaying both operations as an indistinguishable engine-level increase.

### Events, luck, and shared simulation rules

Human and AI companies will use the same event resolver. An event must not become a generic AI cash adjustment merely because its normal presentation is written for a human player.

- Every company has the same event-relevant state, including luck or good-event probability, cargo, passengers, fuel, insurance, ship equipment, cash, and travel delay.
- Good and bad event selection uses the same probability rules for humans and AI companies at the same difficulty.
- Event effects modify real company state through shared operations. Cargo theft removes actual cargo, fuel loss removes actual fuel, delays alter actual arrival time, repairs cost actual cash, and rewards add the same assets or upgrades.
- Insurance evaluates and reimburses AI losses under the same rules used for humans.
- Events requiring a decision expose the same legal choices to the AI. Personality and risk tolerance determine its selection, but do not grant additional options or immunity.
- Planet-specific, travel, news, weather, sabotage, tax, and ship-upgrade events must identify all affected companies and apply their effects consistently.
- Random rolls should be reproducible from saved game state so reloading cannot silently produce different outcomes.

Human players receive the original artwork and narrative text for an event. AI events may be summarized in news or turn reports, but the underlying resolution is identical.

### Markets and commodity supply

OpenTradeEngine retains the original randomly changing `comR`-style economy. A high rating makes a commodity more plentiful and cheaper on a planet, while a low value makes it scarcer and more expensive. This produces useful differences between planets without requiring a detailed production-and-consumption simulation.

The original pools commodity advertising and ship-size contributions by planet. OpenTradeEngine retains the resulting real shared planetary inventory, but records which company generated each advertised share so a campaign benefits its purchaser rather than silently granting identical access to every competitor. Human cargo capacity above 100 contributes directly, while the simplified original AI used:

```text
AI stock contribution = max(0, floor(ship capacity / 4 - 100))
```

OpenTradeEngine gives AI companies real ships, so the destination pool uses real excess cargo capacity for every company. Advertising contributes its verified `floor(spend / 50)` amount. Each commodity's planet rating controls how much of that combined pool materializes. The organic part is public, while each company receives access to its proportional advertised part; every purchase still depletes the same inventory.

The revised commodity system does the following:

- Preserve the original `comR` price, scarcity, and weekly volatility model.
- Apply advertising and real excess ship capacity at the destination, replacing the earlier artificial `log2(week)` stock-growth multiplier, while keeping campaign access company-specific.
- Subtract real purchases by players and AI companies from the stock available for the remainder of the week.
- Add real sales by players and AI companies where the market rules call for returned stock.
- Apply the same availability and transaction rules to human and AI companies.
- Preserve shortages, regional price differences, advertising effects, and opportunities for speculation.

Late-game quantity growth therefore comes from companies operating larger ships and purchasing stronger campaigns, while the `comR`-style rating and 0-to-130 availability roll preserve shortages and regional identity.

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
- Human and AI companies buy shares with real cash and may purchase as many as they can afford.
- The original 10,000-kubar weekly ceiling is removed rather than replaced with another percentage cap.
- Exchange closures and crash losses apply identically to every company.
- AI personality thresholds may still determine when a company buys or liquidates its local holdings.

The interface displays the local share price, average price paid, shares owned, and the company's available cash, savings, and loan balance. AI personalities should reserve operating cash strategically, but they receive no private transaction ceiling or free shares.

## Current status

The project currently provides:

- An Avalonia desktop application.
- A first-launch installation-folder browser.
- Validation for `Gazillionaire.swf` and the `SWF`, `PNG`, and `MP3` asset folders.
- An opening menu based on the original Gazillionaire screen.
- Static extraction and local caching of the original `ZILE2.SWF` title artwork through FFDec during development.
- Screen artwork is loaded only from the installed game's embedded PNG/JPEG assets. The former SWF frame-rendering path has been removed, so character animations cannot leak into static screens; composite views such as the title screen layer their original embedded pieces directly.
- Main-menu controls for fullscreen, sound state, information screens, and quitting.
- An additional **About OpenTradeEngine** menu entry.
- New-game setup for difficulty, one to six humans, zero to six AI companies, seven-planet selection, company naming, ship selection, financing, player turns, arrival and net-worth screens.
- A persistent campaign model for weeks, companies, ships, cash, savings, loans, cargo, passengers, fuel, markets, warehouses, stocks, advertising, insurance, luck, engines and turbocharges.
- All 18 original commodity names and exact price ranges (minimums 5 through 90 in five-kubar steps, with eight-times maximums), planet-specific supply identities, weekly price/supply movement, finite shared inventories, weighted cargo costs, and real buy/sell transactions. Generated, event-modified, and restored legacy-save prices are clamped to the range displayed by the original Marketplace.
- AI commodity and stock decisions that use real cash, ships, cargo capacity, finite market stock, brokerage commission, fuel and destinations. Rising-market stock purchases retain a payroll, fuel, debt and cargo operating reserve instead of consuming all cash before commodity trading; falling local holdings are liquidated.
- The first Intermediate AI planner evaluates complete destination cargo packages rather than only the largest per-ton margin. It fills the real hold across the most profitable available commodities, subtracts import/export tariffs, fuel, landing fees, payroll, debts and accrued obligations, and includes expected uninsured loss based on cargo value, hidden event odds and hazardous weather. Insurance and passenger/commodity advertising are purchased only when their expected benefit supports the cost, with the original six personalities modifying reserves, risk tolerance and strategic emphasis without receiving rule advantages.
- AI warehouse speculation uses a strict surplus-wealth gate: after protecting its operating reserve, planned cargo, fuel, facility fees, insurance and advertising, an AI may store genuinely cheap high-value goods on planets it visits often. Stored cargo is included in return-route planning and sold once the local net price provides a worthwhile margin. Actual warehouse fires are remembered in saves; uninsured losses heavily suppress future storage at that planet and repeated losses also reduce warehouse use elsewhere.
- Passenger ticket pricing with a smooth demand curve and diminishing advertising returns, plus all seven original advertising choices from None through Everything. Advertising is carried to the next planet for one week, including for AI companies. The original ship-mass-scaled price ladder and `spend / 50` commodity-supply contribution are implemented.
- Fuel purchasing and fuel-consuming interplanetary travel, with the original seven navigation coordinates and `distance × 5 ÷ effective engine speed` arrival-time formula exposed in destination choices.
- Every company starts with warehouse storage on every active planet; storage and retrieval persist independently by planet, and capacity upgrades apply across the entire network.
- Original-style Marketplace, Supply and Warehouse screens with the complete 18-commodity catalogue, shared cargo/cash summaries, display filters and direct navigation between the three trading views.
- Gazillionaire-style numeric entry overlays are shared by banking, loans, ticket prices, fuel purchases, auctions, commodity trades, warehouse transfers and stock trades, including keyboard confirmation. The standard Lower/Middle/Upper buttons fill one quarter, one half and the full permitted amount; passenger fares use the original fixed 1,000/3,000/5,000 presets and 100-kubar minimum instead.
- The Stock Market now reproduces the original chart-led layout, 1,000-kubar graph baseline, 1,700-to-1,100 starting exchange values, local/all/owned-share views and average purchase-price display.
- Separate original-style finance destinations: Money opens company status, Bank handles savings deposits and withdrawals, the Traders' Union handles standard borrowing and repayment, and Mr. Zinn handles repayment of the ship-financing debt.
- Interest is applied every game week and retained for each screen's “Last Week” display, rather than treating the displayed rates as annual Earth rates. The original 100,000–150,000 starting Zinn debt by difficulty, 100,000 Traders' Union credit ceiling, and weekly 5%/4%/1% Traders' Union, Zinn and savings rates are implemented.
- Verified original starting defaults including a 1,000-kubar ticket price, 1,500-kubar crew salary and 50-ton warehouse capacity.
- The original 3% import tariff, 2% export tariff and 15% passenger tax are accrued separately from operating expenses.
- Seven planetary stock exchanges with the original 1,700-to-1,100 starting price scale, persistent price-history charts and portfolios, average purchase prices, unrestricted cash-limited purchases, a 1% commission, and exchange crashes/closures that affect humans and AI equally.
- Shared human/AI travel-event resolution using real company state, luck, cargo damage, repair costs and insurance reimbursement.
- Voyager's Insurance follows the original one-purchase, full-coverage, next-trip-only model with a fluctuating quote in the original 15–15,000 range; AI buys and consumes the same policies.
- Pyke replacement engines and Xeen's four original mechanic-offer categories: turbocharging, cargo-bay expansion, passenger seating and fuel-tank expansion. Pyke now uses the decompiled `company roll × weekly news roll × 6` quote, its original one-in-four unavailable weeks, and the original cash-then-savings-then-Traders'-Union-loan payment order. Base engines, permanent turbo levels and capacity upgrades persist independently; replacing or trading an engine preserves turbocharges.
- Tilo's weekly 5%-of-cash All Or Nothing wager, Bass broker market details, Nosh's original `10% + company roll / 2` wholesale fuel discount, Zile's Zinn financing favors, and Frac insurance rate reviews. Tilo, Pyke, Xeen, Queg and Nosh now share the original weekly news roll for their availability and price branches.
- The original Explore Planet film-strip layout, using the installed SWF's embedded static city illustration rather than an animation frame, with Special, Weather, News, Time and About Planet screens that return to the city hub. Planet Specials now use the original navy, framed-character layout, original headings/action labels and reconstructed explanatory copy. Weather Bureau and News Center reproduce the matching framed-character layout and provide Back/Main Menu navigation; Ministry of Time and planetary history use the original Back/Prev/Next paging layout. Their artwork is selected from embedded static SWF images rather than rendered animation frames. All five original Ministry of Time subjects and all 83 original history pages across the fourteen planets are present in their original order.
- All fourteen planet specials are translated from the original `frm_Special_action` tables, including their original result bands, unavailable weeks, full-offer rules, financing effects and sound-result branches. Queg rotates through the original three classes of six commodities, calculates its offer from difficulty and the shared news roll, and caps the quote at 90% of the local market price. Vexx uses the original 1-to-30 petition table: only rolls 7–10 and 22–25 change state, tax/tariff rate changes apply system-wide, minimum rates make the petition backfire, and emergency relief clears only the petitioner's accrued taxes and tariffs. Mira uses its decompiled 1-to-30 table too: flavour-only audiences leave luck untouched, curses explicitly set the good-event chance to 15%, and blessings apply the original 60/70/75/80/85 floors followed by five-point improvements capped at 85%. Stye uses the original 1-to-31 table, including neutral meetings, 50,000/75,000-kubar credit extensions, conditional loan/savings rate improvements or backfires, and one-third or one-quarter Traders' Union debt forgiveness.
- All 70 original deterministic weekly Weather Bureau reports, saved with the campaign; hazardous ranges 1â€“10 and 61â€“70 feed the shared human/AI travel-event resolver while flavour reports 11â€“60 remain informational.
- The News Center now uses all 124 original general reports from the installed game's text library, with deterministic planet, company, commodity and poll-value substitutions and no leftover Flash markup. Bullish, bearish and mixed financial reports now use and establish the same exchange trend consumed by the following stock-market movement.
- The original weekly 1â€“20 global-economic-event roll is restored: reports 1â€“16 apply their price, quantity, fuel or government-rate changes and 17â€“20 produce no event. Notices use the original large planet announcement flow and `EVENT.MP3`.
- The Ministry of Time now displays the live Kukubian calendar beginning at 139.00 A.B. and advancing one fiftieth of a kuku year per completed week.
- Manually payable accrued crew wages, passenger taxes and commodity tariffs, with AI companies using the same payment operations.
- Random ship and government-facility auctions using all 100 original facility identities, secret human bids, original-style AI valuation, persistent ownership, a galaxy-map facilities ledger, and landing fees transferred between companies. As an intentional OpenTradeEngine balance change, every eligible week independently has a flat 25% overall auction chance regardless of player count, with an approximately one-to-three ship/facility mix after facilities unlock. AI ship bids use the decompiled net-worth bands, doubling and smaller-ship catch-up chance; facility bids use the original fee/net-worth bands, adjusted by personality risk and a persistent repeat-visit preference that adds 12.5% per return to that planet up to 2.5×. Unlike the original AI, every computer bid is capped at cash, savings and unused Traders' Union credit—the same financing available to humans. Facility auctions remain blocked for the first ten weeks, tutorial auctions unlock after their corresponding lessons, and tied auctions repeat with fresh secret bids.
- Auction offers and personal travel events share the original two-column event presentation: solid navy background, large white framed static illustration on the left, heading and copy on the right, and wide grey confirmation controls. Both ship-upgrade and facility auctions observe the opening ten-week grace period and can first appear in week 11. Auction announcements show Dred from the installed `DRED.SWF`; pressing **OK** then opens the common numeric secret-bid dialog, where a zero bid declines. Winning-auction results, competitor arrival taunts and public sabotage-damage reports instead use the original centered company-announcement layout with the featured company's portrait, copy below it and a wide bottom **OK** button. Tie and no-bid auction results remain ordinary event cards.
- A broader shared travel-event set covering cargo loss, pirates, storm damage, fuel leaks, fines, repairs, wage and tax forgiveness, free engine and warehouse upgrades, loan/savings/insurance rate changes, credit-limit changes, lawsuits, forced Zinn repayment, inheritance and lottery awards. Implemented events affect human and AI state through one resolver and use matching installed static artwork/audio where identified.
- Interactive travel encounters now use a shared Accept/Decline and outcome flow for humans and AI. The verified early set includes Quaso Mutta's independent one-in-forty destination redirect; the 25,000–100,000-kubar 200-ton ship expansion with all twelve decompiled model-specific engine, fuel, cargo, passenger, crew and insurance packages; the 15,000–50,000-kubar global warehouse expansion; and Mr. Zinn's 50,000-kubar loan extension with its separate 100,000-kubar credit-ceiling increase. The passive events 4–16 are mapped to their original wage/tax forgiveness, engine, insurance, loan-rate, savings-rate, credit-limit, inheritance, lottery and free-warehouse effects. The verified set also includes Nectum's 100-kubar-per-ton Exotic offer, Captain Leahy's double-cost cargo purchase, all 20 of Mulls' original advice lines, Teeter's four ship upgrades with the original `company roll × weekly roll × 6` price, and Meeg's 5,000–25,000-kubar crew-automation gamble. The risky original encounters are stateful too: Scooter Jay pays four times cargo cost but has a 20% police check and seven-times-cost fine; Hands swaps stolen cargo for a hold full of Exotic but risks confiscation and a 50,000-kubar fine; Curtonian has a 25% chance to repay six times the loan; Quist never records his promised return; The Wobbler has a one-in-three flop; Yoyo's coin is rigged; and donating all cargo to Limpus applies the original luck value of 85. Original Travel2 events 25–34 include Sleg's fixed 150,000-kubar three-kuarp engine trade, Emperor Dred's royal visit, Iso's 25–75-times-ship-mass gift, Raffety's 115%-of-market stock cash-out, Nebbit's 20%-discount share offer, Tatilus's 5,000-per-empty-seat emergency passengers, Gurttle's half-tank purchase at 3,500 per ton, Lord 104's triple-cost cargo offer, Nectum's Exotic deal and Squowk's full-hold cargo swap with transferred cost basis. Spike uses the original 15–50-times-ship-mass damage, Nibble uses the same fee scale with a 20% strike failure and ±100 salary result, and the Speevak offer uses 25–125 times ship mass with a 25% police risk and double-offer fine. Event 42 restores the Hapa Jillo syndicate's 20–70-times-ship-mass fee, attacks an anonymous random subset of active competitors with the original ship-scaled damage range, and reproduces its 20% police fine, travel delay and explicit luck value of 45. Each damaged company is announced separately to every active human without identifying the attacker. Their original exclusions from insurance, debt fallback and travel delays are stateful. AI companies make decisions without knowing hidden outcome rolls. Human choices are applied before event odds, autosaving or week advancement.
- AI companies use applicable planet services through the same cash, capacity, luck, banking, fuel, cargo and weekly-use rules as human companies.
- Original-style liquid financial net-worth accounting (cash plus savings and share holdings, minus Traders' Union and Zinn debt), a five-million-kubar standard victory check, bankruptcy tracking, and campaign outcome screens. Ships, cargo at cost, unpaid wages and unpaid tariffs are excluded from the weekly ranking, matching observed original-game results. The Traders' Union and Mr. Zinn limits are checked against the debt after the coming weekly interest: humans receive the original return-to-menu-or-leave warning, AI companies make the minimum affordable protective repayment, and actually crossing either limit always causes bankruptcy. New bankruptcies use the original two-stage loss announcement and Headline News presentation with installed static `LOSE.SWF`/`NEWS_L.SWF` artwork and separate text for avoidable versus forced bankruptcy. A winner may keep playing toward progressively larger targets up to ten billion kubars.
- Tutorial games now use the original persistent 17-stage teaching sequence and a dedicated tutorial screen. Stages 1â€“7 follow the decompiled turn schedule (including the original turn-two and turn-five skips); after Fuel, a one-human game exposes **Add New Feature** with `MORECOMPLEX.MP3` so the player controls the pace, while multiplayer tutorials add one feature per completed week. Main-menu services, Distance/Facilities charts, Stock Markets and File Options shortcuts unlock from the saved tutorial stage, and Tutorial and Novice correctly share economy difficulty zero.
- The Journey galaxy map uses the seven verified original coordinates and embedded static planet artwork, with current-location marking plus the original shared Distance/Facilities chart layout. The Distance view selects a destination and lists every company's location, engine and geometric distance in million kuters. The Facilities view lists each company's facility count, total landing fees and uncollected revenue on the selected planet. Competitor fees accrue until the owner lands there, matching the original rules, and revenue is persisted in saves. File Options and its 13 On/Off Shortcuts reproduce the original menu structures. Its saved **AI Event Reports** option supports Full, Default and None: Full explains all non-quiet AI event outcomes, Default reports accepted offers and police catches without financial details, and None hides optional AI-event reports. This is presentation-only; public sabotage damage, auction results and arrival dialogue remain visible. Quick Marketplace and Warehouse grid transfers, repeated advertising, banking, loans, crew, fuel, exploring, passengers, taxes, insurance and pre-travel cash deposits are functional and persisted in saves.
- A data-driven mod loader scans the executable-side `Mods` folder when **Enable Mods** is selected on the installation launcher; the global choice is saved and disabling it leaves every mod folder untouched but unloads all mod content. Each mod is an ordinary folder with an optional `mod.json` manifest and content in `Planets`, `Events`, and `Assets`. Planet definitions can add selectable worlds or override descriptions and static artwork; declarative travel events can target good/bad chains, particular planets, weeks, cash and luck without executing untrusted code. **File Options → Options** can open or reload the folder and reports invalid content. See `Mods/README.md` for the complete initial format.
- The included **Chaos Monk** example mod demonstrates a specialized private sabotage effect: it is unavailable to the highest-net-worth company, costs 5 kubars per ship ton, has mutually exclusive 1-in-12 backfire, 1-in-6 everyone, and 3-in-4 random-opponent outcomes, and changes luck rather than money. Victims receive private Ooom warnings without revealing luck values or the attacker.
- The Bank, Traders' Union loan and Mr. Zinn loan remain separate institutions with their original labels, installed static character illustrations, weekly interest readouts, credit information, action sets and institution-specific help. Their amount dialogs use the common original-style Lower/Middle/Upper numeric modal and show the appropriate cash, savings, standard-loan and Zinn-loan balances. Savings interest is assessed every game week and respects the original 100,000-kubar weekly maximum.
- Context-sensitive Help is connected across the main planetary menu, Marketplace, Supply, Warehouse, Advertising, Passengers, Crew, Fuel, Taxes, Insurance, Journey, Explore, Money, graphs, finance institutions and Shortcuts using the original GameStrings copy with live company values. Ship Info restores the original eight interactive reference topics and all twelve original ship model names.
- Automatic and manual JSON campaign saves, plus working **Load Saved Game** restoration.
- Persistent per-company weekly wealth history with the original default Company History line graph, plus Net Worth and Market Strength graph modes. The saved history survives reloads and uses the original graph/legend/button layout.
- Contextual original MP3 playback loaded from the selected installation. `PING1` is confined to the title menu instead of firing for every in-game button; passenger, fuel, advertising, insurance, crew and tax effects play only after their corresponding operation succeeds, including when a shortcut performs that operation immediately. Marketplace purchases/sales and warehouse transfers use the decompiled commodity-specific voice mapping for all 18 goods. The Bank, Traders' Union and Mr. Zinn screens retain their decompiled `*_load` opening voice lines, with deposits and withdrawals using their appropriate action sounds. Planet audio continues through the arrival/report sequence and stops when the planetary marketplace menu opens.
- Stable seeded market and event rolls across process restarts, avoiding .NET's randomized string hashes.
- One shared human/AI arrival order reconstructed from `frm_Travel3_travel`: every active company receives one turn per week, ordered by `distance × 5 ÷ effective engine speed`, with deterministic tie-breaking. Travel2 delays multiply that journey before resetting, and apply to AI as well as humans in OpenTradeEngine. AI companies that arrive between human turns act immediately and deplete the same commodity pool before the later company opens its marketplace. Arrival timing, turn cursor and crew automation persist in saves.
- A standalone simulation smoke test covering trading, AI, passengers, warehouses, stocks, events, turbocharging and save restoration.

The recreation is now a playable vertical slice, not a complete Gazillionaire replacement. The complete numbered Travel2 good/bad event tables and all fourteen planet-special families are represented and share one human/AI resolver; remaining work in those areas is fine-grained text, artwork, sound and rare eligibility comparison rather than missing event families. Remaining compatibility work includes exact auction opponent-bidding strategies, complete semantics for every shortcut, final tutorial text/layout comparison, multiplayer edge-case sequencing, and long-run balance verification against the decompiled formulas.

Some newly introduced economy values are provisional compatibility baselines where the exact original constant has not yet been verified. Keep those values isolated in the simulation/catalog classes and replace them as the matching p-code is decoded; do not treat them as confirmed original data.

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
