# OpenTradeEngine Mods

Put each mod in its own folder beside this file. Folders beginning with `_` or `.` are ignored.

```text
Mods/
  MyMod/
    mod.json
    Planets/
      my-planet.json
    Events/
      lucky-find.json
    Assets/
      planet.png
      event.png
      event.mp3
```

`mod.json` is optional. Without it, the folder name becomes the mod name.

```json
{
  "name": "My Mod",
  "version": "1.0.0",
  "author": "Your Name",
  "description": "Example content mod.",
  "enabled": true,
  "priority": 0
}
```

## Planets

A planet definition can add a selectable planet or override the description and artwork of an original planet with the same name.

```json
{
  "name": "New Terra",
  "description": "a recently settled agricultural trading world.",
  "subtitle": "The Agricultural Colony",
  "icon": "Assets/planet-icon.png",
  "arrivalImage": "Assets/planet-arrival.png",
  "cityImage": "Assets/planet-city.png"
}
```

PNG artwork is recommended. Missing artwork falls back to the engine's normal behavior. New planets currently use the generic Explore Planet special; their markets, warehouses, stocks, travel, facilities, passengers and fuel work normally.

## Events

Each event is an independent check in the selected good or bad journey chain, so more than one mod event can occur during a journey.

```json
{
  "id": "my-mod.lucky-find",
  "heading": "Lucky Find",
  "message": "The crew finds a valuable case on {planet}, worth {cashChange} kubars.",
  "kind": "Good",
  "chancePercent": 10,
  "minWeek": 2,
  "planet": null,
  "cashChange": 15000,
  "luckChange": 2,
  "image": "Assets/event.png",
  "audio": "Assets/event.mp3"
}
```

`kind` may be `Good`, `Bad`, or `Either`. A negative `cashChange` is paid from cash, then savings, then the Traders' Union loan, like other mandatory game expenses. Luck remains inside the engine's 15–85 limits. Supported message tokens are `{company}`, `{planet}`, and `{cashChange}`.

The optional built-in `ChaosMonkSabotage` effect supplies the more complex private luck-sabotage behavior used by the included Chaos Monk mod. It supports `feePerShipTon` and the additional `{fee}` message token. The highest-net-worth active company is ineligible for this offer.

## Included Chaos Monk mod

`Mods/ChaosMonk` uses the supplied `chaosmonk.png` artwork and becomes eligible after the normal ten-week sabotage grace period. Its fee is 5 kubars per ton of the buyer's ship—half Brow's minimum per-ton fee. The exclusive outcome odds are:

- **1 in 12:** the ritual backfires and sets the buyer to minimum luck.
- **1 in 6:** every active company, including the buyer, loses 15–50 luck.
- **3 in 4:** a random non-empty subset of opponents loses 25–50 luck; the buyer is excluded.

It is never offered to a company tied for the highest current net worth. Each affected human receives a private Ooom Soothsayer warning without numbers or an attacker identity. It creates no public announcement and is suppressed from Full AI Event Reports.

Mods must first be enabled with **Enable Mods** on the OpenTradeEngine installation launcher. The choice is remembered. Mods can then be reloaded from **File Options → Options → Reload Mods**. Planet-list changes apply when creating a new game. Travel-event changes apply to subsequent journeys. Invalid files are skipped and reported on the launcher and Options screen.
